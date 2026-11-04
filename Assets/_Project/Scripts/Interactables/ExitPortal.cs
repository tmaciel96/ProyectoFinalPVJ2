// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Portal de escape que define la condición de victoria del nivel. Permanece sellado hasta que
// los 3 altares rúnicos han sido purificados. Al desbloquearse, activa su portal místico y
// detecta la entrada del jugador (mediante trigger y proximidad de respaldo) para notificar la victoria.
// =================================================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitPortal : MonoBehaviour
{
    [Header("Estado del Portal")]
    public bool isUnlocked = false;

    [Header("Efectos Visuales")]
    public GameObject sealedVisuals;     // Efecto de fuego/barrera mientras está sellado
    public GameObject activeVisuals;     // Efecto verde/dorado cuando se abre
    public ParticleSystem portalVFX;
    public AudioSource portalAudio;

    [Header("Radio de Escape por Proximidad")]
    public float escapeDistance = 2.8f;

    private Transform playerTransform;

    void Awake()
    {
        // 1. Collider Trigger
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // 2. Rigidbody Cinemático: Obligatorio para que el CharacterController del player dispare OnTriggerEnter
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Start()
    {
        UpdatePortalState();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        // Si el GameManager ya completó los 3 altares, asegurar que el portal esté desbloqueado
        if (!isUnlocked && GameManager.Instance != null)
        {
            if (GameManager.Instance.runesActivated >= GameManager.Instance.totalRunes)
            {
                UnlockPortal();
            }
        }

        // Respaldo por proximidad: si el jugador se mete al portal abierto, se activa la victoria garantizada
        if (isUnlocked)
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            if (playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);
                if (dist <= escapeDistance)
                {
                    TriggerVictory();
                }
            }
        }
    }

    public void UnlockPortal()
    {
        if (isUnlocked) return;
        isUnlocked = true;
        UpdatePortalState();

        if (portalAudio != null) portalAudio.Play();
        Debug.Log("<color=green>¡EL PORTAL DE ESCAPE HA SIDO PURIFICADO Y ABIERTO!</color>");
    }

    private void UpdatePortalState()
    {
        if (sealedVisuals != null) sealedVisuals.SetActive(!isUnlocked);
        if (activeVisuals != null) activeVisuals.SetActive(isUnlocked);

        if (portalVFX != null)
        {
            if (isUnlocked) portalVFX.Play();
            else portalVFX.Stop();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isUnlocked)
        {
            if (other.CompareTag("Player") || 
                other.GetComponent<PlayerController>() != null || 
                other.GetComponentInParent<PlayerController>() != null)
            {
                TriggerVictory();
            }
        }
    }

    private void TriggerVictory()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerEscaped();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, escapeDistance);
    }
}
