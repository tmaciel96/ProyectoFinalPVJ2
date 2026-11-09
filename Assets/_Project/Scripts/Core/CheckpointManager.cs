// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// CheckpointManager (contrato de M2-6, lo usa M1-10): recuerda el último punto de reaparición activado.
// PlayerHealth.Respawn lleva al jugador a CheckpointManager.Current; si todavía no se activó ninguno,
// usa el SpawnPoint de la escena.
// Cualquier sistema puede fijar el checkpoint: el componente Checkpoint al pasar por encima, o la
// reja de sector (QuestGate) al abrirse.
// M2-6: cada checkpoint puede decir a qué sector pertenece. Nunca se vuelve a un sector anterior:
// si el jugador regresa al sector 1 a buscar algo y muere, reaparece igual en el último sector abierto.
// =================================================================================================

using System;
using UnityEngine;

public static class CheckpointManager
{
    private static Transform current;
    private static int currentSector = -1;

    /// <summary>El último checkpoint activado (null si ninguno o si pertenecía a una escena ya descargada).</summary>
    public static Transform Current => current != null ? current : null;

    /// <summary>Avisa cuando cambia el checkpoint (para un cartel "Punto de control" en el HUD, por ejemplo).</summary>
    public static event Action<Transform> OnCheckpointChanged;

    /// <summary>Sector del checkpoint actual (-1 si ninguno o si se fijó sin sector).</summary>
    public static int CurrentSector => current != null ? currentSector : -1;

    /// <summary>
    /// Fija el punto de reaparición. Con sector >= 0 se ignora si ya hay uno de un sector posterior;
    /// con sector negativo (sin sector) siempre reemplaza al actual.
    /// </summary>
    public static void SetCheckpoint(Transform point, int sector = -1)
    {
        if (point == null || point == current) return;
        if (sector >= 0 && sector < CurrentSector) return;
        current = point;
        currentSector = sector;
        OnCheckpointChanged?.Invoke(point);
    }

    public static void Clear()
    {
        current = null;
        currentSector = -1;
    }

    // Al entrar en Play sin recargar el dominio, no arrastrar el checkpoint de la partida anterior
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        currentSector = -1;
        OnCheckpointChanged = null;
    }
}
