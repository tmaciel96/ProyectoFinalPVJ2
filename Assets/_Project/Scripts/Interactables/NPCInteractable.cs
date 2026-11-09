// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// NPCInteractable (M2-3): un NPC del cementerio que da y cierra su misión.
// Al presionar [E] cerca elige el diálogo según el estado de la misión en el QuestManager:
//   - Todavía no es su turno (falta la misión anterior) -> lockedDialogue
//   - Su turno, sin empezar  -> offerDialogue y, al terminarlo, StartQuest
//   - En progreso            -> reminderDialogue; si hay un IQuestDelivery listo, deliveryDialogue,
//                               Deliver y CompleteQuest
//   - Completada             -> thanksDialogue
// Los diálogos son assets DialogueData (los escribe M2-9). Si falta alguno se usa un texto genérico,
// así el NPC funciona desde el primer momento.
// Saltear el diálogo con Btn_Skip cuenta igual: la misión se da o se completa.
// =================================================================================================

using UnityEngine;
using UnityEngine.Events;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Misión")]
    public QuestId quest = QuestId.Bruja;
    [Tooltip("Nombre que aparece en el diálogo y en el aviso del HUD")]
    public string npcName = "Bruja";

    [Header("Interacción")]
    public float interactionRadius = 2.5f;
    [Tooltip("Gira para mirar al jugador al hablarle")]
    public bool faceInteractor = true;

    [Header("Diálogos (DialogueData)")]
    [Tooltip("Antes de su turno: la misión anterior todavía no se completó")]
    public DialogueData lockedDialogue;
    [Tooltip("Presenta la misión; al terminar, la misión pasa a En progreso")]
    public DialogueData offerDialogue;
    [Tooltip("Misión en progreso, sin nada para entregar")]
    public DialogueData reminderDialogue;
    [Tooltip("Entrega (si hay un IQuestDelivery listo); al terminar, la misión se completa")]
    public DialogueData deliveryDialogue;
    [Tooltip("Misión completada")]
    public DialogueData thanksDialogue;

    [Header("Indicadores sobre la cabeza (opcionales)")]
    [Tooltip("Se muestra cuando la misión se puede empezar (por ejemplo un \"!\")")]
    public GameObject questAvailableMarker;
    [Tooltip("Se muestra cuando hay algo para entregar (por ejemplo un \"?\")")]
    public GameObject readyToDeliverMarker;

    [Header("Eventos (sonido, animación)")]
    public UnityEvent onQuestGiven;
    public UnityEvent onQuestDelivered;

    private IQuestDelivery delivery;
    private bool talking;

    void Awake()
    {
        delivery = GetComponentInChildren<IQuestDelivery>();
    }

    void OnEnable()
    {
        PlayerInteractor.Register(this);
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
        UpdateMarkers();
    }

    void OnDisable()
    {
        PlayerInteractor.Unregister(this);
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    void Update()
    {
        // La entrega depende del inventario de la misión, que no avisa: se revisa cada frame (es barato)
        if (readyToDeliverMarker != null) UpdateMarkers();
    }

    // ---------------------------------------------------------------- IInteractable

    public string InteractionPrompt => ReadyToDeliver ? $"entregar a {npcName}" : $"hablar con {npcName}";
    public Vector3 InteractionPoint => transform.position;
    public float InteractionRadius => interactionRadius;
    public bool CanInteract(GameObject interactor) => !talking;

    public void Interact(GameObject interactor)
    {
        if (talking) return;
        if (faceInteractor && interactor != null) FaceTowards(interactor.transform.position);

        QuestState state = QuestManager.GetState(quest);
        switch (state)
        {
            case QuestState.NoIniciada when QuestManager.CanStart(quest):
                Talk(offerDialogue, DefaultOffer, () =>
                {
                    if (QuestManager.StartQuest(quest)) onQuestGiven?.Invoke();
                });
                break;

            case QuestState.NoIniciada:
                Talk(lockedDialogue, DefaultLocked, null);
                break;

            case QuestState.EnProgreso when ReadyToDeliver:
                Talk(deliveryDialogue, DefaultDelivery, () =>
                {
                    // Se vuelve a revisar: entre medio pudo haber cambiado algo
                    if (!ReadyToDeliver || QuestManager.GetState(quest) != QuestState.EnProgreso) return;
                    delivery.Deliver();
                    if (QuestManager.CompleteQuest(quest)) onQuestDelivered?.Invoke();
                });
                break;

            case QuestState.EnProgreso:
                Talk(reminderDialogue, DefaultReminder, null);
                break;

            default:
                Talk(thanksDialogue, DefaultThanks, null);
                break;
        }
    }

    // ---------------------------------------------------------------- Interno

    private bool ReadyToDeliver =>
        delivery != null && QuestManager.IsInProgress(quest) && delivery.IsReadyToDeliver;

    private void Talk(DialogueData data, string fallbackText, System.Action onEnd)
    {
        DialogueSystem dialogue = DialogueSystem.Instance;
        if (dialogue == null)
        {
            // Sin panel de diálogo en la escena (escena de prueba): la misión avanza igual
            Debug.Log($"[NPC {npcName}] {fallbackText}");
            onEnd?.Invoke();
            UpdateMarkers();
            return;
        }

        talking = true;
        System.Action finished = () =>
        {
            talking = false;
            onEnd?.Invoke();
            UpdateMarkers();
        };

        if (data != null && data.lines != null && data.lines.Length > 0)
            dialogue.Play(data, finished);
        else
            dialogue.Play(new[] { new DialogueLine { speaker = npcName, message = fallbackText } }, finished, npcName);
    }

    private void HandleQuestChanged(QuestId id, QuestState state) => UpdateMarkers();

    private void UpdateMarkers()
    {
        if (questAvailableMarker != null)
        {
            bool available = QuestManager.CanStart(quest);
            if (questAvailableMarker.activeSelf != available) questAvailableMarker.SetActive(available);
        }
        if (readyToDeliverMarker != null)
        {
            bool ready = ReadyToDeliver;
            if (readyToDeliverMarker.activeSelf != ready) readyToDeliverMarker.SetActive(ready);
        }
    }

    private void FaceTowards(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // Textos de respaldo hasta que estén los DialogueData de M2-9
    private string DefaultLocked => "Ahora no, sepulturero. Primero ayudá a los que esperan antes que yo.";
    private string DefaultOffer => "Necesito tu ayuda, sepulturero. ¿Me harías un favor?";
    private string DefaultReminder => "¿Todavía nada? Te sigo esperando.";
    private string DefaultDelivery => "¡Lo conseguiste! Gracias, sepulturero.";
    private string DefaultThanks => "Gracias otra vez. Que la runa te ilumine el camino.";

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
