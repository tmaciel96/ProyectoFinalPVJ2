// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// CandleQuest (M3-3): la misión de las velas. Va en el mismo objeto que el NPCInteractable del Vampiro
// e implementa IQuestDelivery:
//   - Cuenta las AltarCandle de su misión que se van apagando.
//   - Al apagar la última avisa con OnAllCandlesOut (PenumbraFader oscurece el sector, M3-4) y el NPC
//     pasa a mostrar "entregar a Vampiro"; al terminar ese diálogo se completa la misión.
// "Velas" en 0 cuenta solas las AltarCandle de su misión que haya en la escena.
// =================================================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class CandleQuest : MonoBehaviour, IQuestDelivery
{
    [Header("Misión")]
    public QuestId quest = QuestId.Vampiro;
    [Tooltip("Cuántas velas hay que apagar (0 = todas las AltarCandle de esta misión en la escena)")]
    public int required = 0;

    [Header("HUD (opcional)")]
    [Tooltip("Muestra \"Velas encendidas: 3\" mientras la misión está en progreso")]
    public TextMeshProUGUI progressText;

    [Header("Eventos")]
    [Tooltip("Al apagar la última vela (antes de volver a hablar con el NPC)")]
    public UnityEvent onAllCandlesOut;
    [Tooltip("Al entregar la misión al NPC")]
    public UnityEvent onDelivered;

    /// <summary>Se apagaron todas las velas de esa misión.</summary>
    public static event Action<QuestId> OnAllCandlesOut;

    public int Extinguished { get; private set; }
    public int Required { get; private set; }
    public int Remaining => Mathf.Max(0, Required - Extinguished);
    public bool IsReadyToDeliver => Required > 0 && Extinguished >= Required;

    void OnEnable()
    {
        AltarCandle.OnCandleExtinguished += HandleCandle;
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        AltarCandle.OnCandleExtinguished -= HandleCandle;
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    void Start()
    {
        if (required > 0)
        {
            Required = required;
        }
        else
        {
            foreach (AltarCandle c in FindObjectsByType<AltarCandle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.quest == quest) Required++;
        }
        if (Required == 0) Debug.LogWarning($"[CandleQuest] {quest}: no hay AltarCandle de esta misión en la escena.");
        RefreshText();
    }

    private void HandleCandle(AltarCandle candle)
    {
        if (candle.quest != quest || IsReadyToDeliver) return;
        Extinguished++;
        RefreshText();
        if (IsReadyToDeliver)
        {
            Debug.Log($"<color=yellow>[CandleQuest] {quest}: se apagaron todas las velas.</color>");
            onAllCandlesOut?.Invoke();
            OnAllCandlesOut?.Invoke(quest);
        }
    }

    public void Deliver()
    {
        onDelivered?.Invoke();
        RefreshText();
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id == quest) RefreshText();
    }

    private void RefreshText()
    {
        if (progressText == null) return;
        bool show = QuestManager.IsInProgress(quest);
        progressText.gameObject.SetActive(show);
        if (!show) return;
        progressText.text = Remaining > 0 ? $"Velas encendidas: {Remaining}" : "Volvé con el Vampiro";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnAllCandlesOut = null;
}
