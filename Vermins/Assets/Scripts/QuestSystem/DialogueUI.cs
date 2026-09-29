using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("References")]
    [SerializeField]
    private GameObject dialoguePanel;

    [SerializeField]
    private TMP_Text speakerNameText;

    [SerializeField]
    private TMP_Text dialogueText;

    [SerializeField]
    private Button closeButton;

    public bool IsOpen =>
        dialoguePanel != null &&
        dialoguePanel.activeSelf;

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

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                HideDialogue
            );
        }

        HideDialogue();
    }

    // ==================================================
    // SHOW
    // ==================================================

    public void ShowDialogue(
        string speakerName,
        string text
    )
    {
        if (dialoguePanel == null)
        {
            Debug.LogError(
                "DialogueUI: Dialogue Panel não foi definido.",
                this
            );

            return;
        }

        if (speakerNameText != null)
        {
            speakerNameText.text =
                speakerName;
        }

        if (dialogueText != null)
        {
            dialogueText.text =
                text;
        }

        dialoguePanel.SetActive(
            true
        );
    }

    // ==================================================
    // HIDE
    // ==================================================

    public void HideDialogue()
    {
        if (dialoguePanel == null)
            return;

        dialoguePanel.SetActive(
            false
        );
    }

    // ==================================================
    // DESTROY
    // ==================================================

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                HideDialogue
            );
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}