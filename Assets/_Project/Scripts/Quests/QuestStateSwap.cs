// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestStateSwap (M3-8): cambia objetos cuando una misión llega a un estado, sin cablear eventos en la
// escena. Por ejemplo el caldero de la Bruja: al completarse la misión oculta el caldero vacío, muestra
// el lleno y prende las burbujas. Si la misión ya estaba en ese estado al cargar, aplica el cambio
// sin sonido.
// =================================================================================================

using UnityEngine;
using UnityEngine.Events;

public class QuestStateSwap : MonoBehaviour
{
    public QuestId quest = QuestId.Bruja;
    [Tooltip("Estado a partir del cual se aplica el cambio")]
    public QuestState whenState = QuestState.Completada;

    [Header("Cambio")]
    public GameObject[] show;
    public GameObject[] hide;
    public ParticleSystem[] play;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.8f;
    public UnityEvent onReached;

    public bool Applied { get; private set; }

    void OnEnable() => QuestManager.OnQuestStateChanged += HandleQuestChanged;
    void OnDisable() => QuestManager.OnQuestStateChanged -= HandleQuestChanged;

    void Start()
    {
        if (Reached(QuestManager.GetState(quest))) Apply(false);
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id == quest && Reached(state)) Apply(true);
    }

    private bool Reached(QuestState state) => (int)state >= (int)whenState;

    private void Apply(bool withSound)
    {
        if (Applied) return;
        Applied = true;
        foreach (GameObject go in hide) if (go != null) go.SetActive(false);
        foreach (GameObject go in show) if (go != null) go.SetActive(true);
        foreach (ParticleSystem ps in play) if (ps != null) { ps.gameObject.SetActive(true); ps.Play(true); }
        if (withSound && clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        onReached?.Invoke();
    }
}
