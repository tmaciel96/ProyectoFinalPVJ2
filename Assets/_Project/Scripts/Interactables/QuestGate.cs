// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestGate (M2-5): la reja que cierra el paso al sector siguiente. Cuando su misión pasa a
// Completada desactiva los colliders que bloquean, cambia lo visual (cerrada / abierta), suena y,
// si tiene una hoja asignada, la desliza o gira hasta quedar abierta.
// Se elige la misión en el Inspector: la reja del sector 1 usa la misión de la Bruja, etc.
// M2-6: si tiene asignado el Checkpoint del sector que libera, al abrirse lo vuelve el punto de
// reaparición (así no hace falta volver a pisarlo para no perder el avance).
// =================================================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class QuestGate : MonoBehaviour
{
    [Header("Misión")]
    [Tooltip("La misión que abre esta reja")]
    public QuestId quest = QuestId.Bruja;
    public bool isOpen = false;

    [Header("Bloqueo")]
    [Tooltip("Colliders que impiden pasar. Si queda vacío se usan los del objeto y sus hijos (no triggers)")]
    public Collider[] blockingColliders;

    [Header("Visual")]
    [Tooltip("Se muestra con la reja cerrada (barrera, niebla, cadenas)")]
    public GameObject closedVisuals;
    [Tooltip("Se muestra con la reja abierta")]
    public GameObject openVisuals;
    [Tooltip("Hoja de la reja que se mueve al abrir (opcional)")]
    public Transform gateLeaf;
    [Tooltip("Desplazamiento local de la hoja al abrir")]
    public Vector3 openOffset = Vector3.zero;
    [Tooltip("Giro local de la hoja al abrir (grados)")]
    public Vector3 openRotation = new Vector3(0f, 100f, 0f);
    public float openDuration = 1.5f;

    [Header("Sector siguiente (M2-6)")]
    [Tooltip("Checkpoint a la entrada del sector que libera esta reja (opcional)")]
    public Checkpoint nextSectorCheckpoint;

    [Header("Sonido y eventos")]
    public AudioSource openAudio;
    public UnityEvent onOpened;

    private Vector3 leafClosedPos;
    private Quaternion leafClosedRot;

    void Awake()
    {
        if (blockingColliders == null || blockingColliders.Length == 0)
        {
            var found = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) found.Add(c);
            blockingColliders = found.ToArray();
        }

        if (gateLeaf != null)
        {
            leafClosedPos = gateLeaf.localPosition;
            leafClosedRot = gateLeaf.localRotation;
        }
    }

    void OnEnable()  { QuestManager.OnQuestStateChanged += HandleQuestChanged; }
    void OnDisable() { QuestManager.OnQuestStateChanged -= HandleQuestChanged; }

    void Start()
    {
        // Misión ya completada (o reja marcada abierta en el Inspector): queda abierta sin animación
        if (isOpen || QuestManager.IsCompleted(quest))
        {
            isOpen = false;
            Open(animate: false);
        }
        else
        {
            ApplyState();
        }
    }

    private void HandleQuestChanged(QuestId id, QuestState state)
    {
        if (id == quest && state == QuestState.Completada) Open();
    }

    public void Open() => Open(animate: true);

    private void Open(bool animate)
    {
        if (isOpen) return;
        isOpen = true;
        ApplyState();

        if (gateLeaf != null)
        {
            if (animate && openDuration > 0f && isActiveAndEnabled) StartCoroutine(AnimateLeaf());
            else SetLeaf(1f);
        }

        if (nextSectorCheckpoint != null) nextSectorCheckpoint.Activate();

        if (animate)
        {
            if (openAudio != null) openAudio.Play();
            onOpened?.Invoke();
        }
        Debug.Log($"[QuestGate] Reja de {quest} abierta.");
    }

    private void ApplyState()
    {
        foreach (Collider c in blockingColliders)
            if (c != null) c.enabled = !isOpen;

        if (closedVisuals != null) closedVisuals.SetActive(!isOpen);
        if (openVisuals != null) openVisuals.SetActive(isOpen);
    }

    private IEnumerator AnimateLeaf()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;
            SetLeaf(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        SetLeaf(1f);
    }

    private void SetLeaf(float k)
    {
        gateLeaf.localPosition = leafClosedPos + openOffset * k;
        gateLeaf.localRotation = leafClosedRot * Quaternion.Euler(openRotation * k);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
