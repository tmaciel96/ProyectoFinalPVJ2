// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// PenumbraFader (M3-4): oscurece la escena cuando el sepulturero apaga todas las velas del Vampiro.
// Baja de a poco la luz de la luna (Directional Light Moon) y la luz ambiente, y puede prender o apagar
// objetos al terminar (por ejemplo activar un "Global Volume Penumbra" más oscuro o apagar luces del
// sector). Desde ahí en adelante el camino se ve con el farol que da el Vampiro (PlayerLantern, M3-5).
// Va en cualquier objeto de la escena (por ejemplo en el grupo de luces del sector del Vampiro).
// =================================================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PenumbraFader : MonoBehaviour
{
    public enum Trigger { AllCandlesOut, QuestCompleted, Manual }

    [Header("Cuándo")]
    public QuestId quest = QuestId.Vampiro;
    [Tooltip("AllCandlesOut: al apagar la última vela. QuestCompleted: al entregarle la misión al NPC. Manual: llamar a Apply()")]
    public Trigger trigger = Trigger.AllCandlesOut;
    public float fadeDuration = 3f;

    [Header("Luz de la luna")]
    [Tooltip("Vacío = el Sun Source de Lighting (la luz direccional)")]
    public Light moonLight;
    [Range(0f, 1f)] public float moonIntensityMultiplier = 0.25f;

    [Header("Luz ambiente")]
    [Range(0f, 1f)] public float ambientMultiplier = 0.35f;

    [Header("Otras luces que bajan (opcional)")]
    public Light[] extraLights;
    [Range(0f, 1f)] public float extraLightsMultiplier = 0f;

    [Header("Al terminar (opcional)")]
    [Tooltip("Por ejemplo un Global Volume más oscuro")]
    public GameObject[] activateOnPenumbra;
    public GameObject[] deactivateOnPenumbra;
    public UnityEvent onPenumbra;

    public bool IsApplied { get; private set; }

    void OnEnable()
    {
        CandleQuest.OnAllCandlesOut += HandleCandlesOut;
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        CandleQuest.OnAllCandlesOut -= HandleCandlesOut;
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    private void HandleCandlesOut(QuestId id)
    {
        if (trigger == Trigger.AllCandlesOut && id == quest) Apply();
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        // Si se completa por otro camino (menú de debug) la penumbra llega igual
        if (id == quest && state == QuestState.Completada && trigger != Trigger.Manual) Apply();
    }

    /// <summary>Oscurece la escena (una sola vez).</summary>
    public void Apply()
    {
        if (IsApplied) return;
        IsApplied = true;
        StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        Light moon = moonLight != null ? moonLight : RenderSettings.sun;
        float moonFrom = moon != null ? moon.intensity : 0f;
        float ambFrom = RenderSettings.ambientIntensity;
        Color skyFrom = RenderSettings.ambientSkyColor, eqFrom = RenderSettings.ambientEquatorColor, gndFrom = RenderSettings.ambientGroundColor;
        float reflFrom = RenderSettings.reflectionIntensity;
        float[] extraFrom = new float[extraLights != null ? extraLights.Length : 0];
        for (int i = 0; i < extraFrom.Length; i++) extraFrom[i] = extraLights[i] != null ? extraLights[i].intensity : 0f;

        for (float t = 0f; t <= fadeDuration; t += Time.deltaTime)
        {
            float k = fadeDuration > 0f ? Mathf.SmoothStep(0f, 1f, t / fadeDuration) : 1f;
            SetAll(Mathf.Lerp(1f, moonIntensityMultiplier, k), Mathf.Lerp(1f, ambientMultiplier, k), Mathf.Lerp(1f, extraLightsMultiplier, k));
            yield return null;
        }
        SetAll(moonIntensityMultiplier, ambientMultiplier, extraLightsMultiplier);

        foreach (GameObject go in activateOnPenumbra) if (go != null) go.SetActive(true);
        foreach (GameObject go in deactivateOnPenumbra) if (go != null) go.SetActive(false);
        onPenumbra?.Invoke();
        Debug.Log("[PenumbraFader] El cementerio quedó en penumbra.");

        void SetAll(float moonK, float ambK, float extraK)
        {
            if (moon != null) moon.intensity = moonFrom * moonK;
            RenderSettings.ambientIntensity = ambFrom * ambK;
            RenderSettings.ambientSkyColor = skyFrom * ambK;
            RenderSettings.ambientEquatorColor = eqFrom * ambK;
            RenderSettings.ambientGroundColor = gndFrom * ambK;
            RenderSettings.reflectionIntensity = reflFrom * ambK;
            for (int i = 0; i < extraFrom.Length; i++)
            {
                if (extraLights[i] == null) continue;
                extraLights[i].intensity = extraFrom[i] * extraK;
                if (extraK <= 0f) extraLights[i].enabled = false;
            }
        }
    }
}
