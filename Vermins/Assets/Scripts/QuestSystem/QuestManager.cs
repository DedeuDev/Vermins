using System;
using UnityEngine;

public enum QuestState
{
    NotStarted,
    Active,
    ReadyToTurnIn,
    Completed
}

public enum SewerQuestObjective
{
    FindDeadGuard,
    DefeatDungeonBoss
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Sewer Quest")]
    [SerializeField]
    private QuestState sewerQuestState =
        QuestState.NotStarted;

    [Header("Objectives")]
    [SerializeField]
    private bool foundDeadGuard = false;

    [SerializeField]
    private bool defeatedDungeonBoss = false;

    public QuestState SewerQuestState =>
        sewerQuestState;

    public bool FoundDeadGuard =>
        foundDeadGuard;

    public bool DefeatedDungeonBoss =>
        defeatedDungeonBoss;

    public event Action OnQuestUpdated;

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
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(
            gameObject
        );
    }

    // ==================================================
    // INICIAR QUEST
    // ==================================================

    public void StartSewerQuest()
    {
        if (
            sewerQuestState !=
            QuestState.NotStarted
        )
        {
            return;
        }

        sewerQuestState =
            QuestState.Active;

        foundDeadGuard = false;
        defeatedDungeonBoss = false;

        Debug.Log(
            "QUEST INICIADA: " +
            "Investigue os esgotos."
        );

        NotifyQuestUpdated();
    }

    // ==================================================
    // COMPLETAR OBJETIVO
    // ==================================================

    public void CompleteObjective(
        SewerQuestObjective objective
    )
    {
        /*
         * Objetivos só contam enquanto
         * a quest estiver ativa.
         */
        if (
            sewerQuestState !=
            QuestState.Active
        )
        {
            return;
        }

        switch (objective)
        {
            case SewerQuestObjective.FindDeadGuard:

                if (foundDeadGuard)
                    return;

                foundDeadGuard = true;

                Debug.Log(
                    "OBJETIVO CONCLUÍDO: " +
                    "Descubra o que aconteceu " +
                    "com os guardas."
                );

                break;

            case SewerQuestObjective.DefeatDungeonBoss:

                if (defeatedDungeonBoss)
                    return;

                defeatedDungeonBoss = true;

                Debug.Log(
                    "OBJETIVO CONCLUÍDO: " +
                    "Descubra o que está " +
                    "acontecendo nos esgotos."
                );

                break;
        }

        CheckQuestCompletion();

        NotifyQuestUpdated();
    }

    // ==================================================
    // VERIFICA SE TODOS OS OBJETIVOS
    // FORAM CONCLUÍDOS
    // ==================================================

    private void CheckQuestCompletion()
    {
        if (
            !foundDeadGuard ||
            !defeatedDungeonBoss
        )
        {
            return;
        }

        sewerQuestState =
            QuestState.ReadyToTurnIn;

        Debug.Log(
            "QUEST PRONTA PARA ENTREGA: " +
            "Volte ao Capitão da Guarda."
        );
    }

    // ==================================================
    // ENTREGAR QUEST
    // ==================================================

    public bool TurnInSewerQuest()
    {
        if (
            sewerQuestState !=
            QuestState.ReadyToTurnIn
        )
        {
            return false;
        }

        sewerQuestState =
            QuestState.Completed;

        Debug.Log(
            "QUEST CONCLUÍDA: " +
            "Investigação dos esgotos."
        );

        NotifyQuestUpdated();

        return true;
    }

    // ==================================================
    // EVENTO
    // ==================================================

    private void NotifyQuestUpdated()
    {
        OnQuestUpdated?.Invoke();
    }

    // ==================================================
    // DEBUG
    // ==================================================

    [ContextMenu("DEBUG - Start Quest")]
    private void DebugStartQuest()
    {
        StartSewerQuest();
    }

    [ContextMenu("DEBUG - Find Dead Guard")]
    private void DebugFindDeadGuard()
    {
        CompleteObjective(
            SewerQuestObjective.FindDeadGuard
        );
    }

    [ContextMenu("DEBUG - Defeat Boss")]
    private void DebugDefeatBoss()
    {
        CompleteObjective(
            SewerQuestObjective.DefeatDungeonBoss
        );
    }

    [ContextMenu("DEBUG - Turn In Quest")]
    private void DebugTurnInQuest()
    {
        TurnInSewerQuest();
    }

    [ContextMenu("DEBUG - Reset Quest")]
    private void DebugResetQuest()
    {
        sewerQuestState =
            QuestState.NotStarted;

        foundDeadGuard = false;
        defeatedDungeonBoss = false;

        Debug.Log(
            "Quest resetada."
        );

        NotifyQuestUpdated();
    }
}