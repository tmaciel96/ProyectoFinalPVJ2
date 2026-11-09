// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Ítem coleccionable interactivo con adaptación al terreno (pociones de vida y bonos de tiempo).
// Detecta la altura del suelo por raycast para descender suavemente, rota y flota sobre su eje,
// e implementa atracción magnética automática cuando el jugador se aproxima a corta distancia.
// =================================================================================================

using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour
{
    public enum PickupType
    {
        HealthPotion,   // Recupera healthAmount puntos de vida
        TimeBonus       // Resta -15 segundos al cronómetro (da más tiempo de vida)
    }

    [Header("Tipo y Beneficio")]
    public PickupType type = PickupType.HealthPotion;
    [Tooltip("Puntos de vida que recupera la poción de vida")]
    public int healthAmount = 25;
    public float timeAmount = 15f;

    [Header("Caída y Altura en el Terreno")]
    [Tooltip("Altura a la que reposa sobre el suelo (a la altura de los pies/rodillas)")]
    public float heightAboveGround = 0.35f;
    [Tooltip("Velocidad con la que cae al suelo al ser soltado")]
    public float fallSpeed = 6f;
    public LayerMask groundLayers = ~0;
    private float targetGroundY;
    private bool hasLanded = false;

    [Header("Efecto Imán hacia el Jugador")]
    [Tooltip("Distancia a la que el ítem es atraído automáticamente hacia el jugador")]
    public float magnetRadius = 3.5f;
    public float magnetSpeed = 9f;

    [Header("Animación de Giro y Flotación")]
    public float rotationSpeed = 90f;
    public float bobAmplitude = 0.12f;
    public float bobFrequency = 3f;

    [Header("Sonido / VFX")]
    public GameObject collectVFX;

    private Transform playerTransform;
    private bool collected = false;

    void Awake()
    {
        // 1. Collider trigger con radio generoso para que sea fácil de agarrar
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            Collider anyCol = GetComponent<Collider>();
            if (anyCol != null) anyCol.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
            if (col.radius < 1.2f) col.radius = 1.2f;
        }

        // 2. Rigidbody cinemático obligatorio para activar OnTriggerEnter con CharacterController
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnEnable()
    {
        collected = false;
        hasLanded = false;

        FindPlayer();
        CalculateGroundTarget();
    }

    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) player = pc.gameObject;
            }

            if (player != null) playerTransform = player.transform;
        }
    }

    /// <summary>
    /// Detecta el piso debajo del ítem para que caiga y no quede flotando en el aire
    /// </summary>
    private void CalculateGroundTarget()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 1f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 30f, groundLayers))
        {
            // Ignorar al jugador o enemigos en el rayo
            if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy"))
            {
                targetGroundY = hit.point.y + heightAboveGround;
                return;
            }
        }

        // Si no detectó suelo, calcular 1 metro por debajo de la posición actual
        targetGroundY = transform.position.y - 1f;
    }

    void Update()
    {
        // 1. Giro constante
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. Caída al suelo si nació en el aire
        if (!hasLanded)
        {
            if (transform.position.y > targetGroundY)
            {
                float newY = Mathf.MoveTowards(transform.position.y, targetGroundY, fallSpeed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
            else
            {
                hasLanded = true;
            }
        }
        else
        {
            // Flotación suave muy sutil a nivel del suelo
            float floatY = targetGroundY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            transform.position = new Vector3(transform.position.x, floatY, transform.position.z);
        }

        // 3. Efecto Imán: Si el jugador se acerca a menos de 3.5m, el ítem vuela hacia él
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= magnetRadius)
            {
                Vector3 targetPos = playerTransform.position + Vector3.up * 0.8f;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, magnetSpeed * Time.deltaTime);

                if (dist <= 1.2f)
                {
                    CheckAndCollect(playerTransform.gameObject);
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        CheckAndCollect(other.gameObject);
    }

    private void CheckAndCollect(GameObject hitObject)
    {
        if (collected) return;

        if (hitObject.CompareTag("Player") || 
            hitObject.GetComponent<PlayerController>() != null || 
            hitObject.GetComponentInParent<PlayerController>() != null)
        {
            collected = true;

            if (type == PickupType.HealthPotion)
            {
                PlayerHealth health = hitObject.GetComponent<PlayerHealth>();
                if (health == null) health = hitObject.GetComponentInParent<PlayerHealth>();
                if (health != null) health.Heal(healthAmount);
            }
            else if (type == PickupType.TimeBonus)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddTime(timeAmount);
                }
            }

            if (collectVFX != null)
            {
                GameObject vfx = Instantiate(collectVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 2f);
            }

            gameObject.SetActive(false);
        }
    }
}
