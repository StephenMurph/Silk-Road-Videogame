using TMPro;
using UnityEngine;

public class ResourceHUDController : MonoBehaviour
{
    [System.Serializable]
    public class ResourceHUDGroup
    {
        public GameObject root;
        public TMP_Text foodText;
        public TMP_Text waterText;
        public TMP_Text goldText;
    }

    [Header("Refs")]
    [SerializeField] private RunState runState;

    [Header("HUD Groups")]
    [SerializeField] private ResourceHUDGroup travelHUD;
    [SerializeField] private ResourceHUDGroup restHUD;

    void Awake()
    {
        if (!runState)
            runState = FindFirstObjectByType<RunState>();
    }

    void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (!runState || runState.resources == null)
            return;

        RefreshGroup(travelHUD);
        RefreshGroup(restHUD);
    }

    private void RefreshGroup(ResourceHUDGroup group)
    {
        if (group.foodText != null)
            group.foodText.text = runState.resources.food.ToString();

        if (group.waterText != null)
            group.waterText.text = runState.resources.water.ToString();

        if (group.goldText != null)
            group.goldText.text = runState.resources.gold.ToString();
    }

    public void SetVisible(bool visible)
    {
        if (travelHUD.root != null)
            travelHUD.root.SetActive(visible);
    }

    public void SetRestVisible(bool visible)
    {
        if (restHUD.root != null)
            restHUD.root.SetActive(visible);
    }
}