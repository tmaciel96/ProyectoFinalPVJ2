// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// IQuestDelivery (M2-3, contrato para M3): las misiones que se cierran entregándole algo al NPC
// (los ingredientes de la Bruja, las ofrendas del Gato) ponen un componente con esta interfaz en el
// mismo objeto que el NPCInteractable. Cuando IsReadyToDeliver es true, el NPC muestra
// "entregar a ..." y, al terminar el diálogo de entrega, llama a Deliver y completa la misión.
// Las misiones que se cumplen en otro lado (por ejemplo encender las velas) no lo necesitan: llaman
// a QuestManager.CompleteQuest directamente.
// =================================================================================================

public interface IQuestDelivery
{
    /// <summary>True cuando el jugador ya tiene todo lo que pide el NPC.</summary>
    bool IsReadyToDeliver { get; }

    /// <summary>Se llama justo antes de completar la misión (sacar los objetos del inventario, etc.).</summary>
    void Deliver();
}
