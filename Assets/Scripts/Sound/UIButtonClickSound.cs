using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIButtonClickSound : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private float volume = 1f;
    

    void Awake()
    {
        if (FindObjectsByType<UIButtonClickSound>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        if (!source)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
    }
    
    void Update()
    {
        RegisterAllButtons();
    }

    public void RegisterAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);

        foreach (var btn in buttons)
        {
            btn.onClick.RemoveListener(PlayClick);
            btn.onClick.AddListener(PlayClick);
        }
    }

    private void PlayClick()
    {
        if (clickClip == null || source == null)
            return;

        source.PlayOneShot(clickClip, volume);
    }
    
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RegisterAllButtons();
    }
}