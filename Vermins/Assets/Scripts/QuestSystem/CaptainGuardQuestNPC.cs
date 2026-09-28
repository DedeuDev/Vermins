using UnityEngine;

public class CaptainGuardQuestNPC :
    MonoBehaviour,
    IInteractable
{
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
        if (logInteraction)
        {
            Debug.Log(
                "Capitão: Alguns guardas desapareceram " +
                "nos esgotos. Descubra o que aconteceu " +
                "com eles e investigue o que está " +
                "acontecendo lá embaixo.",
                this
            );
        }

        QuestManager.Instance.StartSewerQuest();
    }

    // ==================================================
    // QUEST AINDA ATIVA
    // ==================================================

    private void QuestStillActive()
    {
        if (!logInteraction)
            return;

        bool foundGuard =
            QuestManager.Instance.FoundDeadGuard;

        bool defeatedBoss =
            QuestManager.Instance.DefeatedDungeonBoss;

        if (!foundGuard && !defeatedBoss)
        {
            Debug.Log(
                "Capitão: Ainda não descobriu nada? " +
                "Continue investigando os esgotos.",
                this
            );

            return;
        }

        if (foundGuard && !defeatedBoss)
        {
            Debug.Log(
                "Capitão: Então encontrou um dos guardas... " +
                "Continue investigando. Precisamos saber " +
                "o que está causando isso.",
                this
            );

            return;
        }

        if (!foundGuard && defeatedBoss)
        {
            Debug.Log(
                "Capitão: Você encontrou algo perigoso " +
                "lá embaixo, mas ainda precisamos saber " +
                "o que aconteceu com os guardas.",
                this
            );
        }
    }

    // ==================================================
    // ENTREGA QUEST
    // ==================================================

    private void TurnInQuest()
    {
        bool completed =
            QuestManager.Instance.TurnInSewerQuest();

        if (!completed)
            return;

        if (logInteraction)
        {
            Debug.Log(
                "Capitão: Então era isso que estava " +
                "acontecendo... Obrigado. Agora sabemos " +
                "o que aconteceu com os homens que enviei.",
                this
            );
        }
    }

    // ==================================================
    // QUEST JÁ CONCLUÍDA
    // ==================================================

    private void QuestAlreadyCompleted()
    {
        if (!logInteraction)
            return;

        Debug.Log(
            "Capitão: Obrigado novamente pela ajuda.",
            this
        );
    }
}