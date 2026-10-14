using System;
using System.Collections.Generic;
using UnityEngine;

// ======================================================
// ESTADO DE UMA QUEST
// ======================================================

public enum QuestState
{
    NotStarted,
    Active,
    ReadyToTurnIn,
    Completed
}

// ======================================================
// IDS DAS QUESTS
// ======================================================

public enum QuestId
{
    SewerInvestigation,
    DeeperIntoSewers,
    RatPriest
}

// ======================================================
// IDS DE TODOS OS OBJETIVOS
// ======================================================

public enum QuestObjectiveId
{
    // QUEST 1
    FindDeadGuard,
    DefeatBoss1,

    // QUEST 2
    DefeatBoss2,
    InterrogateBoss2,

    // QUEST 3
    DefeatRatPriest
}

// ======================================================
// ENUM ANTIGO
//
// Mantido temporariamente para que os scripts
// existentes da Quest 1 continuem funcionando.
// ======================================================

public enum SewerQuestObjective
{
    FindDeadGuard,
    DefeatDungeonBoss
}

// ======================================================
// OBJETIVO
// ======================================================

[Serializable]
public class QuestObjectiveProgress
{
    [SerializeField]
    private QuestObjectiveId objectiveId;

    [SerializeField]
    private string description;

    [SerializeField]
    private bool completed;

    public QuestObjectiveId ObjectiveId =>
        objectiveId;

    public string Description =>
        description;

    public bool Completed =>
        completed;

    // ==================================================
    // CONSTRUCTOR
    // ==================================================

    public QuestObjectiveProgress(
        QuestObjectiveId objectiveId,
        string description
    )
    {
        this.objectiveId =
            objectiveId;

        this.description =
            description;

        completed = false;
    }

    // ==================================================
    // COMPLETE
    // ==================================================

    public bool Complete()
    {
        if (completed)
            return false;

        completed = true;

        return true;
    }

    // ==================================================
    // RESET
    // ==================================================

    public void ResetObjective()
    {
        completed = false;
    }

    // ==================================================
    // DESCRIPTION
    // ==================================================

    public void SetDescription(
        string newDescription
    )
    {
        description =
            newDescription;
    }
}

// ======================================================
// QUEST
// ======================================================

[Serializable]
public class QuestProgress
{
    [SerializeField]
    private QuestId questId;

    [SerializeField]
    private string title;

    [SerializeField]
    private QuestState state =
        QuestState.NotStarted;

    [SerializeField]
    private List<QuestObjectiveProgress> objectives =
        new List<QuestObjectiveProgress>();

    public QuestId QuestId =>
        questId;

    public string Title =>
        title;

    public QuestState State =>
        state;

    public IReadOnlyList<QuestObjectiveProgress> Objectives =>
        objectives;

    // ==================================================
    // CONSTRUCTOR
    // ==================================================

    public QuestProgress(
        QuestId questId,
        string title
    )
    {
        this.questId =
            questId;

        this.title =
            title;

        state =
            QuestState.NotStarted;

        objectives =
            new List<QuestObjectiveProgress>();
    }

    // ==================================================
    // TITLE
    // ==================================================

    public void SetTitle(
        string newTitle
    )
    {
        title =
            newTitle;
    }

    // ==================================================
    // GARANTE OBJETIVO
    // ==================================================

    public void EnsureObjective(
        QuestObjectiveId objectiveId,
        string description
    )
    {
        QuestObjectiveProgress existing =
            FindObjective(
                objectiveId
            );

        if (existing != null)
        {
            existing.SetDescription(
                description
            );

            return;
        }

        objectives.Add(
            new QuestObjectiveProgress(
                objectiveId,
                description
            )
        );
    }

    // ==================================================
    // PROCURA OBJETIVO
    // ==================================================

    public QuestObjectiveProgress FindObjective(
        QuestObjectiveId objectiveId
    )
    {
        foreach (
            QuestObjectiveProgress objective
            in objectives
        )
        {
            if (objective == null)
                continue;

            if (
                objective.ObjectiveId ==
                objectiveId
            )
            {
                return objective;
            }
        }

        return null;
    }

