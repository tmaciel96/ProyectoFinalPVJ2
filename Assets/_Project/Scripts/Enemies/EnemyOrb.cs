// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Controlador integral de IA para los 3 enemigos no humanoides (Fuegos fatuos / Orbes espectrales:
// Ígneo Rápido, Sombrío Tanque y Vórtice Tirador). Incluye detección por radio con desenganche,
// persecución con Rigidbody cinemático y stopping distance, adaptación al relieve del terreno mediante
// raycasting vertical (snapToGround), ataques cuerpo a cuerpo y a distancia, y drop de viales coleccionables.
// =================================================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class EnemyOrb : MonoBehaviour, IDamageable
{
    public enum OrbType
    {
        IgneoFast,      // Orbe Ígneo: rápido, menor vida, persecución rápida
        SombrioTank,    // Orbe Sombrío: pesado, lento, mucha vida, gran impacto
        VorticeShooter  // Orbe Vórtice: ataca a distancia
    }

    [Header("Tipo de Orbe")]
    public OrbType orbType = OrbType.IgneoFast;

    [Header("Estadísticas")]
    public int maxHealth = 50;
    public int currentHealth;
    public int attackDamage = 15;
    public float attackCooldown = 1.2f;
    private float lastAttackTime;
    public bool isDead = false;

    [Header("Apoyo en el Piso / Terreno")]
    [Tooltip("Si está activo, apoya la base de la cápsula directamente sobre el suelo")]
    public bool snapToGround = true;
    [Tooltip("Ajuste fino de altura respecto al piso (+ sube, - baja)")]
    public float groundOffset = 0f;
    [Tooltip("Altura si prefieres que flote como fuego fatuo (solo si snapToGround está desactivado)")]
    public float hoverHeight = 0.9f;
    public float terrainFollowSpeed = 15f;
    public LayerMask groundLayers = ~0;

    [Header("Área Delimitada y Persecución")]
    [Tooltip("Distancia a la que detecta al jugador y empieza a perseguirlo")]
    public float detectionRadius = 14f;
    [Tooltip("Distancia a la que pierde de vista al jugador y regresa")]
    public float loseAggroRadius = 20f;
    [Tooltip("Distancia mínima al jugador donde frena para no empujarlo")]
    public float stoppingDistance = 1.6f;
    private Vector3 originSpawnPosition;
    private bool isChasing = false;

    [Header("Ataque a Distancia (Vórtice)")]
    public Transform shootPoint;
    public string enemyBulletPoolTag = "EnemyBullet";
    public float shootingRange = 10f;

    [Header("Recompensas / Pickups")]
    [Range(0f, 1f)] public float dropChance = 0.5f;
    public string healthPickupTag = "PickupHealth";
    public string timePickupTag = "PickupTime";

    [Header("Efectos Visuales")]
    public GameObject deathVFX;
    public Color hitFlashColor = Color.white;
    public float flashDuration = 0.12f;

    [Header("Animación (Skeleton.controller / Zombie.controller)")]
    [Tooltip("Si queda vacío se busca en los hijos; los orbes sin Animator siguen funcionando igual")]
    public Animator animator;
    [Tooltip("Segundos que se deja ver la animación de muerte antes de volver al pool")]
    public float deathAnimDuration = 2f;

    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimHit = Animator.StringToHash("Hit");
    private static readonly int AnimDead = Animator.StringToHash("Dead");

    private Vector3 lastPosition;
    private Coroutine deathCoroutine;

    private NavMeshAgent agent;
    private Transform playerTarget;
    private PlayerHealth playerHealth;
    private float currentSpeed = 4f;

    private Renderer[] renderers;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    private Coroutine flashCoroutine;
    private Collider enemyCollider;

    void Awake()
    {
        // 1. Manejo seguro de NavMeshAgent para evitar "Failed to create agent" si no hay NavMesh horneado
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                agent = null;
            }
        }

        // 2. Rigidbody Cinemático para colisiones con proyectiles
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 3. Collider como Trigger para recibir balas y evitar empujar físicamente al jugador
        enemyCollider = GetComponent<Collider>();
        if (enemyCollider == null) enemyCollider = GetComponentInChildren<Collider>();
        if (enemyCollider == null) enemyCollider = gameObject.AddComponent<CapsuleCollider>();
        enemyCollider.isTrigger = true;

        if (!CompareTag("Enemy"))
        {
            gameObject.tag = "Enemy";
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false; // lo mueve el NavMeshAgent

        CacheColors();
    }

    private void CacheColors()
    {
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            if (rend is ParticleSystemRenderer) continue;

            if (rend != null && rend.materials != null)
            {
                Color[] cols = new Color[rend.materials.Length];
                for (int i = 0; i < rend.materials.Length; i++)
                {
                    if (rend.materials[i].HasProperty("_Color"))
                        cols[i] = rend.materials[i].color;
                    else if (rend.materials[i].HasProperty("_BaseColor"))
                        cols[i] = rend.materials[i].GetColor("_BaseColor");
                    else
                        cols[i] = Color.white;
                }
                originalColors[rend] = cols;
            }
        }
    }

    void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
        lastAttackTime = 0f;
        originSpawnPosition = transform.position;
        lastPosition = transform.position;
        isChasing = false;

        // Volver del pool "vivo": collider activo y Animator en locomoción
        if (enemyCollider != null) enemyCollider.enabled = true;
        if (animator != null && animator.isInitialized)
        {
            animator.SetBool(AnimDead, false);
            animator.SetFloat(AnimSpeed, 0f);
        }

        if (agent != null)
        {
            try
            {
                if (!agent.isOnNavMesh)
                {
                    agent.enabled = false;
                }
            }
            catch
            {
                agent.enabled = false;
                agent = null;
            }
        }

        FindPlayer();
        ConfigureByType();

        // Ajustar altura de inmediato en el primer fotograma al nacer
        SnapImmediatelyToTerrain();
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.gameObject;
        }

        if (player != null)
        {
            playerTarget = player.transform;
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void ConfigureByType()
    {
        switch (orbType)
        {
            case OrbType.IgneoFast:
                currentSpeed = 4.8f;
                maxHealth = 40;
                attackDamage = 10;
                stoppingDistance = 1.5f;
                break;

            case OrbType.SombrioTank:
                currentSpeed = 2.4f;
                maxHealth = 90;
                attackDamage = 25;
                stoppingDistance = 1.8f;
                break;

            case OrbType.VorticeShooter:
                currentSpeed = 3.2f;
                maxHealth = 45;
                attackDamage = 15;
                stoppingDistance = shootingRange - 2.5f;
                break;
        }

        if (agent != null && agent.enabled)
        {
            try
            {
                agent.speed = currentSpeed;
                agent.stoppingDistance = stoppingDistance;
            }
            catch
            {
                agent.enabled = false;
                agent = null;
            }
        }

        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead) return;

        // Mantener la cápsula pegada al suelo en desniveles
        AdjustHeightToTerrain();

        if (playerTarget == null)
        {
            FindPlayer();
            if (playerTarget == null) return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (!isChasing)
        {
            if (distanceToPlayer <= detectionRadius)
            {
                isChasing = true;
            }
        }
        else
        {
            if (distanceToPlayer > loseAggroRadius)
            {
                isChasing = false;
            }
        }

        if (isChasing)
        {
            Vector3 lookTarget = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
            if ((lookTarget - transform.position).sqrMagnitude > 0.05f)
            {
                transform.LookAt(lookTarget);
            }

            if (distanceToPlayer > stoppingDistance)
            {
                MoveTowards(playerTarget.position);
            }

            if (orbType == OrbType.VorticeShooter)
            {
                if (distanceToPlayer <= shootingRange && Time.time >= lastAttackTime + attackCooldown)
                {
                    PerformRangedAttack();
                }
            }
            else
            {
                if (distanceToPlayer <= stoppingDistance + 0.5f && Time.time >= lastAttackTime + attackCooldown)
                {
                    PerformMeleeAttack();
                }
            }
        }
        else
        {
            float distToSpawn = Vector3.Distance(transform.position, originSpawnPosition);
            if (distToSpawn > 1.5f)
            {
                MoveTowards(originSpawnPosition);
            }
        }
    }

    void LateUpdate()
    {
        if (isDead || animator == null) return;

        // Velocidad horizontal real (sirve tanto con NavMeshAgent como con el movimiento de respaldo)
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        lastPosition = transform.position;

        animator.SetFloat(AnimSpeed, speed, 0.1f, Time.deltaTime);
    }

    private void MoveTowards(Vector3 targetPos)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            try
            {
                agent.SetDestination(targetPos);
                return;
            }
            catch
            {
                agent.enabled = false;
                agent = null;
            }
        }

        Vector3 moveDir = (targetPos - transform.position);
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude > 0.01f)
        {
            transform.position += moveDir.normalized * (currentSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Raycast profundo que ignora los colliders propios para detectar el suelo real debajo de la cápsula.
    /// </summary>
    private void AdjustHeightToTerrain()
    {
        // Disparar rayo vertical hacia abajo desde 4 metros arriba
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 4f, Vector3.down, 30f, groundLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Ignorar los propios colliders del enemigo, sus hijos y el jugador
            if (hit.collider.transform.root == transform.root || 
                hit.collider.CompareTag("Player") || 
                hit.collider.CompareTag("Enemy"))
            {
                continue;
            }

            // Encontró el terreno real.
            // Si snapToGround está activo, la base del collider se apoya en el suelo (hit.point.y)
            float targetY = snapToGround ? hit.point.y + PivotToColliderBottom() + groundOffset : hit.point.y + hoverHeight;
            float smoothY = Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * terrainFollowSpeed);
            transform.position = new Vector3(transform.position.x, smoothY, transform.position.z);
            break;
        }
    }

    private void SnapImmediatelyToTerrain()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 4f, Vector3.down, 30f, groundLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.transform.root == transform.root || 
                hit.collider.CompareTag("Player") || 
                hit.collider.CompareTag("Enemy"))
            {
                continue;
            }

            float targetY = snapToGround ? hit.point.y + PivotToColliderBottom() + groundOffset : hit.point.y + hoverHeight;
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            break;
        }
    }

    /// <summary>
    /// Distancia del pivote a la base del collider. En los orbes el pivote está en el centro (= media altura);
    /// en esqueleto y zombi está en los pies (= 0), así quedan parados en el piso y no flotando.
    /// </summary>
    private float PivotToColliderBottom()
    {
        if (enemyCollider == null) return 1f;
        return transform.position.y - enemyCollider.bounds.min.y;
    }

    private void PerformMeleeAttack()
    {
        lastAttackTime = Time.time;
        if (animator != null) animator.SetTrigger(AnimAttack);
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }

    private void PerformRangedAttack()
    {
        lastAttackTime = Time.time;
        if (animator != null) animator.SetTrigger(AnimAttack);
        Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + transform.forward * 1f;
        Vector3 targetChest = playerTarget != null ? playerTarget.position + Vector3.up * 1f : transform.position + transform.forward * 5f;
        Quaternion spawnRot = Quaternion.LookRotation((targetChest - spawnPos).normalized);

        if (ObjectPooler.Instance != null && ObjectPooler.Instance.poolDictionary.ContainsKey(enemyBulletPoolTag))
        {
            ObjectPooler.Instance.SpawnFromPool(enemyBulletPoolTag, spawnPos, spawnRot);
        }
    }

    /// <summary>IDamageable: pala, piedra y cualquier fuente con origen conocido.</summary>
    public void TakeDamage(int amount, Vector3 origin)
    {
        // El origen queda disponible para el retroceso del espectro (M4-3)
        TakeDamage(amount);
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"<color=yellow>¡Orbe ({orbType}) recibió {amount} de daño! Vida restante: {currentHealth}/{maxHealth}</color>");

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(HitFlashRoutine());

        if (currentHealth <= 0)
        {
            isDead = true;
            Die();
        }
        else if (animator != null)
        {
            animator.SetTrigger(AnimHit);
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        foreach (var rend in renderers)
        {
            if (rend == null || rend is ParticleSystemRenderer) continue;
            foreach (var mat in rend.materials)
            {
                if (mat.HasProperty("_Color")) mat.color = hitFlashColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hitFlashColor);
            }
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (var rend in renderers)
        {
            if (rend == null || rend is ParticleSystemRenderer || !originalColors.ContainsKey(rend)) continue;
            Color[] cols = originalColors[rend];
            for (int i = 0; i < rend.materials.Length; i++)
            {
                if (i < cols.Length)
                {
                    if (rend.materials[i].HasProperty("_Color")) rend.materials[i].color = cols[i];
                    if (rend.materials[i].HasProperty("_BaseColor")) rend.materials[i].SetColor("_BaseColor", cols[i]);
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        PooledProjectile bullet = other.GetComponent<PooledProjectile>();
        if (bullet == null) bullet = other.GetComponentInParent<PooledProjectile>();
        if (bullet != null && bullet.isPlayerProjectile)
        {
            TakeDamage(bullet.damage);
            bullet.Deactivate();
        }
    }

    private void Die()
    {
        Debug.Log($"<color=green>¡Orbe {orbType} eliminado con éxito!</color>");

        if (deathVFX != null)
        {
            GameObject fx = Instantiate(deathVFX, transform.position, Quaternion.identity);
            Destroy(fx, 2.5f);
        }

        if (UnityEngine.Random.value <= dropChance && ObjectPooler.Instance != null)
        {
            string chosenTag = UnityEngine.Random.value > 0.5f ? healthPickupTag : timePickupTag;
            if (ObjectPooler.Instance.poolDictionary.ContainsKey(chosenTag))
            {
                ObjectPooler.Instance.SpawnFromPool(chosenTag, transform.position, Quaternion.identity);
            }
        }

        // Sin Animator (orbes del TP1) se desactiva en el acto, como antes
        if (animator == null || deathAnimDuration <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        // Con Animator: frenar, dejar de recibir golpes y mostrar la muerte antes de volver al pool
        animator.ResetTrigger(AnimAttack);
        animator.ResetTrigger(AnimHit);
        animator.SetFloat(AnimSpeed, 0f);
        animator.SetBool(AnimDead, true);

        if (enemyCollider != null) enemyCollider.enabled = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (deathCoroutine != null) StopCoroutine(deathCoroutine);
        deathCoroutine = StartCoroutine(DeactivateAfterDeath());
    }

    private IEnumerator DeactivateAfterDeath()
    {
        yield return new WaitForSeconds(deathAnimDuration);
        deathCoroutine = null;
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseAggroRadius);
    }
}
