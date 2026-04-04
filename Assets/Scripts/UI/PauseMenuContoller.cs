using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    private bool isOpen;

    private void Start()
    {
        if (root != null)
            root.SetActive(false);

        isOpen = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isOpen) ResumeGame();
            else OpenPauseMenu();
        }
    }

    public void OpenPauseMenu()
    {
        isOpen = true;

        if (root != null)
            root.SetActive(true);

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isOpen = false;

        if (root != null)
            root.SetActive(false);

        Time.timeScale = 1f;
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}