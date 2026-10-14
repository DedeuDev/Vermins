using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestUI : MonoBehaviour
{
    public static QuestUI Instance
    {
        get;
        private set;
    }

    [Header("References")]
    [SerializeField]
    private GameObject questPanel;

    [SerializeField]
    private TMP_Text questTitleText;

    [Header("Objective Texts")]
    [SerializeField]
    private TMP_Text objectiveText1;

    [SerializeField]
    private TMP_Text objectiveText2;

    [SerializeField]
    private TMP_Text returnObjectiveText;

    [Header("Settings")]
    [SerializeField]
    private bool persistBetweenScenes = true;

    private bool subscribedToQuestManager;

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

        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(
                gameObject
            );
        }
    }

    // ==================================================
    // START
    // ==================================================

    private void Start()
    {
        TrySubscribeToQuestManager();

        RefreshUI();
    }

    // ==================================================
    // UPDATE
    // ==================================================

    private void Update()
    {
        /*
         * Caso a UI inicie antes do QuestManager,
         * continua tentando até encontrá-lo.
         */
        if (!subscribedToQuestManager)
        {
            TrySubscribeToQuestManager();
        }
    }

    // ==================================================
    // SUBSCRIBE
    // ==================================================

    private void TrySubscribeToQuestManager()
    {
        if (subscribedToQuestManager)
            return;

        if (QuestManager.Instance == null)
            return;

        QuestManager.Instance.OnQuestUpdated +=
            RefreshUI;

        subscribedToQuestManager = true;

        RefreshUI();
    }

    // ==================================================
    // REFRESH
    // ==================================================

    public void RefreshUI()
    {
        if (questPanel == null)
            return;

        if (QuestManager.Instance == null)
        {
            HideQuestPanel();
            return;
        }

        QuestProgress currentQuest =
            FindCurrentQuest();

        /*
         * Nenhuma quest está ativa ou pronta
         * para entrega.
         */
        if (currentQuest == null)
        {
            HideQuestPanel();
            return;
        }

        questPanel.SetActive(
            true
        );

        UpdateTitle(
            currentQuest
        );

        UpdateObjectives(
            currentQuest
        );

        UpdateReturnObjective(
            currentQuest
        );
    }

    // ==================================================
    // PROCURA A QUEST ATUAL
    // ==================================================

    private QuestProgress FindCurrentQuest()
    {
        /*
         * Ordem da campanha.
         *
         * Procuramos uma quest Active ou
         * ReadyToTurnIn.
         */

        QuestId[] questOrder =
        {
            QuestId.SewerInvestigation,
            QuestId.DeeperIntoSewers,
            QuestId.RatPriest
        };

        foreach (
            QuestId questId
            in questOrder
        )
        {
            QuestProgress quest =
                QuestManager.Instance.FindQuest(
                    questId
                );

            if (quest == null)
                continue;

            if (
                quest.State ==
                QuestState.Active
            )
            {
                return quest;
            }

            if (
                quest.State ==
                QuestState.ReadyToTurnIn
            )
            {
                return quest;
            }
        }

        return null;
    }

    // ==================================================
    // TITLE
    // ==================================================

    private void UpdateTitle(
        QuestProgress quest
    )
    {
        if (questTitleText == null)
            return;

        questTitleText.text =
            quest.Title;
    }

    // ==================================================
    // OBJECTIVES
    // ==================================================

    private void UpdateObjectives(
        QuestProgress quest
    )
    {
        ClearObjectiveTexts();

        IReadOnlyList<QuestObjectiveProgress> objectives =
            quest.Objectives;

        if (objectives == null)
            return;

        if (objectives.Count >= 1)
        {
            SetObjectiveText(
                objectiveText1,
                objectives[0]
            );
        }

        if (objectives.Count >= 2)
        {
            SetObjectiveText(
                objectiveText2,
                objectives[1]
            );
        }
    }

    // ==================================================
    // SET OBJECTIVE TEXT
    // ==================================================

    private void SetObjectiveText(
        TMP_Text textComponent,
        QuestObjectiveProgress objective
    )
    {
        if (textComponent == null)
            return;

        if (objective == null)
        {
            textComponent.gameObject.SetActive(
                false
            );

            return;
        }

        textComponent.gameObject.SetActive(
            true
        );

        string checkbox =
            objective.Completed
                ? "[X]"
                : "[ ]";

        textComponent.text =
            $"{checkbox} {objective.Description}";
    }

    // ==================================================
    // LIMPA OBJECTIVES
    // ==================================================

    private void ClearObjectiveTexts()
    {
        if (objectiveText1 != null)
        {
            objectiveText1.text =
                string.Empty;

            objectiveText1.gameObject.SetActive(
                false
            );
        }

        if (objectiveText2 != null)
        {
            objectiveText2.text =
                string.Empty;

            objectiveText2.gameObject.SetActive(
                false
            );
        }
    }

    // ==================================================
    // RETURN TO CAPTAIN
    // ==================================================

    private void UpdateReturnObjective(
        QuestProgress quest
    )
    {
        if (returnObjectiveText == null)
            return;

        bool readyToTurnIn =
            quest.State ==
            QuestState.ReadyToTurnIn;

        returnObjectiveText.gameObject.SetActive(
            readyToTurnIn
        );

        if (readyToTurnIn)
        {
            returnObjectiveText.text =
                "Volte ao Capitão da Guarda.";
        }
    }

    // ==================================================
    // HIDE
    // ==================================================

    private void HideQuestPanel()
    {
        questPanel.SetActive(
            false
        );
    }

    // ==================================================
    // DESTROY
    // ==================================================

    private void OnDestroy()
    {
        if (
            subscribedToQuestManager &&
            QuestManager.Instance != null
        )
        {
            QuestManager.Instance.OnQuestUpdated -=
                RefreshUI;
        }

        subscribedToQuestManager = false;

        if (Instance == this)
        {
            Instance = null;
        }
    }
}