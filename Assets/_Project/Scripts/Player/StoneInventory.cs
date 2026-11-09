// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// StoneInventory (M1-4): las piedras que lleva el sepulturero (máximo 10).
// Reemplaza a la munición con recarga del TP1: las piedras se juntan en los montículos (StonePile).
// Cada cambio avisa con OnStonesChanged(actual, máximo) para el HUD.
// Al reaparecer, si tenía menos de startingStones, se le completan hasta ese número.
// =================================================================================================

using System;
using UnityEngine;

public class StoneInventory : MonoBehaviour
{
    [Tooltip("Máximo de piedras que puede cargar")]
    public int maxStones = 10;
    [Tooltip("Piedras al empezar el nivel y mínimo garantizado al reaparecer")]
    public int startingStones = 5;

    /// <summary>(actual, máximo)</summary>
    public static event Action<int, int> OnStonesChanged;
    /// <summary>Se intentó lanzar sin piedras (el HUD puede hacer parpadear el contador).</summary>
    public static event Action OnOutOfStones;

    public int Count { get; private set; }
    public bool IsFull => Count >= maxStones;

    void Awake()
    {
        Count = Mathf.Clamp(startingStones, 0, maxStones);
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerRespawned += HandleRespawn;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerRespawned -= HandleRespawn;
    }

    void Start()
    {
        Notify();
    }

    /// <summary>Gasta una piedra. Devuelve false si no quedaba ninguna.</summary>
    public bool TryConsume()
    {
        if (Count <= 0)
        {
            OnOutOfStones?.Invoke();
            return false;
        }
        Count--;
        Notify();
        return true;
    }

    /// <summary>Suma piedras sin pasar del máximo. Devuelve cuántas entraron realmente.</summary>
    public int Add(int amount)
    {
        if (amount <= 0) return 0;
        int added = Mathf.Min(amount, maxStones - Count);
        if (added <= 0) return 0;
        Count += added;
        Notify();
        return added;
    }

    private void HandleRespawn()
    {
        if (Count < startingStones)
        {
            Count = Mathf.Min(startingStones, maxStones);
            Notify();
        }
    }

    /// <summary>Vuelve a avisar el valor actual (lo usa el HUD al suscribirse tarde).</summary>
    public void Notify()
    {
        OnStonesChanged?.Invoke(Count, maxStones);
    }
}
