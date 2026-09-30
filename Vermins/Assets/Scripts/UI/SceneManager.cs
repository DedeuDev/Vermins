using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadGame()
    {
        // Ian: o jogo novo comeca na cidade, onde o guarda da a quest e o
        // bueiro leva pra dungeon. Direto na dungeon a HUBCity nunca
        // aparecia e a quest nao tinha como comecar.
        SceneManager.LoadScene("HUBCity");
    }

    public void SaveGame()
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.SaveGame();
        }
    }

    public void LoadSavedGame()
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.LoadGame();
        }
    }

    public void OpenOptions()
    {
        SceneManager.LoadScene("Options");
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}