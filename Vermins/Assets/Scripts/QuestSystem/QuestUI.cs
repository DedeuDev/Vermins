using TMPro;
using UnityEngine;

public class QuestUI : MonoBehaviour
{
    public static QuestUI Instance { get; private set; }

    [Header("References")]
    [SerializeField]
    private GameObject questPanel;

    [SerializeField]
    private TMP_Text questTitleText;

    [SerializeField]
    private TMP_Text guardObjectiveText;

    [SerializeField]
    private TMP_Text bossObjectiveText;

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
            Destroy(gameObject);
            return;
        }

        Instance = this;

        /*
         * A UI da quest precisa acompanhar o jogador
         * da HUB até a dungeon e depois voltar para a HUB.
         */
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
         * Isso resolve o caso em que o QuestUI
         * iniciou antes do QuestManager.
         *
         * Assim que ele existir, fazemos a inscrição.
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
    // ATUALIZA UI
    // ==================================================

    public void RefreshUI()
    {
        if (questPanel == null)
            return;

        /*
         * Se ainda não existe QuestManager,
         * não temos nada para mostrar.
         */
        if (QuestManager.Instance == null)
        {
            questPanel.SetActive(
                false
            );

            return;
        }

        QuestState state =
            QuestManager.Instance.SewerQuestState;

        // ========================================
        // QUEST NÃO COMEÇOU
        // ========================================

        if (state == QuestState.NotStarted)
        {
            questPanel.SetActive(
                false
            );

            return;
        }

        // ========================================
        // QUEST JÁ FOI ENTREGUE
        // ========================================

        if (state == QuestState.Completed)
        {
            questPanel.SetActive(
                false
            );

            return;
        }

        // ========================================
        // QUEST ATIVA
        // ========================================

        questPanel.SetActive(
            true
        );

        if (questTitleText != null)
        {
            questTitleText.text =
                "Investigação nos Esgotos";
        }

        UpdateGuardObjective();

        UpdateBossObjective();

        UpdateReturnObjective();
    }

    // ==================================================
    // GUARDA MORTO
    // ==================================================

    private void UpdateGuardObjective()
    {
        if (guardObjectiveText == null)
            return;

        bool completed =
            QuestManager.Instance.FoundDeadGuard;

        if (completed)
        {
            guardObjectiveText.text =
                "[X] Descubra o que aconteceu " +
                "com os guardas.";
        }
        else
        {
            guardObjectiveText.text =
                "[ ] Descubra o que aconteceu " +
                "com os guardas.";
        }
    }

    // ==================================================
    // BOSS
    // ==================================================

    private void UpdateBossObjective()
    {
        if (bossObjectiveText == null)
            return;

        bool completed =
            QuestManager.Instance.DefeatedDungeonBoss;

        if (completed)
        {
            bossObjectiveText.text =
                "[X] Investigue o que está " +
                "acontecendo nos esgotos.";
        }
        else
        {
            bossObjectiveText.text =
                "[ ] Investigue o que está " +
                "acontecendo nos esgotos.";
        }
    }

    // ==================================================
    // VOLTAR AO CAPITÃO
    // ==================================================

    private void UpdateReturnObjective()
    {
        if (returnObjectiveText == null)
            return;

        bool readyToTurnIn =
            QuestManager.Instance.SewerQuestState ==
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