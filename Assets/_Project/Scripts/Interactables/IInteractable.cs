// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Contrato común de todo lo que el sepulturero activa con [E]: altares, la Tumba Maldita, la pila de
// piedras, los NPC, las velas del vampiro. Cada objeto se registra en PlayerInteractor (OnEnable /
// OnDisable) y el PlayerInteractor decide cuál es el más cercano, muestra su texto y lo ejecuta.
// =================================================================================================

using UnityEngine;

public interface IInteractable
{
    /// <summary>Texto del aviso, completa la frase "Presiona [E] para ..." (por ejemplo "apagar la tumba").</summary>
    string InteractionPrompt { get; }

    /// <summary>Punto desde el que se mide la distancia al jugador.</summary>
    Vector3 InteractionPoint { get; }

    /// <summary>Distancia máxima a la que se puede activar.</summary>
    float InteractionRadius { get; }

    /// <summary>Si en este momento acepta la interacción (por ejemplo, una tumba ya apagada devuelve false).</summary>
    bool CanInteract(GameObject interactor);

    /// <summary>Lo que pasa al presionar [E].</summary>
    void Interact(GameObject interactor);
}
