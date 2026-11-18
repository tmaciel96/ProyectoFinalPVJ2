using UnityEngine;

public class QuestCounterUI : MonoBehaviour
{
    [SerializeField] private QuestId quest;
    [SerializeField] private GameObject panel;

    private void OnEnable()
    {
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
        Refresh();
    }

    private void OnDisable()
    {
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
    }

    private void HandleQuestChanged(QuestId changedQuest, QuestState state)
    {
        if (changedQuest == quest) Refresh();
    }

    private void Refresh()
    {
        panel.SetActive(QuestManager.IsInProgress(quest));
    }
}
