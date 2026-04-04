using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class LoadingScreenController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private Slider progressBar;

    private IEnumerator Start()
    {
        LoadingOverlay.ShowIfPresent();

        string sceneName = string.IsNullOrWhiteSpace(GameLoadRequest.TargetSceneName)
            ? "GameScene"
            : GameLoadRequest.TargetSceneName;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(loadOp.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            if (loadingText != null)
                loadingText.text = $"Loading... {Mathf.RoundToInt(progress * 100f)}%";

            yield return null;
        }

        if (progressBar != null)
            progressBar.value = 1f;

        if (loadingText != null)
            loadingText.text = "Now Loading...";

        DontDestroyOnLoad(gameObject.transform.root.gameObject);
        loadOp.allowSceneActivation = true;
    }
}