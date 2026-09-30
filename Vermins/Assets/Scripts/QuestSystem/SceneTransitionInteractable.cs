using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionInteractable :
    MonoBehaviour,
    IInteractable
{
    [Header("Scene")]
    [Tooltip(
        "Nome exato da Scene que será carregada."
    )]
    [SerializeField]
    private string destinationSceneName;

    [Header("Quest Requirement")]
    [Tooltip(
        "Se marcado, o jogador só poderá usar esta " +
        "passagem depois que a quest dos esgotos começar."
    )]
    [SerializeField]
    private bool requireSewerQuestStarted = true;

    [Header("Dialogue")]
    [SerializeField]
    private string interactionName =
        "Entrada dos Esgotos";

    [TextArea(2, 5)]
    [SerializeField]
    private string lockedMessage =
        "Não há motivo para entrar nos esgotos agora.";

    [Header("Debug")]
    [SerializeField]
    private bool logTransition = true;

    // ==================================================
    // INTERACT
    // ==================================================

    [ContextMenu("Interact")]
    public void Interact()
    {
        if (string.IsNullOrWhiteSpace(destinationSceneName))
        {
            Debug.LogError(
                "SceneTransitionInteractable: " +
                "nenhuma Scene de destino foi definida.",
                this
            );

            return;
        }

        // ========================================
        // QUEST
        // ========================================

        if (
            requireSewerQuestStarted &&
            !CanEnterSewers()
        )
        {
            ShowLockedMessage();

            return;
        }

        // ========================================
        // VERIFICA SE A SCENE EXISTE
        // ========================================

        if (
            !Application.CanStreamedLevelBeLoaded(
                destinationSceneName
            )
        )
        {
            Debug.LogError(
                "SceneTransitionInteractable: " +
                $"a Scene '{destinationSceneName}' " +
                "não pode ser carregada. " +
                "Confira se ela foi adicionada à lista " +
                "de Scenes do Build.",
                this
            );

            return;
        }

        // ========================================
        // LOG
        // ========================================

        if (logTransition)
        {
            Debug.Log(
                $"Carregando Scene: {destinationSceneName}",
                this
            );
        }

        // ========================================
        // LOAD
        // ========================================

        SceneManager.LoadScene(
            destinationSceneName
        );
    }

    // ==================================================
    // PODE ENTRAR?
    // ==================================================

    private bool CanEnterSewers()
    {
        if (QuestManager.Instance == null)
            return false;

        QuestState state =
            QuestManager.Instance.SewerQuestState;

        return
            state == QuestState.Active ||
            state == QuestState.ReadyToTurnIn ||
            state == QuestState.Completed;
    }

    // ==================================================
    // MENSAGEM BLOQUEADA
    // ==================================================

    private void ShowLockedMessage()
    {
        if (DialogueUI.Instance != null)
        {
            DialogueUI.Instance.ShowDialogue(
                interactionName,
                lockedMessage
            );
        }
        else
        {
            Debug.Log(
                $"{interactionName}: {lockedMessage}",
                this
            );
        }
    }
}