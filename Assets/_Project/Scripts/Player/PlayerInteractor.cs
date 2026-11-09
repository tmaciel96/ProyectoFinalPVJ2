// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// PlayerInteractor (M1-7): busca el IInteractable más cercano dentro de su radio, expone su texto para
// el HUD (Current / OnFocusChanged) y lo activa al presionar [E].
// Solo la E: el Espacio queda para avanzar los diálogos.
// Mientras hay un diálogo abierto no interactúa, así la misma E que cierra un diálogo no abre otro.
// =================================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    // ---------------------------------------------------------------- Registro global

    private static readonly List<IInteractable> registered = new List<IInteractable>();

    /// <summary>Los interactuables se anotan en OnEnable.</summary>
    public static void Register(IInteractable interactable)
    {
        if (interactable != null && !registered.Contains(interactable)) registered.Add(interactable);
    }

    /// <summary>Y se borran en OnDisable.</summary>
    public static void Unregister(IInteractable interactable)
    {
        registered.Remove(interactable);
    }

    // ---------------------------------------------------------------- Instancia

    /// <summary>El interactuable que el jugador tiene enfocado (null si no hay ninguno).</summary>
    public IInteractable Current { get; private set; }

    /// <summary>Avisa cuando cambia el foco (para el texto "Presiona [E] para ...").</summary>
    public static event Action<IInteractable> OnFocusChanged;

    private PlayerHealth health;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    void OnDisable()
    {
        SetCurrent(null);
    }

    void Update()
    {
        bool blocked = (health != null && health.IsDead) || DialogueSystem.BlocksGameplay;
        SetCurrent(blocked ? null : FindClosest());

        if (Current != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Current.Interact(gameObject);
    }

    private IInteractable FindClosest()
    {
        IInteractable best = null;
        float bestDist = float.MaxValue;
        Vector3 pos = transform.position;

        for (int i = registered.Count - 1; i >= 0; i--)
        {
            IInteractable it = registered[i];
            // Un objeto destruido sin pasar por OnDisable queda como null de Unity
            if (it == null || (it is UnityEngine.Object o && o == null)) { registered.RemoveAt(i); continue; }

            Vector3 delta = it.InteractionPoint - pos;
            delta.y = 0f; // distancia en el plano: no importa la altura de la tumba o del altar
            float dist = delta.magnitude;
            if (dist > it.InteractionRadius || dist >= bestDist) continue;
            if (!it.CanInteract(gameObject)) continue;

            best = it;
            bestDist = dist;
        }
        return best;
    }

    private void SetCurrent(IInteractable next)
    {
        if (ReferenceEquals(next, Current)) return;
        Current = next;
        OnFocusChanged?.Invoke(Current);
    }
}
