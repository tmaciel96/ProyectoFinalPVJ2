// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// RuneAltar (M2-4): la runa de una misión. Se enciende sola (luz, material, partículas y sonido)
// cuando su misión pasa a Completada en el QuestManager; no hace falta acercarse.
// Hay uno por misión: en el Inspector se elige cuál.
// "Activación manual" deja encenderlo con [E] como en el TP1 (solo para pruebas; apagado por defecto).
// El contador de runas del HUD y el portal los maneja el GameManager con el QuestManager (M2-7).
// =================================================================================================

using UnityEngine;
using UnityEngine.Events;

public class RuneAltar : MonoBehaviour, IInteractable
{
    [Header("Misión")]
    [Tooltip("La misión que enciende esta runa")]
    public QuestId quest = QuestId.Bruja;
    [Tooltip("Permite encenderlo con [E] sin completar la misión (pruebas / comportamiento del TP1)")]
    public bool manualActivation = false;
    public float interactionDistance = 3f;
    public bool isActivated = false;

    [Header("Efectos Visuales de Activación")]
    public Light altarLight;
    public Color activatedLightColor = new Color(1f, 0.75f, 0.1f);
    public ParticleSystem activationVFX;
    public AudioSource activationAudio;
    public Renderer runeRenderer;
    public Material activatedMaterial;

    [Header("Eventos")]
    public UnityEvent onActivated;

    void OnEnable()
    {
        PlayerInteractor.Register(this);
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
    }

    void OnDisable()
    {
        PlayerInteractor.Unregister(this);
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    void Start()
    {
        // Si la misión ya estaba completada antes de que el altar existiera, se enciende sin festejo
        if (QuestManager.IsCompleted(quest)) ActivateAltar(playEffects: false);
    }

    // ---------------------------------------------------------------- IInteractable (solo manual)

    public string InteractionPrompt => "purificar el altar";
    public Vector3 InteractionPoint => transform.position;
    public float InteractionRadius => interactionDistance;
    public bool CanInteract(GameObject interactor) => manualActivation && !isActivated;
    public void Interact(GameObject interactor) => ActivateAltar();

    // ---------------------------------------------------------------- Activación

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id == quest && state == QuestState.Completada) ActivateAltar();
    }

    public void ActivateAltar() => ActivateAltar(playEffects: true);

    private void ActivateAltar(bool playEffects)
    {
        if (isActivated) return;
        isActivated = true;

        if (altarLight != null)
        {
            altarLight.enabled = true;
            altarLight.color = activatedLightColor;
        }

        if (runeRenderer != null && activatedMaterial != null)
        {
            runeRenderer.material = activatedMaterial;
        }

        if (activationVFX != null) activationVFX.Play();
        if (playEffects && activationAudio != null) activationAudio.Play();

        onActivated?.Invoke();
        Debug.Log($"[RuneAltar] Runa de {quest} encendida.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
