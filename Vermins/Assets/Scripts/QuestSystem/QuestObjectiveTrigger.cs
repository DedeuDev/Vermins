using UnityEngine;

public class QuestObjectiveTrigger : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField]
    private QuestId questId;

    [SerializeField]
    private QuestObjectiveId objectiveId;

    [Header("Settings")]
    [Tooltip(
        "Impede que este objeto tente completar " +
        "o mesmo objetivo mais de uma vez."
    )]
    [SerializeField]
    private bool triggerOnlyOnce = true;

    [Header("Debug")]
    [SerializeField]
    private bool logResult = true;

    private bool hasTriggered;

    // ==================================================
    // TRIGGER
    // ==================================================

    public void TriggerObjective()
    {
        if (
            triggerOnlyOnce &&
            hasTriggered
        )
        {
            return;
        }

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning(
                "QuestObjectiveTrigger: " +
                "QuestManager não encontrado.",
                this
            );

            return;
        }

        bool completed =
            QuestManager.Instance.CompleteObjective(
                questId,
                objectiveId
            );

        if (!completed)
        {
            if (logResult)
            {
                Debug.Log(
                    "QuestObjectiveTrigger: " +
                    $"o objetivo '{objectiveId}' " +
                    "não foi alterado. " +
                    "A quest pode não estar ativa " +
                    "ou o objetivo já pode estar concluído.",
                    this
                );
            }

            return;
        }

        hasTriggered = true;

        if (logResult)
        {
            Debug.Log(
                "QuestObjectiveTrigger: " +
                $"objetivo '{objectiveId}' " +
                $"da quest '{questId}' concluído.",
                this
            );
        }
    }

    // ==================================================
    // DEBUG
    // ==================================================

    [ContextMenu("DEBUG - Trigger Objective")]
    private void DebugTriggerObjective()
    {
        TriggerObjective();
    }

    [ContextMenu("DEBUG - Reset Trigger")]
    private void DebugResetTrigger()
    {
        hasTriggered = false;
    }
}