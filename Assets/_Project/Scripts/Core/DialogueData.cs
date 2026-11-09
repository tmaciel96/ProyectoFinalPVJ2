// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// DialogueData (M2-2): un diálogo guardado como asset, para no escribir los textos en la escena.
// Crear con clic derecho > Create > Sepulturero > Diálogo. Lo reproduce DialogueSystem.Play(data).
// =================================================================================================

using System;
using UnityEngine;

/// <summary>Una línea de diálogo: quién habla y qué dice.</summary>
[Serializable]
public struct DialogueLine
{
    public string speaker;
    [TextArea(2, 4)]
    public string message;
}

[CreateAssetMenu(fileName = "Dialogo_", menuName = "Sepulturero/Diálogo")]
public class DialogueData : ScriptableObject
{
    [Tooltip("Nombre que se usa si una línea deja el speaker vacío")]
    public string defaultSpeaker;
    public DialogueLine[] lines = new DialogueLine[0];
}
