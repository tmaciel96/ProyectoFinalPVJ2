// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Basado en el PlayerHealth del TP1 (The Pumpkin Ritual, Tamara Maciel).
//
// Vida y reaparición del sepulturero:
//  - M1-8: 3 corazones enteros. Cada golpe enemigo quita 1 corazón (el valor de daño que mandan los
//    enemigos del TP1 se ignora; si algún enemigo tiene que quitar más, usa TakeHearts).
//    OnHealthChanged(actual, máximo) avisa al HUD.
//  - M1-9: después de cada golpe hay invulnerabilityTime segundos sin recibir daño, con el modelo
//    parpadeando.
//  - M1-10: al morir reaparece en el checkpoint del sector actual (CheckpointManager) con los corazones
//    llenos. Si no hay checkpoint, usa el objeto con tag SpawnPoint como antes.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida (corazones)")]
    public int maxHearts = 3;
    [SerializeField] private int currentHearts;

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

    public int CurrentHearts => currentHearts;
    /// <summary>True desde que los corazones llegan a 0 hasta que reaparece.</summary>
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    // Eventos
    /// <summary>(corazones actuales, máximo)</summary>
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
        currentHearts = maxHearts;
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

        OnHealthChanged?.Invoke(currentHearts, maxHearts);
    }

    /// <summary>
    /// Golpe de un enemigo. Con corazones enteros todos los golpes quitan 1, sin importar amount
    /// (los enemigos y proyectiles del TP1 siguen mandando su daño en HP).
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        TakeHearts(1);
    }

    /// <summary>Quita una cantidad exacta de corazones (por ejemplo un golpe fuerte del espectro).</summary>
    public void TakeHearts(int hearts)
    {
        if (IsDead || hearts <= 0) return;
        if (IsInvulnerable) return;

        currentHearts = Mathf.Max(0, currentHearts - hearts);
        OnHealthChanged?.Invoke(currentHearts, maxHearts);
        OnPlayerDamaged?.Invoke();

        if (currentHearts <= 0)
        {
            Die();
            return;
        }

        if (animator != null) animator.SetTrigger(AnimHit);

        invulnerableUntil = Time.time + invulnerabilityTime;
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    /// <summary>Recupera corazones sin pasar del máximo.</summary>
    public void Heal(int hearts)
    {
        if (IsDead || hearts <= 0) return;
        currentHearts = Mathf.Min(currentHearts + hearts, maxHearts);
        OnHealthChanged?.Invoke(currentHearts, maxHearts);
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

        currentHearts = maxHearts;
        StopBlink();
        // Un instante de gracia al aparecer, para no recibir un golpe en el mismo frame
        invulnerableUntil = Time.time + invulnerabilityTime;
        blinkCoroutine = StartCoroutine(BlinkRoutine());

        OnHealthChanged?.Invoke(currentHearts, maxHearts);

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
