using UnityEngine;
using UnityEngine.InputSystem;

// Deixo a pausa sem save nem troca de cena para isolar os testes.
public sealed class SandboxPauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private SandboxTools tools;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (panel.activeSelf) Resume();
        else
        {
            panel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void Resume()
    {
        panel.SetActive(false);
        Time.timeScale = tools.SlowMotion ? 0.25f : 1f;
    }
}
