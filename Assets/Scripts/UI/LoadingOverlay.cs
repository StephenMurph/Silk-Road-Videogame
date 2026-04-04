using UnityEngine;

public class LoadingOverlay : MonoBehaviour
{
    private static LoadingOverlay instance;

    public static void ShowIfPresent()
    {
        if (instance != null)
            instance.gameObject.SetActive(true);
    }

    public static void HideIfPresent()
    {
        if (instance != null)
            instance.gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}