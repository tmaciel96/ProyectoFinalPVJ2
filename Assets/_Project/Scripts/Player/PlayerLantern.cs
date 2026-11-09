// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// PlayerLantern (M3-5): el farol que el Vampiro le da al sepulturero. Va en el jugador.
// Arranca apagado; al completarse la misión del Vampiro (o al llamar a Give) muestra el modelo del farol
// y prende de a poco su luz, con un parpadeo suave de llama. Si no se asigna una luz, crea una Point
// Light cálida a la altura de la mano.
// =================================================================================================

using UnityEngine;
using UnityEngine.Events;

public class PlayerLantern : MonoBehaviour
{
    public enum GiveMoment { QuestCompleted, Manual }

    [Header("Cuándo se recibe")]
    public QuestId givenByQuest = QuestId.Vampiro;
    public GiveMoment giveOn = GiveMoment.QuestCompleted;

    [Header("Luz")]
    [Tooltip("Vacío = se crea una Point Light hija")]
    public Light lanternLight;
    public Vector3 lightLocalPosition = new Vector3(0.35f, 1.3f, 0.3f);
    public Color lightColor = new Color(1f, 0.72f, 0.4f);
    public float intensity = 4f;
    public float range = 9f;
    public float fadeInTime = 1.5f;

    [Header("Parpadeo de la llama")]
    [Range(0f, 0.5f)] public float flickerAmount = 0.12f;
    public float flickerSpeed = 6f;

    [Header("Modelo (opcional)")]
    [Tooltip("Modelo del farol en la mano; se activa al recibirlo")]
    public GameObject lanternModel;

    [Header("Feedback (opcional)")]
    public AudioClip receiveClip;
    public UnityEvent onReceived;

    public bool HasLantern { get; private set; }

    private float receivedAt;

    void Awake()
    {
        if (lanternLight == null)
        {
            GameObject go = new GameObject("Farol_Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = lightLocalPosition;
            lanternLight = go.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = lightColor;
            lanternLight.range = range;
            lanternLight.shadows = LightShadows.Soft;
        }
        lanternLight.enabled = false;
        if (lanternModel != null) lanternModel.SetActive(false);
    }

    void OnEnable() => QuestManager.OnQuestStateChanged += HandleQuestChanged;
    void OnDisable() => QuestManager.OnQuestStateChanged -= HandleQuestChanged;

    void Start()
    {
        if (giveOn == GiveMoment.QuestCompleted && QuestManager.IsCompleted(givenByQuest)) Give();
    }

    void Update()
    {
        if (!HasLantern) return;
        float fade = fadeInTime > 0f ? Mathf.Clamp01((Time.time - receivedAt) / fadeInTime) : 1f;
        float noise = (Mathf.PerlinNoise(Time.time * flickerSpeed, 0.37f) - 0.5f) * 2f * flickerAmount;
        lanternLight.intensity = intensity * fade * (1f + noise);
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (giveOn == GiveMoment.QuestCompleted && id == givenByQuest && state == QuestState.Completada) Give();
    }

    /// <summary>El sepulturero recibe el farol (una sola vez).</summary>
    public void Give()
    {
        if (HasLantern) return;
        HasLantern = true;
        receivedAt = Time.time;
        lanternLight.intensity = 0f;
        lanternLight.enabled = true;
        if (lanternModel != null) lanternModel.SetActive(true);
        if (receiveClip != null) AudioSource.PlayClipAtPoint(receiveClip, transform.position);
        onReceived?.Invoke();
        Debug.Log("[PlayerLantern] El sepulturero recibió el farol.");
    }
}
