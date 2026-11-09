// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestItem (M3-1, M3-7): un objeto de misión que se junta caminando por encima (ingrediente de la
// Bruja, ofrenda del Gato). Gira y flota; cuando el sepulturero se acerca lo atrae como las pociones.
// Solo se puede juntar con su misión En progreso: antes queda quieto (o escondido, con
// "Oculto hasta que empiece la misión"). Al juntarlo avisa a la QuestCollection de su misión, que lleva
// la cuenta y le permite al NPC recibir la entrega.
// No necesita collider ni Rigidbody: la recogida es por distancia.
// =================================================================================================

using UnityEngine;
using UnityEngine.Events;

public class QuestItem : MonoBehaviour
{
    [Header("Misión")]
    public QuestId quest = QuestId.Bruja;
    [Tooltip("Nombre para la consola y el aviso (por ejemplo \"hongo luminoso\")")]
    public string itemName = "ingrediente";
    [Tooltip("No se ve hasta que el NPC da la misión")]
    public bool hideUntilQuestStarts = false;

    [Header("Recogida")]
    [Tooltip("Distancia (en el plano) a la que se junta")]
    public float pickupRadius = 1.2f;
    [Tooltip("Distancia a la que empieza a volar hacia el jugador (0 = sin imán)")]
    public float magnetRadius = 2.5f;
    public float magnetSpeed = 8f;

    [Header("Animación")]
    public float rotationSpeed = 60f;
    public float bobAmplitude = 0.1f;
    public float bobFrequency = 2f;

    [Header("Feedback (opcional)")]
    [Tooltip("Partícula que se instancia al juntarlo")]
    public GameObject collectVFX;
    public AudioClip collectClip;
    [Range(0f, 1f)] public float collectVolume = 0.8f;
    public UnityEvent onCollected;

    public bool IsCollected { get; private set; }
    public bool CanBeCollected => !IsCollected && QuestManager.IsInProgress(quest);

    private Vector3 basePosition;
    private Transform player;
    private PlayerHealth playerHealth;
    private bool visible = true;

    void Awake()
    {
        basePosition = transform.position;
    }

    void OnEnable()
    {
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    void Start()
    {
        FindPlayer();
        RefreshVisibility();
    }

    void Update()
    {
        if (IsCollected || !visible) return;

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        if (!CanBeCollected || player == null || (playerHealth != null && playerHealth.IsDead))
        {
            Bob();
            return;
        }

        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        float dist = delta.magnitude;

        if (dist <= pickupRadius)
        {
            Collect();
        }
        else if (dist <= magnetRadius)
        {
            Vector3 target = player.position + Vector3.up * 0.8f;
            transform.position = Vector3.MoveTowards(transform.position, target, magnetSpeed * Time.deltaTime);
        }
        else
        {
            Bob();
        }
    }

    private void Bob()
    {
        Vector3 p = transform.position;
        float y = basePosition.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        // Si el imán lo había movido y el jugador se alejó, vuelve a su lugar
        Vector3 home = new Vector3(basePosition.x, y, basePosition.z);
        transform.position = Vector3.MoveTowards(p, home, magnetSpeed * Time.deltaTime);
    }

    /// <summary>Lo junta (también se puede llamar desde código o un evento).</summary>
    public void Collect()
    {
        if (!CanBeCollected) return;
        IsCollected = true;

        if (collectVFX != null)
        {
            GameObject vfx = Instantiate(collectVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }
        if (collectClip != null) AudioSource.PlayClipAtPoint(collectClip, transform.position, collectVolume);

        QuestCollection.ReportCollected(this);
        onCollected?.Invoke();
        Debug.Log($"[QuestItem] Juntaste {itemName} ({quest}).");
        gameObject.SetActive(false);
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id == quest) RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool show = !hideUntilQuestStarts || QuestManager.GetState(quest) != QuestState.NoIniciada;
        if (show == visible) return;
        visible = show;
        // Se esconden los componentes visibles y no el objeto, para seguir escuchando al QuestManager
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = show;
        foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = show;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (show) ps.Play(); else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void FindPlayer()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return;
        player = pc.transform;
        playerHealth = pc.GetComponent<PlayerHealth>();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
        if (magnetRadius > pickupRadius)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, magnetRadius);
        }
    }
}
