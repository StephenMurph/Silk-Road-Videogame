using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TownUIController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject root;

    [Header("UI")]
    [SerializeField] private TMP_Text townNameText;
    [SerializeField] private Image townPhotoImage;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject restPanel;

    [Header("Rest UI")]
    [SerializeField] private TMP_Text restTurnsText;
    [SerializeField] private Button restConfirmButton;
    [SerializeField] private Button restMinusButton;
    [SerializeField] private Button restPlusButton;
    [SerializeField] private TMP_Text restCostText;
    [SerializeField] private float restTickDelay = 0.4f;

    [Header("Refs")]
    [SerializeField] private RunState runState;
    [SerializeField] private PartyHUDController partyHUD;
    [SerializeField] private ResourceHUDController resourceHUD;

    private int selectedRestTurns = 0;

    public void ShowTown(TerrainTownRoadSystem.TownInstance town)
    {
        if (town == null)
            return;

        if (townNameText != null)
            townNameText.text = string.IsNullOrWhiteSpace(town.townName) ? "Unknown Town" : town.townName;

        if (townPhotoImage != null)
        {
            if (town.townPhoto != null)
            {
                townPhotoImage.sprite = town.townPhoto;
                townPhotoImage.enabled = true;
            }
            else
            {
                townPhotoImage.sprite = null;
                townPhotoImage.enabled = false;
            }
        }

        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);
    }

    public void HideTown()
    {
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void OpenRest()
    {
        mainMenuRoot.SetActive(false);
        restPanel.SetActive(true);

        partyHUD?.SetRestVisible(true);
        resourceHUD?.SetRestVisible(true);
        partyHUD?.Refresh();
        resourceHUD?.Refresh();

        int maxTurns = GetMaxRestTurns();
        selectedRestTurns = maxTurns > 0 ? 1 : 0;

        RefreshRestUI();
    }

    public void BackToMainMenu()
    {
        partyHUD?.SetRestVisible(false);
        resourceHUD?.SetRestVisible(false);

        restPanel.SetActive(false);
        mainMenuRoot.SetActive(true);
    }

    public void IncreaseRestTurns()
    {
        int maxTurns = GetMaxRestTurns();
        if (maxTurns <= 0)
        {
            selectedRestTurns = 0;
            RefreshRestUI();
            return;
        }

        selectedRestTurns = Mathf.Clamp(selectedRestTurns + 1, 0, maxTurns);
        RefreshRestUI();
    }

    public void DecreaseRestTurns()
    {
        selectedRestTurns = Mathf.Max(0, selectedRestTurns - 1);
        RefreshRestUI();
    }

    public void ConfirmRest()
    {
        int maxTurns = GetMaxRestTurns();

        if (maxTurns <= 0)
        {
            Debug.Log("Not enough supplies to rest.");
            selectedRestTurns = 0;
            RefreshRestUI();
            return;
        }

        selectedRestTurns = Mathf.Clamp(selectedRestTurns, 0, maxTurns);

        if (selectedRestTurns <= 0)
        {
            RefreshRestUI();
            return;
        }

        restConfirmButton.interactable = false;
        if (restMinusButton != null) restMinusButton.interactable = false;
        if (restPlusButton != null) restPlusButton.interactable = false;

        CoroutineRunner.Instance.StartCoroutine(RestRoutine(selectedRestTurns));
    }

    private IEnumerator RestRoutine(int turns)
    {
        while (turns > 0)
        {
            if (GetMaxRestTurns() <= 0)
                break;

            runState.ResolveSuppliesAfterTravelRoll();
            runState.party.HealAllMembers(20);

            turns--;
            selectedRestTurns = turns;

            partyHUD?.Refresh();
            resourceHUD?.Refresh();
            RefreshRestUI();

            yield return new WaitForSeconds(restTickDelay);
        }

        BackToMainMenu();

        if (restConfirmButton != null) restConfirmButton.interactable = true;
        if (restMinusButton != null) restMinusButton.interactable = true;
        if (restPlusButton != null) restPlusButton.interactable = true;
    }

    private int GetMaxRestTurns()
    {
        if (runState == null || runState.party == null || runState.resources == null)
            return 0;

        int foodCostPerTurn = runState.GetFoodCostForTravelRoll();
        int waterCostPerTurn = runState.GetWaterCostForTravelRoll();

        if (foodCostPerTurn <= 0 || waterCostPerTurn <= 0)
            return 0;

        int maxFoodTurns = runState.resources.food / foodCostPerTurn;
        int maxWaterTurns = runState.resources.water / waterCostPerTurn;

        return Mathf.Min(maxFoodTurns, maxWaterTurns);
    }

    private void RefreshRestUI()
    {
        int maxTurns = GetMaxRestTurns();

        // Clamp current value safely
        selectedRestTurns = Mathf.Clamp(selectedRestTurns, 0, maxTurns);

        // Update number display
        if (restTurnsText != null)
            restTurnsText.text = selectedRestTurns.ToString();

        // --- BUTTON STATES ---

        // Confirm = only clickable if > 0
        if (restConfirmButton != null)
            restConfirmButton.interactable = selectedRestTurns > 0;

        // Minus = only clickable if > 0
        if (restMinusButton != null)
            restMinusButton.interactable = selectedRestTurns > 0;

        // Plus = only clickable if < max
        if (restPlusButton != null)
            restPlusButton.interactable = selectedRestTurns < maxTurns;

        // If max is 0 → EVERYTHING OFF
        if (maxTurns <= 0)
        {
            if (restConfirmButton != null) restConfirmButton.interactable = false;
            if (restMinusButton != null) restMinusButton.interactable = false;
            if (restPlusButton != null) restPlusButton.interactable = false;
        }

        RefreshRestCostText();
    }

    private void RefreshRestCostText()
    {
        if (restCostText == null || runState == null)
            return;

        int foodCostPerTurn = runState.GetFoodCostForTravelRoll();
        int waterCostPerTurn = runState.GetWaterCostForTravelRoll();

        restCostText.text = $"Uses {foodCostPerTurn} food and {waterCostPerTurn} water per turn";
    }
    
    
}