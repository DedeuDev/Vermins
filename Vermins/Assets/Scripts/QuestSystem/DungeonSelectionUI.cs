using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DungeonSelectionUI : MonoBehaviour
{
    public static DungeonSelectionUI Instance
    {
        get;
        private set;
    }

    [Header("Panel")]
    [SerializeField]
    private GameObject selectionPanel;

    [Header("Dungeon 1")]
    [SerializeField]
    private Button dungeon1Button;

    [SerializeField]
    private TMP_Text dungeon1ButtonText;

    [SerializeField]
    private string dungeon1SceneName =
        "Teste";

    [SerializeField]
    private string dungeon1DisplayName =
        "Esgotos Superiores";

    [Header("Dungeon 2")]
    [SerializeField]
    private Button dungeon2Button;

    [SerializeField]
    private TMP_Text dungeon2ButtonText;

    [SerializeField]
    private string dungeon2SceneName =
        "Dungeon2";

    [SerializeField]
    private string dungeon2DisplayName =
        "Profundezas dos Esgotos";

    [Header("Dungeon 3")]
    [SerializeField]
    private Button dungeon3Button;

    [SerializeField]
    private TMP_Text dungeon3ButtonText;

    [SerializeField]
    private string dungeon3SceneName =
        "Dungeon3";

    [SerializeField]
    private string dungeon3DisplayName =
        "Templo Subterrâneo";

    [Header("Close")]
    [SerializeField]
    private Button closeButton;

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

        RegisterButtons();

        Close();
    }

    // ==================================================
    // BOTÕES
    // ==================================================

    private void RegisterButtons()
    {
        if (dungeon1Button != null)
        {
            dungeon1Button.onClick.AddListener(
                LoadDungeon1
            );
        }

        if (dungeon2Button != null)
        {
            dungeon2Button.onClick.AddListener(
                LoadDungeon2
            );
        }

        if (dungeon3Button != null)
        {
            dungeon3Button.onClick.AddListener(
                LoadDungeon3
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                Close
            );
        }
    }

    // ==================================================
    // OPEN
    // ==================================================

    public void Open()
    {
        if (selectionPanel == null)
        {
            Debug.LogError(
                "DungeonSelectionUI: " +
                "Selection Panel não foi definido.",
                this
            );

            return;
        }

        RefreshDungeonButtons();

        selectionPanel.SetActive(
            true
        );
    }

    // ==================================================
    // CLOSE
    // ==================================================

    public void Close()
    {
        if (selectionPanel == null)
            return;

        selectionPanel.SetActive(
            false
        );
    }

    // ==================================================
    // REFRESH
    // ==================================================

    private void RefreshDungeonButtons()
    {
        if (QuestManager.Instance == null)
        {
            SetDungeonButton(
                dungeon1Button,
                dungeon1ButtonText,
                dungeon1DisplayName,
                false,
                false,
                dungeon1SceneName
            );

            SetDungeonButton(
                dungeon2Button,
                dungeon2ButtonText,
                dungeon2DisplayName,
                false,
                false,
                dungeon2SceneName
            );

            SetDungeonButton(
                dungeon3Button,
                dungeon3ButtonText,
                dungeon3DisplayName,
                false,
                false,
                dungeon3SceneName
            );

            return;
        }

        QuestState quest1State =
            QuestManager.Instance.GetQuestState(
                QuestId.SewerInvestigation
            );

        QuestState quest2State =
            QuestManager.Instance.GetQuestState(
                QuestId.DeeperIntoSewers
            );

        QuestState quest3State =
            QuestManager.Instance.GetQuestState(
                QuestId.RatPriest
            );

        // ========================================
        // DESBLOQUEIOS
        // ========================================

        bool dungeon1Unlocked =
            quest1State !=
            QuestState.NotStarted;

        bool dungeon2Unlocked =
            quest1State ==
            QuestState.Completed;

        bool dungeon3Unlocked =
            quest2State ==
            QuestState.Completed;

        // ========================================
        // MISSÃO ATUAL
        // ========================================

        bool dungeon1CurrentQuest =
            quest1State ==
            QuestState.Active ||
            quest1State ==
            QuestState.ReadyToTurnIn;

        bool dungeon2CurrentQuest =
            quest2State ==
            QuestState.Active ||
            quest2State ==
            QuestState.ReadyToTurnIn;

        bool dungeon3CurrentQuest =
            quest3State ==
            QuestState.Active ||
            quest3State ==
            QuestState.ReadyToTurnIn;

        // ========================================
        // BUTTONS
        // ========================================

        SetDungeonButton(
            dungeon1Button,
            dungeon1ButtonText,
            dungeon1DisplayName,
            dungeon1Unlocked,
            dungeon1CurrentQuest,
            dungeon1SceneName
        );

        SetDungeonButton(
            dungeon2Button,
            dungeon2ButtonText,
            dungeon2DisplayName,
            dungeon2Unlocked,
            dungeon2CurrentQuest,
            dungeon2SceneName
        );

        SetDungeonButton(
            dungeon3Button,
            dungeon3ButtonText,
            dungeon3DisplayName,
            dungeon3Unlocked,
            dungeon3CurrentQuest,
            dungeon3SceneName
        );
    }

    // ==================================================
    // CONFIGURA UM BOTÃO
    // ==================================================

    private void SetDungeonButton(
        Button button,
        TMP_Text buttonText,
        string displayName,
        bool unlocked,
        bool currentQuest,
        string sceneName
    )
    {
        if (button == null)
            return;

        bool sceneAvailable =
            IsSceneAvailable(
                sceneName
            );

        button.interactable =
            unlocked &&
            sceneAvailable;

        if (buttonText == null)
            return;

        if (!unlocked)
        {
            buttonText.text =
                displayName +
                "\n[BLOQUEADO]";

            return;
        }

        if (!sceneAvailable)
        {
            buttonText.text =
                displayName +
                "\n[CENA NÃO CONFIGURADA]";

            return;
        }

        if (currentQuest)
        {
            buttonText.text =
                displayName +
                "\n[MISSÃO ATUAL]";

            return;
        }

        buttonText.text =
            displayName;
    }

    // ==================================================
    // SCENE DISPONÍVEL?
    // ==================================================

    private bool IsSceneAvailable(
        string sceneName
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                sceneName
            )
        )
        {
            return false;
        }

        return
            Application.CanStreamedLevelBeLoaded(
                sceneName
            );
    }

    // ==================================================
    // DUNGEON 1
    // ==================================================

    private void LoadDungeon1()
    {
        LoadDungeon(
            dungeon1SceneName
        );
    }

    // ==================================================
    // DUNGEON 2
    // ==================================================

    private void LoadDungeon2()
    {
        LoadDungeon(
            dungeon2SceneName
        );
    }

    // ==================================================
    // DUNGEON 3
    // ==================================================

    private void LoadDungeon3()
    {
        LoadDungeon(
            dungeon3SceneName
        );
    }

    // ==================================================
    // LOAD
    // ==================================================

    private void LoadDungeon(
        string sceneName
    )
    {
        if (!IsSceneAvailable(sceneName))
        {
            Debug.LogError(
                "DungeonSelectionUI: " +
                $"a Scene '{sceneName}' " +
                "não pode ser carregada.",
                this
            );

            return;
        }

        Close();

        // Ian: guarda vida, pocoes, ouro e joias do Player antes de trocar
        // de cena, igual o SceneTransitionInteractable faz. Sem isso o
        // Player da dungeon nasce zerado: medi saindo da cidade com 40 de
        // vida, 3 pocoes, 37 de ouro e 2 joias e chegando com 100, 0, 0, 0.
        CrossSceneState.Guardar();

        SceneManager.LoadScene(
            sceneName
        );
    }

    // ==================================================
    // DESTROY
    // ==================================================

    private void OnDestroy()
    {
        if (dungeon1Button != null)
        {
            dungeon1Button.onClick.RemoveListener(
                LoadDungeon1
            );
        }

        if (dungeon2Button != null)
        {
            dungeon2Button.onClick.RemoveListener(
                LoadDungeon2
            );
        }

        if (dungeon3Button != null)
        {
            dungeon3Button.onClick.RemoveListener(
                LoadDungeon3
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                Close
            );
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}