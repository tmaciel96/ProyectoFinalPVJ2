// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Tumba Maldita: enemigo fijo a distancia. Una lápida alta con una luz fantasmal que flota encima,
// detecta al sepulturero, avisa con un pulso de luz y le dispara un proyectil del pool "EnemyBullet".
//
// Se apaga (deja de disparar para siempre, o hasta Rekindle) de alguna de estas formas, según extinguishMode:
//   - Interact: acercarse y presionar E/Espacio, como las velas del vampiro (misión 3).
//   - Shovel:   golpear la luz con la pala o la piedra (wispHealth golpes).
//   - Both:     cualquiera de las dos.
//   - ScriptOnly: solo desde código, por ejemplo cuando la misión del vampiro deja el sector en penumbra.
// Cada tumba apagada avisa con OnTombExtinguished, para que la misión cuente cuántas quedan.
//
// Cuando exista IInteractable / PlayerInteractor (M1-7, M2-2) el modo Interact debería pasar por ahí.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CursedTomb : MonoBehaviour, IDamageable
{
    public enum ExtinguishMode { Interact, Shovel, Both, ScriptOnly }

    [Header("Estado")]
    [Tooltip("Si arranca apagada, se enciende con Rekindle() (por ejemplo al entrar al sector)")]
    public bool startLit = true;
    public ExtinguishMode extinguishMode = ExtinguishMode.Interact;

    [Header("Luz flotante")]
    [Tooltip("Objeto que flota encima de la tumba (la luz, la partícula y el punto de disparo cuelgan de acá)")]
    public Transform wisp;
    public Light wispLight;
    public ParticleSystem wispParticles;
    public float bobAmplitude = 0.15f;
    public float bobSpeed = 2f;
    public Color idleColor = new Color(0.45f, 1f, 0.75f);
    public Color chargeColor = new Color(1f, 0.35f, 0.35f);
    public float idleIntensity = 2f;
    public float chargeIntensity = 6f;

    [Header("Ataque a distancia")]
    public float detectionRange = 12f;
    public float fireCooldown = 2.2f;
    [Tooltip("Segundos de aviso (la luz se pone roja y brilla) antes de disparar")]
    public float chargeTime = 0.6f;
    [Tooltip("Exigir línea de visión libre hasta el jugador")]
    public bool requireLineOfSight = true;
    public LayerMask sightBlockers = ~0;
    public string projectilePoolTag = "EnemyBullet";
    [Tooltip("Respaldo si no hay ObjectPooler o el tag no existe (por ejemplo en una escena de prueba)")]
    public GameObject projectileFallbackPrefab;

    [Header("Apagado")]
    [Tooltip("Distancia para apagarla con E/Espacio (modo Interact)")]
    public float interactRadius = 2.2f;
    [Tooltip("Golpes de pala o piedra para apagarla (modo Shovel)")]
    public int wispHealth = 2;
    public float extinguishFadeTime = 0.8f;
    public AudioSource audioSource;
    public AudioClip fireClip;
    public AudioClip extinguishClip;

    /// <summary>Avisa cada vez que una tumba se apaga (la misión del vampiro o del niño puede contarlas).</summary>
    public static event Action<CursedTomb> OnTombExtinguished;

    public bool IsLit { get; private set; }

    private Transform player;
    private PlayerHealth playerHealth;
    private Vector3 wispBaseLocalPos;
    private float nextFireTime;
    private int currentWispHealth;
    private Coroutine attackRoutine;
    private Coroutine fadeRoutine;

    void Awake()
    {
        if (wisp != null) wispBaseLocalPos = wisp.localPosition;
        if (wispLight == null && wisp != null) wispLight = wisp.GetComponentInChildren<Light>();
        if (wispParticles == null && wisp != null) wispParticles = wisp.GetComponentInChildren<ParticleSystem>();

        // Igual que los orbes: el tag Enemy hace que los proyectiles enemigos la ignoren
        if (!CompareTag("Enemy")) gameObject.tag = "Enemy";
    }

    void OnEnable()
    {
        if (startLit) Rekindle();
        else SetLitVisuals(false, instant: true);
    }

    void Update()
    {
        AnimateWisp();
        if (!IsLit) return;

        if (player == null && !FindPlayer()) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (CanInteract() && dist <= interactRadius && WasInteractPressed())
        {
            Extinguish();
            return;
        }

        if (attackRoutine == null && Time.time >= nextFireTime && dist <= detectionRange && HasLineOfSight())
            attackRoutine = StartCoroutine(AttackRoutine());
    }

    // ---------------------------------------------------------------- API pública

    /// <summary>Apaga la tumba: deja de disparar, se apaga la luz y avisa con OnTombExtinguished.</summary>
    public void Extinguish()
    {
        if (!IsLit) return;
        IsLit = false;

        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
        PlayClip(extinguishClip);
        SetLitVisuals(false, instant: false);

        Debug.Log($"<color=cyan>Tumba maldita apagada: {name}</color>");
        OnTombExtinguished?.Invoke(this);
    }

    /// <summary>Vuelve a encenderla (por ejemplo al reiniciar un sector o si la misión lo pide).</summary>
    public void Rekindle()
    {
        IsLit = true;
        currentWispHealth = Mathf.Max(1, wispHealth);
        nextFireTime = Time.time + fireCooldown * 0.5f;
        SetLitVisuals(true, instant: true);
    }

    /// <summary>IDamageable: la pala y la piedra apagan la luz si el modo lo permite.</summary>
    public void TakeDamage(int amount, Vector3 origin)
    {
        if (!IsLit) return;
        if (extinguishMode != ExtinguishMode.Shovel && extinguishMode != ExtinguishMode.Both) return;

        currentWispHealth--;
        if (currentWispHealth <= 0) Extinguish();
        else if (wispLight != null) StartCoroutine(FlickerRoutine());
    }

    // ---------------------------------------------------------------- Ataque

    private IEnumerator AttackRoutine()
    {
        // Aviso: la luz se pone roja y sube de intensidad
        float t = 0f;
        while (t < chargeTime)
        {
            t += Time.deltaTime;
            float k = chargeTime > 0f ? t / chargeTime : 1f;
            SetLightColor(Color.Lerp(idleColor, chargeColor, k), Mathf.Lerp(idleIntensity, chargeIntensity, k));
            yield return null;
        }

        if (IsLit && player != null) Fire();

        SetLightColor(idleColor, idleIntensity);
        nextFireTime = Time.time + fireCooldown;
        attackRoutine = null;
    }

    private void Fire()
    {
        Vector3 spawnPos = wisp != null ? wisp.position : transform.position + Vector3.up * 2f;
        Vector3 target = player.position + Vector3.up * 1f;
        Quaternion rot = Quaternion.LookRotation((target - spawnPos).normalized);

        bool pooled = ObjectPooler.Instance != null
                      && ObjectPooler.Instance.poolDictionary != null
                      && ObjectPooler.Instance.poolDictionary.ContainsKey(projectilePoolTag);

        if (pooled) ObjectPooler.Instance.SpawnFromPool(projectilePoolTag, spawnPos, rot);
        else if (projectileFallbackPrefab != null) Instantiate(projectileFallbackPrefab, spawnPos, rot);
        else Debug.LogWarning($"CursedTomb '{name}': no hay pool '{projectilePoolTag}' ni prefab de respaldo.");

        PlayClip(fireClip);
    }

    private bool HasLineOfSight()
    {
        if (!requireLineOfSight) return true;

        Vector3 from = wisp != null ? wisp.position : transform.position + Vector3.up * 2f;
        Vector3 to = player.position + Vector3.up * 1f;
        Vector3 dir = to - from;

        RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, sightBlockers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            Transform root = hit.collider.transform.root;
            if (root == transform.root || root == player.root) continue;
            return false; // algo del escenario tapa la vista
        }
        return true;
    }

    // ---------------------------------------------------------------- Apagado por interacción

    private bool CanInteract()
    {
        return extinguishMode == ExtinguishMode.Interact || extinguishMode == ExtinguishMode.Both;
    }

    private bool WasInteractPressed()
    {
        var kb = Keyboard.current;
        return kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
    }

    // ---------------------------------------------------------------- Visuales

    private void AnimateWisp()
    {
        if (wisp == null || !IsLit) return;
        wisp.localPosition = wispBaseLocalPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
    }

    private void SetLitVisuals(bool lit, bool instant)
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }

        if (wispParticles != null)
        {
            if (lit) wispParticles.Play(true);
            else wispParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (wispLight == null) return;

        if (lit)
        {
            wispLight.enabled = true;
            SetLightColor(idleColor, idleIntensity);
        }
        else if (instant || extinguishFadeTime <= 0f)
        {
            wispLight.intensity = 0f;
            wispLight.enabled = false;
        }
        else
        {
            fadeRoutine = StartCoroutine(FadeOutRoutine());
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        float start = wispLight.intensity;
        float t = 0f;
        while (t < extinguishFadeTime)
        {
            t += Time.deltaTime;
            wispLight.intensity = Mathf.Lerp(start, 0f, t / extinguishFadeTime);
            yield return null;
        }
        wispLight.enabled = false;
        fadeRoutine = null;
    }

    private IEnumerator FlickerRoutine()
    {
        for (int i = 0; i < 3 && IsLit; i++)
        {
            wispLight.intensity = idleIntensity * 0.2f;
            yield return new WaitForSeconds(0.05f);
            wispLight.intensity = idleIntensity;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void SetLightColor(Color c, float intensity)
    {
        if (wispLight == null) return;
        wispLight.color = c;
        wispLight.intensity = intensity;
    }

    // ---------------------------------------------------------------- Utilidades

    private bool FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) p = pc.gameObject;
        }
        if (p == null) return false;

        player = p.transform;
        playerHealth = p.GetComponent<PlayerHealth>();
        return true;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(0.3f, 1f, 0.7f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
