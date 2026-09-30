using UnityEngine;

public class DeadGuardQuestInteractable :
    MonoBehaviour,
    IInteractable
{
    [Header("Dialogue")]
    [SerializeField]
    private string speakerName =
        "Guarda Morto";

    [TextArea(3, 6)]
    [SerializeField]
    private string discoveryText =
        "Um dos guardas desaparecidos... " +
        "Ele não sobreviveu à expedição. " +
        "Algo perigoso está acontecendo nos esgotos.";

    [TextArea(2, 5)]
    [SerializeField]
    private string alreadyInvestigatedText =
        "Você já examinou este corpo.";

    [TextArea(2, 5)]
    [SerializeField]
    private string questNotActiveText =
        "Um guarda morto. Talvez alguém na cidade " +
        "saiba o que aconteceu.";

    [Header("Debug")]
    [SerializeField]
    private bool logInteraction = true;

    // ==================================================
    // INTERACT
    // ==================================================

    [ContextMenu("Interact")]
    public void Interact()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError(
                "DeadGuardQuestInteractable: " +
                "QuestManager não encontrado.",
                this
            );

            return;
        }

        QuestState questState =
            QuestManager.Instance.SewerQuestState;

        // ========================================
        // QUEST AINDA NÃO COMEÇOU
        // ========================================

        if (questState == QuestState.NotStarted)
        {
            ShowDialogue(
                questNotActiveText
            );

            return;
        }

        // ========================================
        // QUEST JÁ TERMINOU
        // ========================================

        if (questState == QuestState.Completed)
        {
            ShowDialogue(
                alreadyInvestigatedText
            );

            return;
        }

        // ========================================
        // OBJETIVO JÁ FOI CONCLUÍDO
        // ========================================

        if (QuestManager.Instance.FoundDeadGuard)
        {
            ShowDialogue(
                alreadyInvestigatedText
            );

            return;
        }

        // ========================================
        // PRIMEIRA INVESTIGAÇÃO
        // ========================================

        ShowDialogue(
            discoveryText
        );

        QuestManager.Instance.CompleteObjective(
            SewerQuestObjective.FindDeadGuard
        );
    }

    // ==================================================
    // DIALOGUE
    // ==================================================

    private void ShowDialogue(
        string message
    )
    {
        if (DialogueUI.Instance != null)
        {
            DialogueUI.Instance.ShowDialogue(
                speakerName,
                message
            );
        }
        else
        {
            Debug.LogWarning(
                "DeadGuardQuestInteractable: " +
                "DialogueUI não encontrado.",
                this
            );
        }

        if (logInteraction)
        {
            Debug.Log(
                $"{speakerName}: {message}",
                this
            );
        }
    }
}