// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// CheckpointManager (contrato de M2-6, lo usa M1-10): recuerda el último punto de reaparición activado.
// PlayerHealth.Respawn lleva al jugador a CheckpointManager.Current; si todavía no se activó ninguno,
// usa el SpawnPoint de la escena.
// Cualquier sistema puede fijar el checkpoint: el componente Checkpoint al pasar por encima, o más
// adelante el SectorGate (M2-6) al liberar un sector.
// =================================================================================================

using System;
using UnityEngine;

public static class CheckpointManager
{
    private static Transform current;

    /// <summary>El último checkpoint activado (null si ninguno o si pertenecía a una escena ya descargada).</summary>
    public static Transform Current => current != null ? current : null;

    /// <summary>Avisa cuando cambia el checkpoint (para un cartel "Punto de control" en el HUD, por ejemplo).</summary>
    public static event Action<Transform> OnCheckpointChanged;

    public static void SetCheckpoint(Transform point)
    {
        if (point == null || point == current) return;
        current = point;
        OnCheckpointChanged?.Invoke(point);
    }

    public static void Clear()
    {
        current = null;
    }

    // Al entrar en Play sin recargar el dominio, no arrastrar el checkpoint de la partida anterior
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        OnCheckpointChanged = null;
    }
}
