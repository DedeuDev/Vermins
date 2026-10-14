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
    // INTERACT
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

        // ========================================
        // QUEST 1
        // ========================================

        QuestState quest1State =
            QuestManager.Instance.GetQuestState(
                QuestId.SewerInvestigation
            );

        if (
            quest1State !=
            QuestState.Completed
        )
        {
            HandleQuest1(
                quest1State
            );

            return;
        }

        // ========================================
        // QUEST 2
        // ========================================

        QuestState quest2State =
            QuestManager.Instance.GetQuestState(
                QuestId.DeeperIntoSewers
            );

        if (
            quest2State !=
            QuestState.Completed
        )
        {
            HandleQuest2(
                quest2State
            );

            return;
        }

        // ========================================
        // QUEST 3
        // ========================================

        QuestState quest3State =
            QuestManager.Instance.GetQuestState(
                QuestId.RatPriest
            );

        if (
            quest3State !=
            QuestState.Completed
        )
        {
            HandleQuest3(
                quest3State
            );

            return;
        }

        // ========================================
        // CAMPANHA CONCLUÍDA
        // ========================================

        ShowDialogue(
            "Você fez mais do que poderíamos ter esperado. " +
            "Os esgotos estão seguros novamente e a ameaça " +
            "dos ratos foi contida."
        );
    }

    // ==================================================
    // QUEST 1
    // INVESTIGAÇÃO NOS ESGOTOS
    // ==================================================

    private void HandleQuest1(
        QuestState state
    )
    {
        switch (state)
        {
            case QuestState.NotStarted:

                StartQuest1();

                break;

            case QuestState.Active:

                Quest1Active();

                break;

            case QuestState.ReadyToTurnIn:

                TurnInQuest1();

                break;
        }
    }

    // ==================================================
    // START QUEST 1
    // ==================================================

    private void StartQuest1()
    {
        ShowDialogue(
            "Alguns guardas desapareceram nos esgotos. " +
            "Eles foram enviados para investigar, mas " +
            "ninguém voltou. Descubra o que aconteceu " +
            "com eles e investigue o que está acontecendo " +
            "lá embaixo."
        );

        QuestManager.Instance.StartQuest(
            QuestId.SewerInvestigation
        );
    }

    // ==================================================
    // QUEST 1 ACTIVE
    // ==================================================

    private void Quest1Active()
    {
        bool foundGuard =
            QuestManager.Instance.IsObjectiveCompleted(
                QuestId.SewerInvestigation,
                QuestObjectiveId.FindDeadGuard
            );

        bool defeatedBoss =
            QuestManager.Instance.IsObjectiveCompleted(
                QuestId.SewerInvestigation,
                QuestObjectiveId.DefeatBoss1
            );

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
    // TURN IN QUEST 1
    // ==================================================

    private void TurnInQuest1()
    {
        bool completed =
            QuestManager.Instance.TurnInQuest(
                QuestId.SewerInvestigation
            );

        if (!completed)
            return;

        ShowDialogue(
            "Então era isso que estava acontecendo... " +
            "Agora sabemos o que aconteceu com os homens " +
            "que enviei. Mas isso pode ser apenas o começo."
        );
    }

    // ==================================================
    // QUEST 2
    // MAIS FUNDO NOS ESGOTOS
    // ==================================================

    private void HandleQuest2(
        QuestState state
    )
    {
        switch (state)
        {
            case QuestState.NotStarted:

                StartQuest2();

                break;

            case QuestState.Active:

                Quest2Active();

                break;

            case QuestState.ReadyToTurnIn:

                TurnInQuest2();

                break;
        }
    }

    // ==================================================
    // START QUEST 2
    // ==================================================

    private void StartQuest2()
    {
        ShowDialogue(
            "Precisamos descobrir até onde isso vai. " +
            "Volte aos esgotos e avance ainda mais fundo. " +
            "Encontre quem está comandando os ratos e " +
            "descubra por que os clãs estão se unindo."
        );

        QuestManager.Instance.StartQuest(
            QuestId.DeeperIntoSewers
        );
    }

    // ==================================================
    // QUEST 2 ACTIVE
    // ==================================================

    private void Quest2Active()
    {
        bool defeatedBoss =
            QuestManager.Instance.IsObjectiveCompleted(
                QuestId.DeeperIntoSewers,
                QuestObjectiveId.DefeatBoss2
            );

        bool interrogatedBoss =
            QuestManager.Instance.IsObjectiveCompleted(
                QuestId.DeeperIntoSewers,
                QuestObjectiveId.InterrogateBoss2
            );

        if (
            !defeatedBoss &&
            !interrogatedBoss
        )
        {
            ShowDialogue(
                "Vá mais fundo nos esgotos. Precisamos " +
                "descobrir quem está por trás dessa união."
            );

            return;
        }

        if (
            defeatedBoss &&
            !interrogatedBoss
        )
        {
            ShowDialogue(
                "Você derrotou o líder deles, mas ainda " +
                "precisamos descobrir por que os clãs " +
                "de ratos estão trabalhando juntos."
            );

            return;
        }

        if (
            !defeatedBoss &&
            interrogatedBoss
        )
        {
            ShowDialogue(
                "Continue investigando as profundezas. " +
                "Ainda precisamos eliminar a ameaça."
            );
        }
    }

    // ==================================================
    // TURN IN QUEST 2
    // ==================================================

    private void TurnInQuest2()
    {
        bool completed =
            QuestManager.Instance.TurnInQuest(
                QuestId.DeeperIntoSewers
            );

        if (!completed)
            return;

        ShowDialogue(
            "Então os clãs realmente se uniram... " +
            "e existe alguém acima deles. Se esse sacerdote " +
            "é quem está organizando tudo, precisamos agir."
        );
    }

    // ==================================================
    // QUEST 3
    // O RATO SACERDOTE
    // ==================================================

    private void HandleQuest3(
        QuestState state
    )
    {
        switch (state)
        {
            case QuestState.NotStarted:

                StartQuest3();

                break;

            case QuestState.Active:

                Quest3Active();

                break;

            case QuestState.ReadyToTurnIn:

                TurnInQuest3();

                break;
        }
    }

    // ==================================================
    // START QUEST 3
    // ==================================================

    private void StartQuest3()
    {
        ShowDialogue(
            "Chegou a hora de acabar com isso. " +
            "Desça até as partes mais profundas dos esgotos " +
            "e encontre o Rato Sacerdote. Derrote-o antes " +
            "que ele consiga reunir ainda mais ratos."
        );

        QuestManager.Instance.StartQuest(
            QuestId.RatPriest
        );
    }

    // ==================================================
    // QUEST 3 ACTIVE
    // ==================================================

    private void Quest3Active()
    {
        ShowDialogue(
            "Encontre o Rato Sacerdote e acabe com essa ameaça."
        );
    }

    // ==================================================
    // TURN IN QUEST 3
    // ==================================================

    private void TurnInQuest3()
    {
        bool completed =
            QuestManager.Instance.TurnInQuest(
                QuestId.RatPriest
            );

        if (!completed)
            return;

        ShowDialogue(
            "O Rato Sacerdote está morto? Então finalmente " +
            "podemos respirar um pouco mais tranquilos. " +
            "Você prestou um grande serviço à cidade."
        );
    }

    // ==================================================
    // SHOW DIALOGUE
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