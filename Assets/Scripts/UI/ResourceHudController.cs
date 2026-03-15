using TMPro;
using UnityEngine;

public class ResourceHUDController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RunState runState;

    [Header("Texts")]
    [SerializeField] private TMP_Text foodText;
    [SerializeField] private TMP_Text waterText;
    [SerializeField] private TMP_Text goldText;

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

        if (foodText != null)
            foodText.text = runState.resources.food.ToString();

        if (waterText != null)
            waterText.text = runState.resources.water.ToString();

        if (goldText != null)
            goldText.text = runState.resources.gold.ToString();
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}