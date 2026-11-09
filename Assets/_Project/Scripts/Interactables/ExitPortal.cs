// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// ExitPortal (M2-8): la salida del cementerio, detrás del portón principal. Queda sellado hasta que
// las 5 misiones están completadas (las 5 runas encendidas); entonces se abre y, cuando el sepulturero
// lo cruza (trigger o, de respaldo, por distancia), el GameManager muestra la pantalla de victoria.
// El portón que bloquea el paso es un QuestGate con "Requiere todas las misiones".
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

    void OnEnable()  { QuestManager.OnAllQuestsCompleted += UnlockPortal; }
    void OnDisable() { QuestManager.OnAllQuestsCompleted -= UnlockPortal; }

    void Start()
    {
        if (QuestManager.AllCompleted) isUnlocked = true;
        UpdatePortalState();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
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
