// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// AltarCandle (M3-3): vela de la capilla del Vampiro. Con la misión En progreso, al acercarse aparece
// "Presiona [E] para apagar la vela"; al apagarla se desvanecen sus luces, se cortan sus partículas y,
// si están asignados, cambia el modelo encendido por el apagado (Candle_*_Lit -> Candle_*_Unlit).
// Cada vela apagada avisa con OnCandleExtinguished; CandleQuest (en el Vampiro) las cuenta.
// Antes de que el Vampiro dé la misión no se puede apagar (no muestra el aviso).
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class AltarCandle : MonoBehaviour, IInteractable
{
    [Header("Misión")]
    public QuestId quest = QuestId.Vampiro;

    [Header("Interacción")]
    public float interactionRadius = 1.8f;

    [Header("Visual")]
    [Tooltip("Luces de la llama (vacío = todas las Light hijas)")]
    public Light[] flameLights;
    [Tooltip("Partículas de la llama (vacío = todas las ParticleSystem hijas)")]
    public ParticleSystem[] flameParticles;
    [Tooltip("Modelo encendido (opcional): se oculta al apagarla")]
    public GameObject litVisual;
    [Tooltip("Modelo apagado (opcional): se muestra al apagarla")]
    public GameObject unlitVisual;
    public float fadeTime = 0.4f;

    [Header("Feedback (opcional)")]
    public AudioClip extinguishClip;
    [Range(0f, 1f)] public float extinguishVolume = 0.8f;
    [Tooltip("Humito al apagarla")]
    public GameObject smokeVFX;
    public UnityEvent onExtinguished;

    /// <summary>Avisa cada vez que se apaga una vela.</summary>
    public static event Action<AltarCandle> OnCandleExtinguished;

    public bool IsLit { get; private set; } = true;

    private float[] baseIntensities;

    void Awake()
    {
        if (flameLights == null || flameLights.Length == 0) flameLights = GetComponentsInChildren<Light>(true);
        if (flameParticles == null || flameParticles.Length == 0) flameParticles = GetComponentsInChildren<ParticleSystem>(true);
        baseIntensities = new float[flameLights.Length];
        for (int i = 0; i < flameLights.Length; i++) baseIntensities[i] = flameLights[i] != null ? flameLights[i].intensity : 0f;
        if (unlitVisual != null) unlitVisual.SetActive(false);
        if (litVisual != null) litVisual.SetActive(true);
    }

    void OnEnable() => PlayerInteractor.Register(this);
    void OnDisable() => PlayerInteractor.Unregister(this);

    // ---------------------------------------------------------------- IInteractable

    public string InteractionPrompt => "apagar la vela";
    public Vector3 InteractionPoint => transform.position;
    public float InteractionRadius => interactionRadius;
    public bool CanInteract(GameObject interactor) => IsLit && QuestManager.IsInProgress(quest);

    public void Interact(GameObject interactor)
    {
        if (CanInteract(interactor)) Extinguish();
    }

    // ---------------------------------------------------------------- Apagado

    /// <summary>Apaga la vela (también se puede llamar desde código o un evento).</summary>
    public void Extinguish()
    {
        if (!IsLit) return;
        IsLit = false;

        foreach (ParticleSystem ps in flameParticles)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StartCoroutine(FadeOut());

        if (smokeVFX != null)
        {
            GameObject vfx = Instantiate(smokeVFX, InteractionPoint + Vector3.up * 0.3f, Quaternion.identity);
            Destroy(vfx, 4f);
        }
        if (extinguishClip != null) AudioSource.PlayClipAtPoint(extinguishClip, transform.position, extinguishVolume);

        OnCandleExtinguished?.Invoke(this);
        onExtinguished?.Invoke();
    }

    private IEnumerator FadeOut()
    {
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            float k = 1f - t / fadeTime;
            for (int i = 0; i < flameLights.Length; i++)
                if (flameLights[i] != null) flameLights[i].intensity = baseIntensities[i] * k;
            yield return null;
        }
        foreach (Light l in flameLights) if (l != null) l.enabled = false;
        if (litVisual != null) litVisual.SetActive(false);
        if (unlitVisual != null) unlitVisual.SetActive(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnCandleExtinguished = null;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
