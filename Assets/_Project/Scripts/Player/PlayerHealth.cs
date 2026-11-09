// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Basado en el PlayerHealth del TP1 (The Pumpkin Ritual, Tamara Maciel).
//
// Vida y reaparición del sepulturero:
//  - M1-8: 100 puntos de vida (decisión del grupo, en lugar de los 3 corazones del GDD). Cada enemigo
//    quita lo que manda en TakeDamage, así el espectro puede pegar más fuerte que el zombi.
//    OnHealthChanged(actual, máximo) avisa al HUD.
//  - M1-9: después de cada golpe hay invulnerabilityTime segundos sin recibir daño, con el modelo
//    parpadeando.
//  - M1-10: al morir reaparece en el checkpoint del sector actual (CheckpointManager) con la vida
//    llena. Si no hay checkpoint, usa el objeto con tag SpawnPoint como antes.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("Invulnerabilidad tras un golpe")]
    [Tooltip("Segundos sin recibir daño después de cada golpe")]
    public float invulnerabilityTime = 1.2f;
    [Tooltip("Segundos entre parpadeos del modelo mientras es invulnerable")]
    public float blinkInterval = 0.1f;

    [Header("Punto de reaparición de respaldo")]
    [Tooltip("Se usa solo si no hay ningún checkpoint activado. Si queda vacío se busca el tag SpawnPoint.")]
    public Transform spawnPoint;

    [Header("Animación")]
    [Tooltip("Animator del modelo (Player.controller). Si queda vacío se busca en los hijos.")]
    public Animator animator;
    [Tooltip("Segundos que se deja ver la animación de muerte antes de reaparecer")]
    public float deathAnimDuration = 2f;

    private static readonly int AnimHit = Animator.StringToHash("Hit");
    private static readonly int AnimDead = Animator.StringToHash("Dead");

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    /// <summary>True desde que la vida llega a 0 hasta que reaparece.</summary>
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    // Eventos
    /// <summary>(vida actual, máximo)</summary>
    public static event Action<int, int> OnHealthChanged;
    public static event Action OnPlayerDamaged;
    public static event Action OnPlayerRespawned;

    private CharacterController characterController;
    private Renderer[] renderers;
    private float invulnerableUntil;
    private Coroutine deathCoroutine;
    private Coroutine blinkCoroutine;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        currentHealth = maxHealth;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Start()
    {
        if (spawnPoint == null)
        {
            GameObject sp = GameObject.FindGameObjectWithTag("SpawnPoint");
            if (sp != null) spawnPoint = sp.transform;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>Golpe de un enemigo o proyectil: resta amount puntos de vida.</summary>
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;
        if (IsInvulnerable) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnPlayerDamaged?.Invoke();

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (animator != null) animator.SetTrigger(AnimHit);

        invulnerableUntil = Time.time + invulnerabilityTime;
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    /// <summary>Recupera vida sin pasar del máximo.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ---------------------------------------------------------------- M1-9: parpadeo

    private IEnumerator BlinkRoutine()
    {
        bool visible = true;
        while (IsInvulnerable && !IsDead)
        {
            visible = !visible;
            SetVisible(visible);
            yield return new WaitForSeconds(blinkInterval);
        }
        SetVisible(true);
        blinkCoroutine = null;
    }

    private void StopBlink()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers)
        {
            // Solo mallas: luces de partículas o efectos hijos no se tocan
            if (r != null && (r is MeshRenderer || r is SkinnedMeshRenderer)) r.enabled = visible;
        }
    }

    // ---------------------------------------------------------------- Muerte y reaparición

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        StopBlink();
        invulnerableUntil = 0f;

        if (animator != null)
        {
            animator.ResetTrigger(AnimHit);
            animator.SetBool(AnimDead, true);
        }

        if (deathCoroutine != null) StopCoroutine(deathCoroutine);
        deathCoroutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Sin Animator no hay nada que mostrar: reaparece en el acto
        if (animator != null && deathAnimDuration > 0f)
            yield return new WaitForSeconds(deathAnimDuration);

        deathCoroutine = null;

        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied();
        else Respawn();
    }

    public void Respawn()
    {
        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }

        IsDead = false;
        if (animator != null) animator.SetBool(AnimDead, false);

        currentHealth = maxHealth;
        StopBlink();
        // Un instante de gracia al aparecer, para no recibir un golpe en el mismo frame
        invulnerableUntil = Time.time + invulnerabilityTime;
        blinkCoroutine = StartCoroutine(BlinkRoutine());

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // M1-10: el checkpoint del sector actual; si no hay, el SpawnPoint de la escena
        Transform target = CheckpointManager.Current != null ? CheckpointManager.Current : spawnPoint;
        Vector3 targetPos = target != null ? target.position : Vector3.zero;
        Quaternion targetRot = target != null ? target.rotation : Quaternion.identity;

        if (characterController != null) characterController.enabled = false;
        transform.SetPositionAndRotation(targetPos, targetRot);
        if (characterController != null) characterController.enabled = true;

        // PlayerController, PlayerShovel, StoneInventory y la cámara se reinician con este evento
        OnPlayerRespawned?.Invoke();
    }
}
