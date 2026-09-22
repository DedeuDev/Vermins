using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadGame()
    {
        SceneManager.LoadScene("DungeonComPlayer");
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