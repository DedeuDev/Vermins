using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    public GameObject optionsMenu;

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // o optionsMenu nao existe na cena da dungeon, so na do menu principal.
            // sem essa checagem o ESC estourava aqui e o menu de pausa nunca abria.
            if (optionsMenu != null && optionsMenu.activeSelf)
            {
                BackToPause();
            }
            else if (pauseMenu.activeSelf)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    void Pause()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OpenOptions()
    {
        // saio antes de esconder o menu de pausa. se escondesse primeiro e estourasse
        // depois, o jogo ficava com timeScale 0 e nenhum botao na tela pra sair.
        if (optionsMenu == null)
        {
            Debug.LogWarning("Nao tem painel de opcoes nessa cena, o botao Options nao faz nada.", this);
            return;
        }

        pauseMenu.SetActive(false);
        optionsMenu.SetActive(true);
    }

    public void BackToPause()
    {
        if (optionsMenu != null)
        {
            optionsMenu.SetActive(false);
        }

        pauseMenu.SetActive(true);
    }
}
