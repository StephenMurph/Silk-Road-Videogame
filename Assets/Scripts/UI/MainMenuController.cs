using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "LoadingScene";

    [Header("New Game")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button newGameButton;

    private void Awake()
    {
        if (!newGameButton)
            Debug.LogWarning("MainMenuController: New Game button not assigned.");

        if (playerNameInput != null)
            playerNameInput.onValueChanged.AddListener(_ => RefreshNewGameButton());
    }

    private void Start()
    {
        RefreshNewGameButton();
    }

    private void OnDestroy()
    {
        if (playerNameInput != null)
            playerNameInput.onValueChanged.RemoveListener(_ => RefreshNewGameButton());
    }

    private void RefreshNewGameButton()
    {
        if (newGameButton == null)
            return;

        bool hasValidName = playerNameInput != null &&
                            !string.IsNullOrWhiteSpace(playerNameInput.text);

        newGameButton.interactable = hasValidName;
    }

    public void StartGame()
    {
        string typedName = playerNameInput != null ? playerNameInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(typedName))
            return;

        GameLaunchState.LoadFromSave = false;
        GameLaunchState.NewLeaderName = typedName;

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
        GameLaunchState.NewLeaderName = "";

        SceneManager.LoadScene(loadingSceneName);
    }
}