using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private EventPopupUI eventPopupUI;
    [SerializeField] private Sprite skullSprite;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool gameOverTriggered;

    public bool IsGameOverTriggered => gameOverTriggered;

    private void Awake()
    {
        if (!eventPopupUI)
            eventPopupUI = FindFirstObjectByType<EventPopupUI>(FindObjectsInactive.Include);
    }

    public void TriggerGameOver(string bodyText)
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;
        Time.timeScale = 0f;

        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.PlayMusic(GameAudioManager.MusicState.GameOver);

        if (eventPopupUI == null)
        {
            Debug.LogError("GameOverManager: No EventPopupUI found.");
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        eventPopupUI.ShowSimpleEvent(
            "Game Over",
            bodyText,
            skullSprite,
            "OK",
            () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(mainMenuSceneName);
            }
        );
    }
}