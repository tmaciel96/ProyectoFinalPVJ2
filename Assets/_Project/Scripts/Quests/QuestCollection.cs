// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestCollection (M3-1, M3-7): las misiones de "juntar y entregar". Va en el mismo objeto que el
// NPCInteractable de la Bruja (ingredientes) o del Gato (ofrendas) e implementa IQuestDelivery:
//   - Cuenta los QuestItem de su misión que el sepulturero va juntando.
//   - Cuando están todos, el NPC muestra "entregar a ..."; al terminar el diálogo de entrega el NPC
//     llama a Deliver (evento onDelivered: el caldero burbujea, etc.) y completa la misión.
// "Requeridos" en 0 cuenta solo los QuestItem de su misión que haya en la escena.
// El progreso se puede mostrar en un texto del HUD ("Ingredientes: 1 / 3") y lo avisa OnProgressChanged.
// =================================================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class QuestCollection : MonoBehaviour, IQuestDelivery
{
    [Header("Misión")]
    public QuestId quest = QuestId.Bruja;
    [Tooltip("Cuántos objetos hay que juntar (0 = todos los QuestItem de esta misión en la escena)")]
    public int required = 0;
    [Tooltip("Texto del progreso, por ejemplo \"Ingredientes\" u \"Ofrendas\"")]
    public string itemLabel = "Ingredientes";

    [Header("HUD (opcional)")]
    [Tooltip("Muestra \"Ingredientes: 1 / 3\" mientras la misión está en progreso")]
    public TextMeshProUGUI progressText;

    [Header("Al entregar")]
    [Tooltip("Por ejemplo: activar el caldero lleno, partículas o sonido")]
    public UnityEvent onDelivered;

    /// <summary>(misión, juntados, requeridos). Para el HUD de M5.</summary>
    public static event Action<QuestId, int, int> OnProgressChanged;

    public int Collected { get; private set; }
    public int Required { get; private set; }
    public bool IsReadyToDeliver => Required > 0 && Collected >= Required;

    private static readonly Dictionary<QuestId, QuestCollection> active = new Dictionary<QuestId, QuestCollection>();

    void Reset()
    {
        // Si está en el NPC, toma su misión
        NPCInteractable npc = GetComponent<NPCInteractable>();
        if (npc != null) quest = npc.quest;
        itemLabel = quest == QuestId.GatoNegro ? "Ofrendas" : "Ingredientes";
    }

    void OnEnable()
    {
        if (active.TryGetValue(quest, out QuestCollection other) && other != null && other != this)
            Debug.LogWarning($"[QuestCollection] Hay dos colecciones para {quest}; se usa la de {name}.");
        active[quest] = this;
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        if (active.TryGetValue(quest, out QuestCollection current) && current == this) active.Remove(quest);
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    void Start()
    {
        Required = required > 0 ? required : CountItemsInScene();
        if (Required == 0) Debug.LogWarning($"[QuestCollection] {quest}: no hay QuestItem de esta misión en la escena.");
        RefreshText();
    }

    private int CountItemsInScene()
    {
        int n = 0;
        foreach (QuestItem item in FindObjectsByType<QuestItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.quest == quest) n++;
        return n;
    }

    // ---------------------------------------------------------------- Lo llama QuestItem

    public static void ReportCollected(QuestItem item)
    {
        if (item == null) return;
        if (active.TryGetValue(item.quest, out QuestCollection col) && col != null) col.AddOne();
        else Debug.LogWarning($"[QuestCollection] Se juntó {item.itemName} pero no hay QuestCollection para {item.quest}.");
    }

    private void AddOne()
    {
        Collected++;
        OnProgressChanged?.Invoke(quest, Collected, Required);
        RefreshText();
        if (IsReadyToDeliver) Debug.Log($"<color=yellow>[QuestCollection] {quest}: tenés todo, volvé a entregarlo.</color>");
    }

    // ---------------------------------------------------------------- IQuestDelivery

    public void Deliver()
    {
        onDelivered?.Invoke();
        RefreshText();
    }

    // ---------------------------------------------------------------- HUD

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id == quest) RefreshText();
    }

    private void RefreshText()
    {
        if (progressText == null) return;
        bool show = QuestManager.IsInProgress(quest);
        progressText.gameObject.SetActive(show);
        if (show) progressText.text = $"{itemLabel}: {Collected} / {Required}";
    }

    // Con "Enter Play Mode" sin recargar dominio los estáticos sobreviven
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        OnProgressChanged = null;
    }
}
