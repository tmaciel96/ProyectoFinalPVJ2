// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestManager (M2-1, contrato compartido): estado de las 5 misiones del cementerio.
//  - Cada misión pasa por No iniciada -> En progreso -> Completada, y avisa con OnQuestStateChanged.
//  - Orden secuencial (decisión del grupo): Bruja, Esqueleto, Vampiro, Niño fantasma y Gato negro.
//    Una misión solo se puede empezar cuando la anterior está completada.
//  - Quién lo usa: el NPC (M2-3) llama a StartQuest al hablarle; cada misión de M3 llama a
//    CompleteQuest al cumplirse; el altar (M2-4), la reja (M2-5), el GameManager (M2-7) y el HUD (M5)
//    escuchan OnQuestStateChanged sin conocer a las misiones.
//  - Es estático, como CheckpointManager: no hace falta ponerlo en la escena. El estado vuelve a cero
//    cada vez que se carga una escena (empezar o reiniciar la partida); morir no lo toca.
// =================================================================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Las 5 misiones, en el orden en que se juegan.</summary>
public enum QuestId { Bruja, Esqueleto, Vampiro, NinoFantasma, GatoNegro }

public enum QuestState { NoIniciada, EnProgreso, Completada }

public static class QuestManager
{
    public static readonly int QuestCount = Enum.GetValues(typeof(QuestId)).Length;

    /// <summary>
    /// Si es true (lo normal), una misión solo arranca con la anterior completada.
    /// Se puede apagar para probar una misión suelta en una escena de prueba.
    /// </summary>
    public static bool EnforceOrder = true;

    private static readonly QuestState[] states = new QuestState[QuestCount];

    /// <summary>(misión, estado nuevo). Lo escuchan altares, rejas, GameManager y HUD.</summary>
    public static event Action<QuestId, QuestState> OnQuestStateChanged;
    /// <summary>Se dispara una vez, cuando se completa la quinta misión.</summary>
    public static event Action OnAllQuestsCompleted;

    // ---------------------------------------------------------------- Consultas

    public static QuestState GetState(QuestId id) => states[(int)id];
    public static bool IsCompleted(QuestId id) => states[(int)id] == QuestState.Completada;
    public static bool IsInProgress(QuestId id) => states[(int)id] == QuestState.EnProgreso;

    /// <summary>Cuántas misiones están completadas (las runas encendidas, de 0 a 5).</summary>
    public static int CompletedCount
    {
        get
        {
            int n = 0;
            foreach (QuestState s in states) if (s == QuestState.Completada) n++;
            return n;
        }
    }

    public static bool AllCompleted => CompletedCount == QuestCount;

    /// <summary>La primera misión que falta completar (en progreso o por empezar); null si ya están todas.</summary>
    public static QuestId? CurrentQuest
    {
        get
        {
            for (int i = 0; i < QuestCount; i++)
                if (states[i] != QuestState.Completada) return (QuestId)i;
            return null;
        }
    }

    /// <summary>True si la misión todavía no empezó y (con EnforceOrder) la anterior ya está completada.</summary>
    public static bool CanStart(QuestId id)
    {
        if (states[(int)id] != QuestState.NoIniciada) return false;
        if (!EnforceOrder || id == 0) return true;
        return states[(int)id - 1] == QuestState.Completada;
    }

    // ---------------------------------------------------------------- Cambios de estado

    /// <summary>
    /// Pasa la misión a En progreso. Devuelve false si ya empezó o si falta completar la anterior
    /// (el NPC usa eso para decir "todavía no es tu turno").
    /// </summary>
    public static bool StartQuest(QuestId id)
    {
        if (!CanStart(id))
        {
            Debug.Log($"[QuestManager] {id} no puede empezar (estado {GetState(id)}).");
            return false;
        }
        SetState(id, QuestState.EnProgreso);
        return true;
    }

    /// <summary>
    /// Marca la misión como completada. Solo funciona si estaba En progreso, así una misión no se
    /// cumple antes de que el NPC la haya dado. Devuelve false si no cambió nada.
    /// </summary>
    public static bool CompleteQuest(QuestId id)
    {
        if (states[(int)id] != QuestState.EnProgreso)
        {
            Debug.Log($"[QuestManager] {id} no se puede completar (estado {GetState(id)}).");
            return false;
        }
        SetState(id, QuestState.Completada);
        if (AllCompleted) OnAllQuestsCompleted?.Invoke();
        return true;
    }

    /// <summary>Vuelve todas las misiones a No iniciada y avisa a los que escuchan.</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < QuestCount; i++)
        {
            if (states[i] == QuestState.NoIniciada) continue;
            states[i] = QuestState.NoIniciada;
            OnQuestStateChanged?.Invoke((QuestId)i, QuestState.NoIniciada);
        }
    }

    private static void SetState(QuestId id, QuestState state)
    {
        states[(int)id] = state;
        Debug.Log($"[QuestManager] {id}: {state} ({CompletedCount}/{QuestCount} completadas)");
        OnQuestStateChanged?.Invoke(id, state);
    }

    // ---------------------------------------------------------------- Ciclo de vida

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        // Con "Enter Play Mode" sin recargar dominio los estáticos sobreviven: se limpian acá
        Array.Clear(states, 0, states.Length);
        EnforceOrder = true;
        OnQuestStateChanged = null;
        OnAllQuestsCompleted = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Empezar o reiniciar la partida carga la escena de nuevo: misiones desde cero.
        // Las escenas aditivas (si algún día se usan) no reinician nada.
        if (mode == LoadSceneMode.Single) Array.Clear(states, 0, states.Length);
    }
}
