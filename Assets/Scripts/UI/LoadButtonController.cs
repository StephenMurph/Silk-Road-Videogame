using UnityEngine;
using UnityEngine.UI;

public class LoadButtonController : MonoBehaviour
{
    [SerializeField] private Button loadButton;

    private void Awake()
    {
        if (!loadButton)
            loadButton = GetComponent<Button>();
    }

    private void Start()
    {
        RefreshState();
    }

    public void RefreshState()
    {
        bool hasSave = GameSaveSystem.HasSave();

        loadButton.interactable = hasSave;
    }
}