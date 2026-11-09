// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Checkpoint (M1-10): zona trigger que, cuando el sepulturero la pisa, pasa a ser el punto de
// reaparición (CheckpointManager). Reaparece en respawnPoint (o en este mismo objeto) mirando hacia su
// frente. Se pone uno por sector, a la entrada.
// =================================================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Dónde aparece el jugador. Si queda vacío, en este objeto.")]
    public Transform respawnPoint;
    [Tooltip("Activar este checkpoint al empezar el nivel (el del primer sector)")]
    public bool activeOnStart = false;

    public bool IsCurrent => CheckpointManager.Current == Point;

    private Transform Point => respawnPoint != null ? respawnPoint : transform;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void Start()
    {
        if (activeOnStart) CheckpointManager.SetCheckpoint(Point);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerHealth>() == null) return;
        CheckpointManager.SetCheckpoint(Point);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = IsCurrent ? new Color(0.3f, 1f, 0.5f, 0.8f) : new Color(0.3f, 0.6f, 1f, 0.6f);
        Transform p = Point;
        Gizmos.DrawWireSphere(p.position + Vector3.up * 0.1f, 0.5f);
        Gizmos.DrawLine(p.position + Vector3.up, p.position + Vector3.up + p.forward * 1.2f);
    }
}
