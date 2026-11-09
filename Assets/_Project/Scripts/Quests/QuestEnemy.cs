// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestEnemy (M3-2, M3-6): marca a un enemigo para que las misiones de "limpiar el área" lo cuenten.
// No depende del tipo de enemigo (orbe del TP1, zombi o espectro de M4): un enemigo está vivo mientras
// está activo, y cuenta como eliminado cuando se desactiva (vuelve al pool) o se destruye.
// CursedSpawner se la agrega sola a lo que spawnea; a los enemigos puestos a mano en la escena hay
// que agregársela (o ponerla en el prefab).
// =================================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestEnemy : MonoBehaviour
{
    /// <summary>Un enemigo se activó (spawneó).</summary>
    public static event Action<QuestEnemy> OnEnemyAppeared;
    /// <summary>Un enemigo se desactivó o destruyó (murió).</summary>
    public static event Action<QuestEnemy> OnEnemyRemoved;

    private static readonly HashSet<QuestEnemy> alive = new HashSet<QuestEnemy>();
    public static IEnumerable<QuestEnemy> Alive => alive;

    /// <summary>La misión que lo reclamó (null si no pertenece a ninguna).</summary>
    public AreaClearQuest Owner { get; set; }

    private static bool quitting;

    void OnEnable()
    {
        alive.Add(this);
        OnEnemyAppeared?.Invoke(this);
    }

    void OnDisable()
    {
        alive.Remove(this);
        if (quitting || !gameObject.scene.isLoaded) return;
        OnEnemyRemoved?.Invoke(this);
        Owner = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        alive.Clear();
        OnEnemyAppeared = null;
        OnEnemyRemoved = null;
        quitting = false;
        Application.quitting += () => quitting = true;
    }
}
