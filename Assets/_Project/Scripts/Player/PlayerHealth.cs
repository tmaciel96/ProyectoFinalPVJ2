// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Sistema de salud y reaparición del jugador. Controla los puntos de vida (100 HP), tiempo de
// inmunidad temporal contra golpes continuos, feedback visual de daño con destello rojo en los materiales,
// eventos para el HUD de vida y la secuencia de muerte con respawn en el SpawnPoint.
// =================================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Salud")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Punto de Reaparición")]
    public Transform spawnPoint;

    [Header("Feedback de Daño Visual")]
    public Color damageFlashColor = Color.red;
    public float flashDuration = 0.15f;
    public float invulnerabilityDuration = 0.25f;
    private float lastDamageTime = -1f;

    [Header("Animación")]
    [Tooltip("Animator del modelo (Player.controller). Si queda vacío se busca en los hijos.")]
    public Animator animator;
    [Tooltip("Segundos que se deja ver la animación de muerte antes de reaparecer")]
    public float deathAnimDuration = 2f;

    private static readonly int AnimHit = Animator.StringToHash("Hit");
    private static readonly int AnimDead = Animator.StringToHash("Dead");

    /// <summary>True desde que la vida llega a 0 hasta que reaparece.</summary>
    public bool IsDead { get; private set; }
    private Coroutine deathCoroutine;

    private CharacterController characterController;
    private Renderer[] allRenderers;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    private Coroutine flashCoroutine;

    // Eventos
    public static event Action<int, int> OnHealthChanged;
    public static event Action OnPlayerDamaged;
    public static event Action OnPlayerRespawned;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        currentHealth = maxHealth;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheOriginalColors();
    }

    private void CacheOriginalColors()
    {
        allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var rend in allRenderers)
        {
            if (rend != null && rend.materials != null)
            {
                Color[] colors = new Color[rend.materials.Length];
                for (int i = 0; i < rend.materials.Length; i++)
                {
                    if (rend.materials[i].HasProperty("_Color"))
                        colors[i] = rend.materials[i].color;
                    else if (rend.materials[i].HasProperty("_BaseColor"))
                        colors[i] = rend.materials[i].GetColor("_BaseColor");
                    else
                        colors[i] = Color.white;
                }
                originalColors[rend] = colors;
            }
        }
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

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;

        if (Time.time < lastDamageTime + invulnerabilityDuration) return;
        lastDamageTime = Time.time;

        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnPlayerDamaged?.Invoke();

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DamageFlashRoutine());

        Debug.Log($"<color=red>¡Player recibió {amount} de daño! Vida restante: {currentHealth}/{maxHealth}</color>");

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (animator != null)
        {
            animator.SetTrigger(AnimHit);
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        foreach (var rend in allRenderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.materials)
            {
                if (mat.HasProperty("_Color")) mat.color = damageFlashColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", damageFlashColor);
            }
        }

        yield return new WaitForSeconds(flashDuration);

        RestoreOriginalColors();
    }

    private void RestoreOriginalColors()
    {
        foreach (var rend in allRenderers)
        {
            if (rend == null || !originalColors.ContainsKey(rend)) continue;
            Color[] colors = originalColors[rend];
            for (int i = 0; i < rend.materials.Length; i++)
            {
                if (i < colors.Length)
                {
                    if (rend.materials[i].HasProperty("_Color")) rend.materials[i].color = colors[i];
                    if (rend.materials[i].HasProperty("_BaseColor")) rend.materials[i].SetColor("_BaseColor", colors[i]);
                }
            }
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Debug.Log("Player ha muerto. Reapareciendo en el SpawnPoint...");

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
        // Sin Animator no hay nada que mostrar: reaparece en el acto, como antes
        if (animator != null && deathAnimDuration > 0f)
            yield return new WaitForSeconds(deathAnimDuration);

        deathCoroutine = null;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
        else
        {
            Respawn();
        }
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
        lastDamageTime = -1f;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        RestoreOriginalColors();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Vector3 targetPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion targetRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        if (characterController != null)
        {
            characterController.enabled = false;
            transform.position = targetPos;
            transform.rotation = targetRot;
            characterController.enabled = true;
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
        }

        // Restablecer obligatoriamente munición y disparo en PlayerController
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.ResetShootingState();
        }

        // Notificar que el jugador reapareció (la cámara se teletransporta de inmediato)
        OnPlayerRespawned?.Invoke();
    }
}