    // ==================================================
    // START
    // ==================================================

    public bool StartQuest()
    {
        if (
            state !=
            QuestState.NotStarted
        )
        {
            return false;
        }

        foreach (
            QuestObjectiveProgress objective
            in objectives
        )
        {
            objective?.ResetObjective();
        }

        state =
            QuestState.Active;

        return true;
    }

    // ==================================================
    // COMPLETE OBJECTIVE
    // ==================================================

    public bool CompleteObjective(
        QuestObjectiveId objectiveId
    )
    {
        if (
            state !=
            QuestState.Active
        )
        {
            return false;
        }

        QuestObjectiveProgress objective =
            FindObjective(
                objectiveId
            );

        if (objective == null)
            return false;

        bool changed =
            objective.Complete();

        if (!changed)
            return false;

        if (AreAllObjectivesComplete())
        {
            state =
                QuestState.ReadyToTurnIn;
        }

        return true;
    }

    // ==================================================
    // TODOS CONCLUÍDOS?
    // ==================================================

    public bool AreAllObjectivesComplete()
    {
        if (
            objectives == null ||
            objectives.Count == 0
        )
        {
            return false;
        }

        foreach (
            QuestObjectiveProgress objective
            in objectives
        )
        {
            if (objective == null)
                continue;

            if (!objective.Completed)
                return false;
        }

        return true;
    }

    // ==================================================
    // TURN IN
    // ==================================================

    public bool TurnInQuest()
    {
        if (
            state !=
            QuestState.ReadyToTurnIn
        )
        {
            return false;
        }

        state =
            QuestState.Completed;

        return true;
    }

    // ==================================================
    // RESET
    // ==================================================

    public void ResetQuest()
    {
        state =
            QuestState.NotStarted;

        foreach (
            QuestObjectiveProgress objective
            in objectives
        )
        {
            objective?.ResetObjective();
        }
    }
}

