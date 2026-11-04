using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    // Ian: a ordem da campanha virou campo, porque agora duas
    // funcoes percorrem ela.
    private static readonly QuestId[] QuestOrder =
    {
        QuestId.SewerInvestigation,
        QuestId.DeeperIntoSewers,
        QuestId.RatPriest
    };

    // Ian: o objetivo da vez fica amarelo e o feito fica cinza e
    // riscado. Antes so o [ ] virava [X], e os dois brigavam pela
    // atencao do jogador.
    private const string CurrentColor =
        "<color=#FFD24D>";

    private const string DoneColor =
        "<color=#8C8C8C>";

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

        // Ian: escuto a troca de cena para esconder o painel no MainMenu e em
        // Options e mostra-lo de novo ao voltar para o jogo.
        SceneManager.sceneLoaded +=
            HandleSceneLoaded;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode
    )
    {
        RefreshUI();
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

        // Ian: o Canvas persiste por DontDestroyOnLoad, entao escondo a HUD
        // da quest fora das cenas de jogo (MainMenu e Options).
        string activeScene =
            SceneManager.GetActiveScene().name;

        if (
            activeScene == "MainMenu" ||
            activeScene == "Options"
        )
        {
            HideQuestPanel();
            return;
        }

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
            // Ian: antes o painel sumia aqui, justo antes da
            // primeira quest e entre uma e outra, quando o jogador
            // precisa saber que tem que voltar ao Capitao. Agora so
            // some quando nao sobra quest nenhuma.
            QuestProgress nextQuest =
                FindNextQuestToStart();

            if (nextQuest == null)
            {
                HideQuestPanel();
                return;
            }

            ShowTalkToCaptain(
                nextQuest
            );

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

        foreach (
            QuestId questId
            in QuestOrder
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
    // Ian: PROXIMA QUEST A PEGAR
    // ==================================================

    private QuestProgress FindNextQuestToStart()
    {
        foreach (
            QuestId questId
            in QuestOrder
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
                QuestState.NotStarted &&
                QuestManager.Instance.CanStartQuest(
                    questId
                )
            )
            {
                return quest;
            }
        }

        return null;
    }

    // ==================================================
    // Ian: FALE COM O CAPITAO
    // ==================================================

    private void ShowTalkToCaptain(
        QuestProgress nextQuest
    )
    {
        questPanel.SetActive(
            true
        );

        UpdateTitle(
            nextQuest
        );

        ClearObjectiveTexts();

        if (objectiveText1 != null)
        {
            objectiveText1.gameObject.SetActive(
                true
            );

            objectiveText1.text =
                CurrentColor +
                "[ ] Fale com o Capitão da Guarda na cidade." +
                "</color>";
        }

        if (returnObjectiveText != null)
        {
            returnObjectiveText.gameObject.SetActive(
                false
            );
        }
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

        // Ian: o da vez e o primeiro que falta, na ordem da lista.
        QuestObjectiveProgress currentObjective =
            null;

        foreach (
            QuestObjectiveProgress objective
            in objectives
        )
        {
            if (
                objective != null &&
                !objective.Completed
            )
            {
                currentObjective =
                    objective;

                break;
            }
        }

        if (objectives.Count >= 1)
        {
            SetObjectiveText(
                objectiveText1,
                objectives[0],
                objectives[0] == currentObjective
            );
        }

        if (objectives.Count >= 2)
        {
            SetObjectiveText(
                objectiveText2,
                objectives[1],
                objectives[1] == currentObjective
            );
        }
    }

    // ==================================================
    // SET OBJECTIVE TEXT
    // ==================================================

    private void SetObjectiveText(
        TMP_Text textComponent,
        QuestObjectiveProgress objective,
        bool isCurrent
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

        // Ian: so remonta quando a quest muda, nunca por frame.
        if (objective.Completed)
        {
            textComponent.text =
                $"{DoneColor}<s>[X] {objective.Description}</s></color>";
        }
        else if (isCurrent)
        {
            textComponent.text =
                $"{CurrentColor}[ ] {objective.Description}</color>";
        }
        else
        {
            textComponent.text =
                $"[ ] {objective.Description}";
        }
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
            // Ian: amarelo como o objetivo da vez, e com o lugar.
            returnObjectiveText.text =
                CurrentColor +
                "[ ] Volte ao Capitão da Guarda na cidade." +
                "</color>";
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
        SceneManager.sceneLoaded -=
            HandleSceneLoaded;

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