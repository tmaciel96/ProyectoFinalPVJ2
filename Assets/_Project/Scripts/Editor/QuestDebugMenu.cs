// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// QuestDebugMenu (M2-1): menú Sepulturero > Misiones para probar altares, rejas y HUD en Play sin
// jugar cada misión. Solo funciona con el juego corriendo y no entra en el build.
// =================================================================================================

using System.Text;
using UnityEditor;
using UnityEngine;

public static class QuestDebugMenu
{
    private const string Root = "Sepulturero/Misiones/";

    [MenuItem(Root + "Empezar la actual", false, 0)]
    private static void StartCurrent()
    {
        QuestId? id = QuestManager.CurrentQuest;
        if (id.HasValue) QuestManager.StartQuest(id.Value);
    }

    [MenuItem(Root + "Completar la actual", false, 1)]
    private static void CompleteCurrent()
    {
        QuestId? id = QuestManager.CurrentQuest;
        if (!id.HasValue) return;
        // Para probar no hace falta pasar por el NPC: si no empezó, se empieza y se completa
        if (QuestManager.GetState(id.Value) == QuestState.NoIniciada) QuestManager.StartQuest(id.Value);
        QuestManager.CompleteQuest(id.Value);
    }

    [MenuItem(Root + "Completar todas", false, 2)]
    private static void CompleteAll()
    {
        while (QuestManager.CurrentQuest.HasValue)
        {
            QuestId id = QuestManager.CurrentQuest.Value;
            if (QuestManager.GetState(id) == QuestState.NoIniciada) QuestManager.StartQuest(id);
            if (!QuestManager.CompleteQuest(id)) break;
        }
    }

    [MenuItem(Root + "Reiniciar misiones", false, 20)]
    private static void ResetAll() => QuestManager.ResetAll();

    [MenuItem(Root + "Mostrar estado en consola", false, 21)]
    private static void LogState()
    {
        var sb = new StringBuilder("[QuestManager] Estado:");
        for (int i = 0; i < QuestManager.QuestCount; i++)
        {
            QuestId id = (QuestId)i;
            sb.Append($"\n  {i + 1}. {id}: {QuestManager.GetState(id)}");
        }
        sb.Append($"\n  Runas: {QuestManager.CompletedCount}/{QuestManager.QuestCount}");
        Debug.Log(sb.ToString());
    }

    [MenuItem(Root + "Empezar la actual", true)]
    [MenuItem(Root + "Completar la actual", true)]
    [MenuItem(Root + "Completar todas", true)]
    [MenuItem(Root + "Reiniciar misiones", true)]
    [MenuItem(Root + "Mostrar estado en consola", true)]
    private static bool IsPlaying() => Application.isPlaying;
}
