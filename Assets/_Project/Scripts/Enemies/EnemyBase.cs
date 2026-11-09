// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// EnemyBase (M4-1): base de los enemigos del cementerio (zombi y espectro). Reemplaza al EnemyOrb del TP1.
//   - Vida y daño salen del Inspector (no se pisan por tipo como en el orbe).
//   - IDamageable: la pala y la piedra restan vida; destello de color, un tirón hacia atrás y animación
//     Hit. Al morir avisa con OnEnemyDied, reproduce la muerte y se desactiva (vuelve al pool). Al
//     desactivarse, QuestEnemy le suma la baja a la misión de limpiar el área (M4-4).
//   - IA común: patrulla (puntos fijos o al azar alrededor de donde nació), persecución por proximidad
//     con desenganche, ataque por contacto con enfriamiento y vuelta a casa.
//   - Se mueve con NavMeshAgent si hay NavMesh cerca; si no, en línea recta pegado al terreno.
// Cada enemigo concreto (ZombieEnemy, SpectreEnemy) ajusta los valores y sobreescribe lo que cambia.
// Usa los mismos parámetros de Animator que Zombie.controller y Skeleton.controller: Speed, Attack, Hit, Dead.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    /// <summary>Se dispara al morir cualquier enemigo (antes de la animación de muerte).</summary>
    public static event Action<EnemyBase> OnEnemyDied;

    [Header("Vida y ataque")]
    public int maxHealth = 60;
    public int attackDamage = 20;
    [Tooltip("Distancia a la que golpea al jugador")]
    public float attackRange = 1.6f;
    public float attackCooldown = 1.3f;

    [Header("Movimiento")]
    public float patrolSpeed = 1.2f;
    public float chaseSpeed = 2.5f;
    [Tooltip("Grados por segundo al girar (movimiento sin NavMesh y al encarar al jugador)")]
    public float turnSpeed = 360f;
    [Tooltip("Usar NavMeshAgent si el objeto lo tiene y hay NavMesh cerca")]
    public bool useNavMesh = true;

    [Header("Patrulla")]
    [Tooltip("Puntos a recorrer en orden; vacío = puntos al azar alrededor de donde nació")]
    public Transform[] patrolPoints;
    public float patrolRadius = 6f;
    [Tooltip("Segundos de espera en cada punto (mínimo y máximo)")]
    public Vector2 patrolWait = new Vector2(1f, 3f);

    [Header("Detección")]
    public float detectionRadius = 10f;
    [Tooltip("Pierde al jugador si se aleja más que esto")]
    public float loseAggroRadius = 16f;
    [Tooltip("Abandona la persecución si se aleja de casa más que esto (0 = sin límite)")]
    public float leashRadius = 30f;

    [Header("Al recibir un golpe")]
    public Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
    public float flashDuration = 0.15f;
    [Tooltip("Metros que retrocede con cada golpe")]
    public float hitPushback = 0.5f;
    [Tooltip("Segundos que queda aturdido sin moverse ni atacar")]
    public float hitStun = 0.3f;

    [Header("Terreno")]
    public LayerMask groundLayers = ~0;
    [Tooltip("Ajuste fino de altura respecto al piso (+ sube, - baja)")]
    public float groundOffset = 0f;
    public float terrainFollowSpeed = 15f;

    [Header("Efectos y sonido")]
    public GameObject deathVFX;
    public AudioClip attackClip;
    public AudioClip hitClip;
    public AudioClip deathClip;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("Animación")]
    [Tooltip("Si queda vacío se busca en los hijos; sin Animator el enemigo funciona igual")]
    public Animator animator;
    [Tooltip("Segundos que se ve la muerte antes de volver al pool")]
    public float deathAnimDuration = 2f;

    [Header("Recompensa")]
    [Range(0f, 1f)] public float dropChance = 0f;
    public string healthPickupTag = "PickupHealth";

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsChasing { get; private set; }
    public Vector3 Home { get; private set; }

    protected static readonly int AnimSpeed = Animator.StringToHash("Speed");
    protected static readonly int AnimAttack = Animator.StringToHash("Attack");
    protected static readonly int AnimHit = Animator.StringToHash("Hit");
    protected static readonly int AnimDead = Animator.StringToHash("Dead");

    protected NavMeshAgent agent;
    protected Transform player;
    protected PlayerHealth playerHealth;
    protected Collider enemyCollider;

    private Renderer[] renderers;
    private MaterialPropertyBlock flashBlock;
    private Coroutine flashRoutine;
    private Coroutine deathRoutine;

    private float lastAttackTime = -999f;
    private float stunnedUntil;
    private Vector3 lastPosition;
    private Vector3 patrolTarget;
    private bool hasPatrolTarget;
    private float patrolGiveUpAt;
    private float waitUntil;
    private int patrolIndex;

    protected bool UsingAgent => agent != null && agent.enabled && agent.isOnNavMesh;

    // ---------------------------------------------------------------- Ciclo de vida

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false; // se prende en OnEnable si hay NavMesh cerca

        // Rigidbody cinemático y collider trigger: recibe la piedra y la pala sin empujar al jugador
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        enemyCollider = GetComponent<Collider>();
        enemyCollider.isTrigger = true;

        if (!CompareTag("Enemy")) gameObject.tag = "Enemy";

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        renderers = GetComponentsInChildren<Renderer>(true);
        flashBlock = new MaterialPropertyBlock();
    }

    protected virtual void OnEnable()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        IsChasing = false;
        hasPatrolTarget = false;
        stunnedUntil = 0f;
        lastAttackTime = -999f;
        waitUntil = Time.time + UnityEngine.Random.Range(0f, patrolWait.y);
        patrolIndex = 0;

        if (enemyCollider != null) enemyCollider.enabled = true;
        ClearFlash();

        if (animator != null && animator.isInitialized)
        {
            animator.SetBool(AnimDead, false);
            animator.SetFloat(AnimSpeed, 0f);
        }

        TryEnableAgent();
        if (!UsingAgent) SnapToGround(true);

        Home = transform.position;
        lastPosition = transform.position;
        FindPlayer();
    }

    protected virtual void OnDisable()
    {
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        if (agent != null) agent.enabled = false;
    }

    private void TryEnableAgent()
    {
        if (agent == null) return;
        agent.enabled = false;
        if (!useNavMesh) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);
            agent.stoppingDistance = 0.2f; // la persecución frena sola al llegar al rango de ataque
        }
    }

    private void FindPlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) go = pc.gameObject;
        }
        if (go == null) return;

        player = go.transform;
        playerHealth = go.GetComponent<PlayerHealth>();
    }

    // ---------------------------------------------------------------- IA

    protected virtual void Update()
    {
        if (IsDead) return;

        if (player == null) FindPlayer();
        if (!UsingAgent) SnapToGround(false);

        if (Time.time < stunnedUntil)
        {
            Stop();
            return;
        }

        UpdateAggro();

        if (IsChasing) Chase();
        else Patrol();
    }

    protected virtual void LateUpdate()
    {
        if (IsDead || animator == null) return;

        // Velocidad horizontal real: sirve con NavMeshAgent y con el movimiento de respaldo
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        animator.SetFloat(AnimSpeed, speed, 0.1f, Time.deltaTime);
    }

    private bool PlayerAvailable => player != null && (playerHealth == null || !playerHealth.IsDead) && player.gameObject.activeInHierarchy;

    private void UpdateAggro()
    {
        if (!PlayerAvailable)
        {
            if (IsChasing) StopChasing();
            return;
        }

        float dist = FlatDistance(transform.position, player.position);
        if (!IsChasing)
        {
            if (dist <= detectionRadius) IsChasing = true;
        }
        else if (dist > loseAggroRadius || (leashRadius > 0f && FlatDistance(transform.position, Home) > leashRadius))
        {
            StopChasing();
        }
    }

    private void StopChasing()
    {
        IsChasing = false;
        hasPatrolTarget = false;
        waitUntil = Time.time + 0.5f;
    }

    protected virtual void Chase()
    {
        float dist = FlatDistance(transform.position, player.position);

        if (dist > attackRange * 0.9f) MoveTo(player.position, chaseSpeed);
        else Stop();

        FaceTowards(player.position);

        if (dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            Attack();
        }
    }

    protected virtual void Patrol()
    {
        if (Time.time < waitUntil)
        {
            Stop();
            return;
        }

        if (!hasPatrolTarget)
        {
            patrolTarget = NextPatrolPoint();
            hasPatrolTarget = true;
            patrolGiveUpAt = Time.time + 10f; // si no llega (obstáculo), elige otro
        }

        if (FlatDistance(transform.position, patrolTarget) <= 0.6f || Time.time > patrolGiveUpAt)
        {
            hasPatrolTarget = false;
            waitUntil = Time.time + UnityEngine.Random.Range(patrolWait.x, patrolWait.y);
            Stop();
            return;
        }

        MoveTo(patrolTarget, patrolSpeed);
    }

    protected virtual Vector3 NextPatrolPoint()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            for (int tries = 0; tries < patrolPoints.Length; tries++)
            {
                Transform t = patrolPoints[patrolIndex % patrolPoints.Length];
                patrolIndex++;
                if (t != null) return t.position;
            }
        }

        Vector2 r = UnityEngine.Random.insideUnitCircle * patrolRadius;
        Vector3 p = Home + new Vector3(r.x, 0f, r.y);
        if (UsingAgent && NavMesh.SamplePosition(p, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas)) p = hit.position;
        return p;
    }

    /// <summary>Golpe por contacto. Devuelve true si el golpe le quitó vida al jugador.</summary>
    protected virtual bool Attack()
    {
        if (animator != null) animator.SetTrigger(AnimAttack);
        PlayClip(attackClip);

        if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvulnerable) return false;
        playerHealth.TakeDamage(attackDamage);
        OnAttackLanded();
        return true;
    }

    /// <summary>Después de quitarle vida al jugador (el espectro lo empuja).</summary>
    protected virtual void OnAttackLanded() { }

    // ---------------------------------------------------------------- Movimiento

    protected void MoveTo(Vector3 target, float speed)
    {
        if (UsingAgent)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.SetDestination(target);
            return;
        }

        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float step = speed * Time.deltaTime;
        transform.position += Vector3.ClampMagnitude(dir, step);
        FaceTowards(target);
    }

    protected void Stop()
    {
        if (UsingAgent)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    protected void FaceTowards(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0025f) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void Displace(Vector3 offset)
    {
        if (UsingAgent) agent.Move(offset);
        else transform.position += offset;
    }

    protected static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------- Terreno

    /// <summary>Altura deseada sobre el piso encontrado; el espectro la sobreescribe para flotar.</summary>
    protected virtual float HeightAboveGround(float groundY)
    {
        float pivotToBottom = enemyCollider != null && enemyCollider.enabled ? transform.position.y - enemyCollider.bounds.min.y : 0f;
        return groundY + pivotToBottom + groundOffset;
    }

    protected void SnapToGround(bool immediate)
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 4f, Vector3.down, 30f, groundLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Ignorar los colliders propios, al jugador y a otros enemigos
            if (hit.collider.transform.root == transform.root || hit.collider.CompareTag("Player") || hit.collider.CompareTag("Enemy"))
                continue;

            float targetY = HeightAboveGround(hit.point.y);
            float y = immediate ? targetY : Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * terrainFollowSpeed);
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            return;
        }
    }

    // ---------------------------------------------------------------- Daño

    /// <summary>IDamageable: pala, piedra y cualquier fuente con origen conocido.</summary>
    public virtual void TakeDamage(int amount, Vector3 origin)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth -= amount;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());

        // Un golpe siempre lo engancha con el jugador, aunque lo hayan atacado de lejos con una piedra
        if (PlayerAvailable) IsChasing = true;

        if (CurrentHealth <= 0)
        {
            Die();
            return;
        }

        PlayClip(hitClip);
        if (animator != null) animator.SetTrigger(AnimHit);

        stunnedUntil = Time.time + hitStun;
        Vector3 away = transform.position - origin;
        away.y = 0f;
        if (hitPushback > 0f && away.sqrMagnitude > 0.0001f) Displace(away.normalized * hitPushback);
    }

    private IEnumerator FlashRoutine()
    {
        SetFlash(hitFlashColor);
        yield return new WaitForSeconds(flashDuration);
        ClearFlash();
        flashRoutine = null;
    }

    private void SetFlash(Color c)
    {
        flashBlock.Clear();
        flashBlock.SetColor("_BaseColor", c);
        flashBlock.SetColor("_Color", c);
        foreach (Renderer r in renderers)
            if (r != null && !(r is ParticleSystemRenderer)) r.SetPropertyBlock(flashBlock);
    }

    private void ClearFlash()
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers)
            if (r != null && !(r is ParticleSystemRenderer)) r.SetPropertyBlock(null);
    }

    protected virtual void Die()
    {
        IsDead = true;
        IsChasing = false;
        CurrentHealth = 0;

        PlayClip(deathClip);
        if (deathVFX != null) Destroy(Instantiate(deathVFX, transform.position, Quaternion.identity), 2.5f);

        if (dropChance > 0f && UnityEngine.Random.value <= dropChance && ObjectPooler.Instance != null &&
            ObjectPooler.Instance.poolDictionary.ContainsKey(healthPickupTag))
            ObjectPooler.Instance.SpawnFromPool(healthPickupTag, transform.position, Quaternion.identity);

        if (enemyCollider != null) enemyCollider.enabled = false;
        if (UsingAgent)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (animator != null)
        {
            animator.ResetTrigger(AnimAttack);
            animator.ResetTrigger(AnimHit);
            animator.SetFloat(AnimSpeed, 0f);
            animator.SetBool(AnimDead, true);
        }

        OnEnemyDied?.Invoke(this);

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DeathRoutine());
    }

    /// <summary>Lo que se ve al morir antes de desactivarse; el espectro se desvanece en lugar de animar.</summary>
    protected virtual IEnumerator DeathRoutine()
    {
        if (animator != null && deathAnimDuration > 0f) yield return new WaitForSeconds(deathAnimDuration);
        deathRoutine = null;
        gameObject.SetActive(false);
    }

    protected void PlayClip(AudioClip clip)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 home = Application.isPlaying ? Home : transform.position;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
        if (patrolPoints == null || patrolPoints.Length == 0) Gizmos.DrawWireSphere(home, patrolRadius);
        else foreach (Transform t in patrolPoints) if (t != null) Gizmos.DrawLine(home, t.position);
    }
}
