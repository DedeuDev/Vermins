using UnityEngine;

public class CaptainGuardQuestNPC :
    MonoBehaviour,
    IInteractable
{
    [Header("NPC")]
    [SerializeField]
    private string npcName =
        "Capitão da Guarda";

    [Header("Debug")]
    [SerializeField]
    private bool logInteraction = true;

    // ==================================================
    // INTERAÇÃO
    // ==================================================

    [ContextMenu("Interact")]
    public void Interact()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError(
                "CaptainGuardQuestNPC: " +
                "QuestManager não encontrado.",
                this
            );

            return;
        }

        QuestState state =
            QuestManager.Instance.SewerQuestState;

        switch (state)
        {
            case QuestState.NotStarted:

                OfferQuest();

                break;

            case QuestState.Active:

                QuestStillActive();

                break;

            case QuestState.ReadyToTurnIn:

                TurnInQuest();

                break;

            case QuestState.Completed:

                QuestAlreadyCompleted();

                break;
        }
    }

    // ==================================================
    // OFERECE QUEST
    // ==================================================

    private void OfferQuest()
    {
        string message =
            "Alguns guardas desapareceram nos esgotos. " +
            "Eles foram enviados para investigar, mas " +
            "ninguém voltou. Descubra o que aconteceu " +
            "com eles e investigue o que está acontecendo " +
            "lá embaixo.";

        ShowDialogue(
            message
        );

        /*
         * POR ENQUANTO a missão começa
         * automaticamente.
         *
         * No próximo passo vamos substituir
         * isso por:
         *
         * [Aceitar missão]
         * [Agora não]
         */
        QuestManager.Instance.StartSewerQuest();
    }

    // ==================================================
    // QUEST AINDA ATIVA
    // ==================================================

    private void QuestStillActive()
    {
        bool foundGuard =
            QuestManager.Instance.FoundDeadGuard;

        bool defeatedBoss =
            QuestManager.Instance.DefeatedDungeonBoss;

        if (
            !foundGuard &&
            !defeatedBoss
        )
        {
            ShowDialogue(
                "Ainda não descobriu nada? " +
                "Continue investigando os esgotos."
            );

            return;
        }

        if (
            foundGuard &&
            !defeatedBoss
        )
        {
            ShowDialogue(
                "Então encontrou um dos guardas... " +
                "Continue investigando. Precisamos saber " +
                "o que está causando isso."
            );

            return;
        }

        if (
            !foundGuard &&
            defeatedBoss
        )
        {
            ShowDialogue(
                "Você encontrou algo perigoso lá embaixo, " +
                "mas ainda precisamos saber o que aconteceu " +
                "com os guardas."
            );
        }
    }

    // ==================================================
    // ENTREGA QUEST
    // ==================================================

    private void TurnInQuest()
    {
        bool completed =
            QuestManager.Instance
                .TurnInSewerQuest();

        if (!completed)
            return;

        ShowDialogue(
            "Então era isso que estava acontecendo... " +
            "Obrigado. Agora sabemos o que aconteceu " +
            "com os homens que enviei."
        );
    }

    // ==================================================
    // QUEST JÁ CONCLUÍDA
    // ==================================================

    private void QuestAlreadyCompleted()
    {
        ShowDialogue(
            "Obrigado novamente pela ajuda."
        );
    }

    // ==================================================
    // MOSTRA DIÁLOGO
    // ==================================================

    private void ShowDialogue(
        string message
    )
    {
        if (DialogueUI.Instance != null)
        {
            DialogueUI.Instance.ShowDialogue(
                npcName,
                message
            );
        }
        else
        {
            Debug.LogWarning(
                "CaptainGuardQuestNPC: " +
                "DialogueUI não encontrado.",
                this
            );
        }

        if (logInteraction)
        {
            Debug.Log(
                $"{npcName}: {message}",
                this
            );
        }
    }
}