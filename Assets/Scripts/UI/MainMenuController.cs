using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "LoadingScene";

    public void StartGame()
    {
        GameLaunchState.LoadFromSave = false;
        SceneManager.LoadScene(loadingSceneName);
    }

    public void LoadGame()
    {
        if (!GameSaveSystem.HasSave())
        {
            Debug.Log("No save found.");
            return;
        }

        GameLaunchState.LoadFromSave = true;
        SceneManager.LoadScene(loadingSceneName);
    }
}