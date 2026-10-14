using UnityEngine;

public class DungeonEntranceInteractable :
    MonoBehaviour,
    IInteractable
{
    [Header("Dialogue")]
    [SerializeField]
    private string interactionName =
        "Entrada dos Esgotos";

    [TextArea(2, 5)]
    [SerializeField]
    private string lockedMessage =
        "Não há motivo para entrar nos esgotos agora. " +
        "Talvez o Capitão da Guarda tenha alguma informação.";

    // ==================================================
    // INTERACT
    // ==================================================

    [ContextMenu("Interact")]
    public void Interact()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError(
                "DungeonEntranceInteractable: " +
                "QuestManager não encontrado.",
                this
            );

            return;
        }

        // ========================================
        // AINDA NÃO COMEÇOU A CAMPANHA
        // ========================================

        QuestState quest1State =
            QuestManager.Instance.GetQuestState(
                QuestId.SewerInvestigation
            );

        if (
            quest1State ==
            QuestState.NotStarted
        )
        {
            ShowLockedMessage();

            return;
        }

        // ========================================
        // ABRE SELETOR
        // ========================================

        if (DungeonSelectionUI.Instance == null)
        {
            Debug.LogError(
                "DungeonEntranceInteractable: " +
                "DungeonSelectionUI não encontrado.",
                this
            );

            return;
        }

        DungeonSelectionUI.Instance.Open();
    }

    // ==================================================
    // LOCKED MESSAGE
    // ==================================================

    private void ShowLockedMessage()
    {
        if (DialogueUI.Instance != null)
        {
            DialogueUI.Instance.ShowDialogue(
                interactionName,
                lockedMessage
            );

            return;
        }

        Debug.Log(
            $"{interactionName}: {lockedMessage}",
            this
        );
    }
}