// ======================================================
// QUEST MANAGER
// ======================================================

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance
    {
        get;
        private set;
    }

    [Header("Quests")]
    [SerializeField]
    private List<QuestProgress> quests =
        new List<QuestProgress>();

    public event Action OnQuestUpdated;

    // ==================================================
    // COMPATIBILIDADE COM A QUEST 1 ANTIGA
    // ==================================================

    public QuestState SewerQuestState =>
        GetQuestState(
            QuestId.SewerInvestigation
        );

    public bool FoundDeadGuard =>
        IsObjectiveCompleted(
            QuestId.SewerInvestigation,
            QuestObjectiveId.FindDeadGuard
        );

    public bool DefeatedDungeonBoss =>
        IsObjectiveCompleted(
            QuestId.SewerInvestigation,
            QuestObjectiveId.DefeatBoss1
        );

    // ==================================================
    // AWAKE
    // ==================================================

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Destroy(
                gameObject
            );

            return;
        }

        Instance = this;

        DontDestroyOnLoad(
            gameObject
        );

        EnsureQuestDefinitions();
    }

    // ==================================================
    // ON VALIDATE
    // ==================================================

    private void OnValidate()
    {
        EnsureQuestDefinitions();
    }

    // ==================================================
    // DEFINIÇÕES DAS QUESTS
    // ==================================================

    private void EnsureQuestDefinitions()
    {
        if (quests == null)
        {
            quests =
                new List<QuestProgress>();
        }

        // ==============================================
        // QUEST 1
        // ==============================================

        QuestProgress quest1 =
            EnsureQuest(
                QuestId.SewerInvestigation,
                "Investigação nos Esgotos"
            );

        quest1.EnsureObjective(
            QuestObjectiveId.FindDeadGuard,
            "Descubra o que aconteceu com os guardas."
        );

        quest1.EnsureObjective(
            QuestObjectiveId.DefeatBoss1,
            "Investigue o que está acontecendo nos esgotos."
        );

        // ==============================================
        // QUEST 2
        // ==============================================

        QuestProgress quest2 =
            EnsureQuest(
                QuestId.DeeperIntoSewers,
                "Mais Fundo nos Esgotos"
            );

        quest2.EnsureObjective(
            QuestObjectiveId.DefeatBoss2,
            "Derrote o líder encontrado nas profundezas."
        );

        quest2.EnsureObjective(
            QuestObjectiveId.InterrogateBoss2,
            "Descubra por que os clãs de ratos se uniram."
        );

        // ==============================================
        // QUEST 3
        // ==============================================

        QuestProgress quest3 =
            EnsureQuest(
                QuestId.RatPriest,
                "O Rato Sacerdote"
            );

        quest3.EnsureObjective(
            QuestObjectiveId.DefeatRatPriest,
            "Derrote o Rato Sacerdote."
        );
    }

    // ==================================================
    // GARANTE QUEST
    // ==================================================

    private QuestProgress EnsureQuest(
        QuestId questId,
        string title
    )
    {
        QuestProgress existing =
            FindQuest(
                questId
            );

        if (existing != null)
        {
            existing.SetTitle(
                title
            );

            return existing;
        }

        QuestProgress newQuest =
            new QuestProgress(
                questId,
                title
            );

        quests.Add(
            newQuest
        );

        return newQuest;
    }

    // ==================================================
    // PROCURA QUEST
    // ==================================================

    public QuestProgress FindQuest(
        QuestId questId
    )
    {
        if (quests == null)
            return null;

        foreach (
            QuestProgress quest
            in quests
        )
        {
            if (quest == null)
                continue;

            if (
                quest.QuestId ==
                questId
            )
            {
                return quest;
            }
        }

        return null;
    }

    // ==================================================
    // GET STATE
    // ==================================================

    public QuestState GetQuestState(
        QuestId questId
    )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
        {
            return
                QuestState.NotStarted;
        }

        return quest.State;
    }

    // ==================================================
    // GET TITLE
    // ==================================================

    public string GetQuestTitle(
        QuestId questId
    )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
            return string.Empty;

        return quest.Title;
    }

    // ==================================================
    // GET OBJECTIVES
    // ==================================================

    public IReadOnlyList<QuestObjectiveProgress>
        GetQuestObjectives(
            QuestId questId
        )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
            return null;

        return quest.Objectives;
    }

    // ==================================================
    // OBJECTIVE COMPLETED?
    // ==================================================

    public bool IsObjectiveCompleted(
        QuestId questId,
        QuestObjectiveId objectiveId
    )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
            return false;

        QuestObjectiveProgress objective =
            quest.FindObjective(
                objectiveId
            );

        if (objective == null)
            return false;

        return objective.Completed;
    }

    // ==================================================
    // START QUEST
    // ==================================================

    public bool StartQuest(
        QuestId questId
    )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
        {
            Debug.LogError(
                $"QuestManager: Quest '{questId}' " +
                "não encontrada.",
                this
            );

            return false;
        }

        if (!CanStartQuest(questId))
        {
            Debug.LogWarning(
                $"QuestManager: Quest '{questId}' " +
                "ainda não pode ser iniciada.",
                this
            );

            return false;
        }

        bool started =
            quest.StartQuest();

        if (!started)
            return false;

        Debug.Log(
            $"QUEST INICIADA: {quest.Title}",
            this
        );

        NotifyQuestUpdated();

        return true;
    }

    // ==================================================
    // PODE COMEÇAR?
    // ==================================================

    public bool CanStartQuest(
        QuestId questId
    )
    {
        switch (questId)
        {
            case QuestId.SewerInvestigation:

                return true;

            case QuestId.DeeperIntoSewers:

                return
                    GetQuestState(
                        QuestId.SewerInvestigation
                    ) ==
                    QuestState.Completed;

            case QuestId.RatPriest:

                return
                    GetQuestState(
                        QuestId.DeeperIntoSewers
                    ) ==
                    QuestState.Completed;
        }

        return false;
    }

    // ==================================================
    // COMPLETE OBJECTIVE
    // ==================================================

    public bool CompleteObjective(
        QuestId questId,
        QuestObjectiveId objectiveId
    )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
        {
            Debug.LogError(
                $"QuestManager: Quest '{questId}' " +
                "não encontrada.",
                this
            );

            return false;
        }

        QuestObjectiveProgress objective =
            quest.FindObjective(
                objectiveId
            );

        if (objective == null)
        {
            Debug.LogError(
                $"QuestManager: objetivo '{objectiveId}' " +
                $"não existe na Quest '{questId}'.",
                this
            );

            return false;
        }

        bool completed =
            quest.CompleteObjective(
                objectiveId
            );

        if (!completed)
            return false;

        Debug.Log(
            "OBJETIVO CONCLUÍDO: " +
            objective.Description,
            this
        );

        if (
            quest.State ==
            QuestState.ReadyToTurnIn
        )
        {
            Debug.Log(
                $"QUEST PRONTA PARA ENTREGA: " +
                $"{quest.Title}",
                this
            );
        }

        NotifyQuestUpdated();

        return true;
    }

    // ==================================================
    // TURN IN QUEST
    // ==================================================

    public bool TurnInQuest(
        QuestId questId
    )
    {
        QuestProgress quest =
            FindQuest(
                questId
            );

        if (quest == null)
            return false;

        bool completed =
            quest.TurnInQuest();

        if (!completed)
            return false;

        Debug.Log(
            $"QUEST CONCLUÍDA: {quest.Title}",
            this
        );

        NotifyQuestUpdated();

        return true;
    }

    // ==================================================
    // COMPATIBILIDADE:
    // START QUEST 1
    // ==================================================

    public void StartSewerQuest()
    {
        StartQuest(
            QuestId.SewerInvestigation
        );
    }

    // ==================================================
    // COMPATIBILIDADE:
    // COMPLETE OBJECTIVE QUEST 1
    // ==================================================

    public void CompleteObjective(
        SewerQuestObjective objective
    )
    {
        switch (objective)
        {
            case SewerQuestObjective.FindDeadGuard:

                CompleteObjective(
                    QuestId.SewerInvestigation,
                    QuestObjectiveId.FindDeadGuard
                );

                break;

            case SewerQuestObjective.DefeatDungeonBoss:

                CompleteObjective(
                    QuestId.SewerInvestigation,
                    QuestObjectiveId.DefeatBoss1
                );

                break;
        }
    }

    // ==================================================
    // COMPATIBILIDADE:
    // TURN IN QUEST 1
    // ==================================================

    public bool TurnInSewerQuest()
    {
        return TurnInQuest(
            QuestId.SewerInvestigation
        );
    }

    // ==================================================
    // EVENT
    // ==================================================

    private void NotifyQuestUpdated()
    {
        OnQuestUpdated?.Invoke();
    }

    // ==================================================
    // DEBUG - QUEST 1
    // ==================================================

    [ContextMenu("DEBUG - Start Quest")]
    private void DebugStartQuest1()
    {
        StartQuest(
            QuestId.SewerInvestigation
        );
    }

    [ContextMenu("DEBUG - Find Dead Guard")]
    private void DebugFindDeadGuard()
    {
        CompleteObjective(
            QuestId.SewerInvestigation,
            QuestObjectiveId.FindDeadGuard
        );
    }

    [ContextMenu("DEBUG - Defeat Boss")]
    private void DebugDefeatBoss1()
    {
        CompleteObjective(
            QuestId.SewerInvestigation,
            QuestObjectiveId.DefeatBoss1
        );
    }

    [ContextMenu("DEBUG - Turn In Quest")]
    private void DebugTurnInQuest1()
    {
        TurnInQuest(
            QuestId.SewerInvestigation
        );
    }

    // ==================================================
    // DEBUG - RESET
    // ==================================================

    [ContextMenu("DEBUG - Reset All Quests")]
    private void DebugResetAllQuests()
    {
        foreach (
            QuestProgress quest
            in quests
        )
        {
            quest?.ResetQuest();
        }

        Debug.Log(
            "Todas as quests foram resetadas.",
            this
        );

        NotifyQuestUpdated();
    }
}