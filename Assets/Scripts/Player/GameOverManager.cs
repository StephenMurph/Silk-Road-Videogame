using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private EventPopupUI eventPopupUI;
    [SerializeField] private Sprite skullSprite;
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    private bool gameOverTriggered;
    
    public static bool IsGameOver => Instance != null && Instance.gameOverTriggered;

    public bool IsGameOverTriggered => gameOverTriggered;

    public static GameOverManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (!eventPopupUI)
            eventPopupUI = FindFirstObjectByType<EventPopupUI>(FindObjectsInactive.Include);
    }

    public void TriggerGameOver(string bodyText)
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;

        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.PlayMusic(GameAudioManager.MusicState.GameOver);
        
        Time.timeScale = 0f;

        DestroyAllDice();
        
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
    
    private void DestroyAllDice()
    {
        var dice = FindObjectsByType<DiceController>(FindObjectsSortMode.None);

        foreach (var d in dice)
        {
            if (d != null)
                Destroy(d.gameObject);
        }
    }
}