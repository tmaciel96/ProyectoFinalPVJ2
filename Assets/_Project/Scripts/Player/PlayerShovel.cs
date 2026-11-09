// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// M1-3 · Golpe con la pala: ataque frontal de corto alcance con cooldown.
// Clic izquierdo, J o X del gamepad (acción Attack del PlayerInputReader) inicia el golpe; tras hitDelay (el momento en que la pala baja en la
// animación) se revisa una esfera delante del sepulturero y se daña a cada IDamageable una vez.
//
// Cuando exista la capa "Enemy" (M5-3), asignarla en hitLayers para no revisar todo el escenario.
// =================================================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShovel : MonoBehaviour
{
    [Header("Daño")]
    public int damage = 35;
    [Tooltip("Segundos mínimos entre golpes")]
    public float cooldown = 0.6f;
    [Tooltip("Demora entre el clic y el impacto, para que coincida con la animación")]
    public float hitDelay = 0.25f;

    [Header("Alcance")]
    [Tooltip("Distancia hacia adelante del centro de la zona de golpe")]
    public float reach = 1.2f;
    [Tooltip("Radio de la zona de golpe")]
    public float radius = 1.0f;
    [Tooltip("Altura del centro de la zona de golpe respecto de los pies")]
    public float height = 1.0f;
    [Tooltip("Capas que puede golpear (cuando exista, dejar solo 'Enemy')")]
    public LayerMask hitLayers = ~0;

    [Header("Feedback (opcional)")]
    public AudioSource audioSource;
    public AudioClip swingClip;
    public AudioClip hitClip;

    public bool IsSwinging { get; private set; }

    private float nextSwingTime;
    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private PlayerInputReader input;
    private readonly Collider[] overlapBuffer = new Collider[24];
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerHealth = GetComponent<PlayerHealth>();
        input = PlayerController.EnsureInput(gameObject);
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerRespawned += ResetState;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerRespawned -= ResetState;
        ResetState();
    }

    void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (DialogueSystem.BlocksGameplay) return; // el clic que avanza un diálogo no es un palazo
        if (Time.time < nextSwingTime) return;

        if (input != null && input.AttackPressed)
        {
            bool mouse = input.AttackFromPointer;
            nextSwingTime = Time.time + cooldown;
            // Con el mouse gira hacia el puntero antes del golpe; con J pega hacia donde ya mira
            if (mouse && playerController != null) playerController.FaceAimForAction();
            StartCoroutine(SwingRoutine());
        }
    }

    private IEnumerator SwingRoutine()
    {
        IsSwinging = true;
        if (playerController != null) playerController.PlayAttackAnimation();
        PlayClip(swingClip);

        if (hitDelay > 0f) yield return new WaitForSeconds(hitDelay);

        if (playerHealth == null || !playerHealth.IsDead)
            ApplyHit();

        IsSwinging = false;
    }

    private void ApplyHit()
    {
        alreadyHit.Clear();
        Vector3 center = HitCenter();

        // Collide: los enemigos usan colliders trigger para recibir proyectiles
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, hitLayers, QueryTriggerInteraction.Collide);
        bool hitSomething = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col.transform.root == transform.root) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || alreadyHit.Contains(target)) continue;

            alreadyHit.Add(target);
            target.TakeDamage(damage, transform.position);
            hitSomething = true;
        }

        if (hitSomething) PlayClip(hitClip);
    }

    private void ResetState()
    {
        StopAllCoroutines();
        IsSwinging = false;
        nextSwingTime = 0f;
    }

    private Vector3 HitCenter()
    {
        return transform.position + transform.forward * reach + Vector3.up * height;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(HitCenter(), radius);
    }
}
