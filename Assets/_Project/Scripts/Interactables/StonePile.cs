// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// StonePile (M1-6): montículo de piedras. Con [E] el sepulturero junta stonesGiven piedras (sin pasar del
// máximo del StoneInventory). Después queda vacío refillTime segundos y vuelve a llenarse.
// Mientras está vacío (o si el jugador ya tiene el máximo) no muestra el aviso de interacción.
// =================================================================================================

using UnityEngine;

public class StonePile : MonoBehaviour, IInteractable
{
    [Header("Piedras")]
    public int stonesGiven = 5;
    [Tooltip("Segundos hasta que el montículo vuelve a tener piedras (0 = no se repone)")]
    public float refillTime = 20f;

    [Header("Interacción")]
    public float interactionRadius = 2f;

    [Header("Visual (opcional)")]
    [Tooltip("Lo que se oculta mientras el montículo está vacío (por ejemplo las piedras sueltas de arriba)")]
    public GameObject fullVisual;

    [Header("Feedback (opcional)")]
    public AudioSource audioSource;
    public AudioClip pickupClip;

    public bool IsAvailable => Time.time >= availableAt;

    private float availableAt;

    // ---------------------------------------------------------------- IInteractable

    public string InteractionPrompt => "juntar piedras";
    public Vector3 InteractionPoint => transform.position;
    public float InteractionRadius => interactionRadius;

    public bool CanInteract(GameObject interactor)
    {
        if (!IsAvailable) return false;
        StoneInventory inv = interactor != null ? interactor.GetComponentInParent<StoneInventory>() : null;
        return inv != null && !inv.IsFull;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        StoneInventory inv = interactor.GetComponentInParent<StoneInventory>();
        if (inv.Add(stonesGiven) <= 0) return;

        if (audioSource != null && pickupClip != null) audioSource.PlayOneShot(pickupClip);

        if (refillTime > 0f)
        {
            availableAt = Time.time + refillTime;
        }
        else
        {
            availableAt = float.PositiveInfinity;
        }
        SetFullVisual(false);
    }

    // ---------------------------------------------------------------- Ciclo de vida

    void OnEnable()
    {
        availableAt = 0f;
        SetFullVisual(true);
        PlayerInteractor.Register(this);
    }

    void OnDisable()
    {
        PlayerInteractor.Unregister(this);
    }

    void Update()
    {
        // Volver a mostrar las piedras cuando se repone
        if (fullVisual != null && !fullVisual.activeSelf && IsAvailable)
            SetFullVisual(true);
    }

    private void SetFullVisual(bool full)
    {
        if (fullVisual != null && fullVisual.activeSelf != full) fullVisual.SetActive(full);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
