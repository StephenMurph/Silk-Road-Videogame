using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private Button restBackButton;
    [SerializeField] private TMP_Text restConfirmButtonText;
    
    [Header("Recruit Panel")]
    [SerializeField] private GameObject recruitPanel;
    [SerializeField] private RecruitSlotUI[] recruitSlots = new RecruitSlotUI[3];
    [SerializeField] private TMP_Text recruitStatusText;
    
    [Header("Buy Panel")]
    [SerializeField] private GameObject buyPanel;
    [SerializeField] private BuySlotUI[] buySlots = new BuySlotUI[6];
    [SerializeField] private Button buyConfirmButton;
    [SerializeField] private TMP_Text buyTotalCostText;
    [SerializeField] private TMP_Text buyGoldAfterText;
    [SerializeField] private TMP_Text buyFoodAfterText;
    [SerializeField] private TMP_Text buyWaterAfterText;

    [Header("Buy Preview Inventory")]
    [SerializeField] private BuyPreviewPartyRowUI[] buyPreviewRows = new BuyPreviewPartyRowUI[4];

    [Header("Buy Icons")]
    [SerializeField] private GoodIconEntry[] buyGoodIcons;
    
    [Header("Sell Panel")]
    [SerializeField] private GameObject sellPanel;
    [SerializeField] private SellPartyRowUI[] sellRows = new SellPartyRowUI[4];
    [SerializeField] private Button sellConfirmButton;
    [SerializeField] private TMP_Text sellTotalText;
    [SerializeField] private TMP_Text sellGoldAfterText;
    
    private readonly int[] selectedSellQuantities = new int[8];
    
    [System.Serializable]
    public class RecruitSlotUI
    {
        public GameObject root;
        public TMP_Text nameText;
        public TMP_Text goldCostText;
        public TMP_Text foodCostText;
        public TMP_Text waterCostText;
        public Button recruitButton;
        public TMP_Text recruitButtonText;
    }
    
    [System.Serializable]
    public class SellCargoSlotUI
    {
        public GameObject root;
        public TMP_Text sellQuantityText;
        public Image iconImage;
        public TMP_Text sellPriceText;
        public Button plusButton;
        public Button minusButton;
    }

    [System.Serializable]
    public class SellPartyRowUI
    {
        public GameObject root;
        public TMP_Text nameText;
        public SellCargoSlotUI slotA;
        public SellCargoSlotUI slotB;
    }
    
    private enum BuyEntryKind
    {
        None,
        Food,
        Water,
        TradeGood
    }

    private class BuyEntryData
    {
        public BuyEntryKind kind;
        public TradeGoodType goodType;
        public string displayName;
        public int price;
    }

    private readonly BuyEntryData[] activeBuyEntries = new BuyEntryData[6];
    private readonly int[] selectedBuyQuantities = new int[6];
    
    [System.Serializable]
    public class GoodIconEntry
    {
        public TradeGoodType goodType;
        public Sprite icon;
    }

    [System.Serializable]
    public class BuySlotUI
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text priceText;
        public TMP_Text quantityText;
        public Button minusButton;
        public Button plusButton;
    }

    [System.Serializable]
    public class BuyPreviewCargoSlotUI
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text quantityText;
    }

    [System.Serializable]
    public class BuyPreviewPartyRowUI
    {
        public GameObject root;
        public TMP_Text nameText;
        public BuyPreviewCargoSlotUI slotA;
        public BuyPreviewCargoSlotUI slotB;
    }

    [Header("Refs")]
    [SerializeField] private RunState runState;
    [SerializeField] private PartyHUDController partyHUD;
    [SerializeField] private ResourceHUDController resourceHUD;
    [SerializeField] private TerrainTownRoadSystem townSystem;
    [SerializeField] private WorldMarketState marketState;

    private int selectedRestTurns = 0;
    private bool isResting = false;
    private bool stopRestRequested = false;
    private Coroutine activeRestRoutine;
    private TerrainTownRoadSystem.TownInstance currentTown;

    private void Awake()
    {
        if (!runState) runState = FindFirstObjectByType<RunState>();
        if (!partyHUD) partyHUD = FindFirstObjectByType<PartyHUDController>();
        if (!resourceHUD) resourceHUD = FindFirstObjectByType<ResourceHUDController>();
        if (!townSystem) townSystem = FindFirstObjectByType<TerrainTownRoadSystem>();
        if (!marketState) marketState = FindFirstObjectByType<WorldMarketState>();
    }
    
    public void ShowTown(TerrainTownRoadSystem.TownInstance town)
    {
        if (town == null)
            return;

        currentTown = town;
        
        int townIndex = townSystem != null ? townSystem.towns.IndexOf(town) : -1;
        marketState?.OnTownArrival(townSystem, townIndex);

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

        isResting = false;
        stopRestRequested = false;

        int maxTurns = GetMaxRestTurns();
        selectedRestTurns = maxTurns > 0 ? 1 : 0;

        RefreshRestUI();
    }

    public void BackToMainMenu()
    {
        if (isResting)
            return;

        partyHUD?.SetRestVisible(false);
        resourceHUD?.SetRestVisible(false);

        if (restPanel != null)
            restPanel.SetActive(false);

        if (recruitPanel != null)
            recruitPanel.SetActive(false);

        if (buyPanel != null)
            buyPanel.SetActive(false);
        
        if (sellPanel != null)
            sellPanel.SetActive(false);

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
        if (isResting)
        {
            stopRestRequested = true;
            return;
        }

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

        isResting = true;
        stopRestRequested = false;

        RefreshRestUI();

        activeRestRoutine = CoroutineRunner.Instance.StartCoroutine(RestRoutine(selectedRestTurns));
    }

    private IEnumerator RestRoutine(int turns)
    {
        while (turns > 0)
        {
            if (stopRestRequested)
                break;

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

        isResting = false;
        stopRestRequested = false;
        activeRestRoutine = null;

        RefreshRestUI();
        BackToMainMenu();
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

        selectedRestTurns = Mathf.Clamp(selectedRestTurns, 0, maxTurns);

        if (restTurnsText != null)
            restTurnsText.text = selectedRestTurns.ToString();

        if (restConfirmButtonText != null)
            restConfirmButtonText.text = isResting ? "Stop Resting" : "Rest";

        if (isResting)
        {
            if (restConfirmButton != null)
                restConfirmButton.interactable = true;

            if (restMinusButton != null)
                restMinusButton.interactable = false;

            if (restPlusButton != null)
                restPlusButton.interactable = false;

            if (restBackButton != null)
                restBackButton.interactable = false;
        }
        else
        {
            if (restConfirmButton != null)
                restConfirmButton.interactable = selectedRestTurns > 0;

            if (restMinusButton != null)
                restMinusButton.interactable = selectedRestTurns > 0;

            if (restPlusButton != null)
                restPlusButton.interactable = selectedRestTurns < maxTurns;

            if (restBackButton != null)
                restBackButton.interactable = true;

            if (maxTurns <= 0)
            {
                if (restConfirmButton != null) restConfirmButton.interactable = false;
                if (restMinusButton != null) restMinusButton.interactable = false;
                if (restPlusButton != null) restPlusButton.interactable = false;
            }
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
    
    public void OpenRecruit()
    {
        mainMenuRoot.SetActive(false);

        if (recruitPanel != null)
            recruitPanel.SetActive(true);

        RefreshRecruitPanel();
    }

    public void BackFromRecruit()
    {
        if (recruitPanel != null)
            recruitPanel.SetActive(false);

        mainMenuRoot.SetActive(true);
    }

    private void RefreshRecruitPanel()
    {
        if (currentTown == null || currentTown.recruitCandidates == null)
            return;

        bool partyFull =
            runState != null &&
            runState.party != null &&
            runState.party.MemberCount >= runState.party.maxPartySize;

        if (recruitStatusText != null)
            recruitStatusText.text = partyFull ? "Party Full" : "";

        for (int i = 0; i < recruitSlots.Length; i++)
        {
            if (recruitSlots[i] == null || recruitSlots[i].root == null)
                continue;

            bool hasCandidate =
                i < currentTown.recruitCandidates.Length &&
                currentTown.recruitCandidates[i] != null;

            recruitSlots[i].root.SetActive(hasCandidate);

            if (!hasCandidate)
                continue;

            RecruitCandidateData candidate = currentTown.recruitCandidates[i];
            bool alreadyRecruited = candidate.recruited;

            if (recruitSlots[i].nameText != null)
                recruitSlots[i].nameText.text = candidate.candidateName;

            if (recruitSlots[i].goldCostText != null)
                recruitSlots[i].goldCostText.text = alreadyRecruited ? "-" : candidate.goldCostPerTurn.ToString();

            if (recruitSlots[i].foodCostText != null)
                recruitSlots[i].foodCostText.text = alreadyRecruited ? "-" : candidate.foodCostPerTurn.ToString();

            if (recruitSlots[i].waterCostText != null)
                recruitSlots[i].waterCostText.text = alreadyRecruited ? "-" : candidate.waterCostPerTurn.ToString();

            if (recruitSlots[i].recruitButton != null)
                recruitSlots[i].recruitButton.interactable = !partyFull && !alreadyRecruited;

            if (recruitSlots[i].recruitButtonText != null)
                recruitSlots[i].recruitButtonText.text = alreadyRecruited ? "Recruited" : "Recruit";
        }
    }

    public void RecruitCandidate0()
    {
        RecruitCandidateAtIndex(0);
    }

    public void RecruitCandidate1()
    {
        RecruitCandidateAtIndex(1);
    }

    public void RecruitCandidate2()
    {
        RecruitCandidateAtIndex(2);
    }

    private void RecruitCandidateAtIndex(int index)
    {
        if (currentTown == null || currentTown.recruitCandidates == null)
            return;

        if (index < 0 || index >= currentTown.recruitCandidates.Length)
            return;

        RecruitCandidateData candidate = currentTown.recruitCandidates[index];
        if (candidate == null || candidate.recruited)
            return;

        if (runState == null || runState.party == null)
            return;

        bool added = runState.party.TryAddMember(
            candidate.candidateName,
            candidate.goldCostPerTurn,
            candidate.foodCostPerTurn,
            candidate.waterCostPerTurn
        );

        if (!added)
        {
            Debug.Log("Party full. Could not recruit.");
            RefreshRecruitPanel();
            return;
        }

        Debug.Log(
            $"Recruited {candidate.candidateName} " +
            $"(Gold {candidate.goldCostPerTurn}, Food {candidate.foodCostPerTurn}, Water {candidate.waterCostPerTurn})"
        );

        candidate.recruited = true;

        partyHUD?.Refresh();
        resourceHUD?.Refresh();

        RefreshRecruitPanel();
    }
    
    public void OpenBuy()
    {
        if (currentTown == null || currentTown.marketData == null)
            return;

        mainMenuRoot.SetActive(false);

        if (buyPanel != null)
            buyPanel.SetActive(true);

        BuildBuyEntries();
        ClearBuySelections();
        RefreshBuyPanel();
    }

    public void BackFromBuy()
    {
        if (buyPanel != null)
            buyPanel.SetActive(false);

        mainMenuRoot.SetActive(true);
    }

    private void BuildBuyEntries()
    {
        for (int i = 0; i < activeBuyEntries.Length; i++)
            activeBuyEntries[i] = null;

        if (currentTown == null || currentTown.marketData == null)
            return;

        activeBuyEntries[0] = new BuyEntryData
        {
            kind = BuyEntryKind.Food,
            goodType = TradeGoodType.None,
            displayName = "Food",
            price = currentTown.marketData.foodPrice
        };

        activeBuyEntries[1] = new BuyEntryData
        {
            kind = BuyEntryKind.Water,
            goodType = TradeGoodType.None,
            displayName = "Water",
            price = currentTown.marketData.waterPrice
        };

        for (int i = 0; i < 4; i++)
        {
            if (currentTown.marketData.goods == null || i >= currentTown.marketData.goods.Length)
                break;

            var good = currentTown.marketData.goods[i];
            if (good == null || good.type == TradeGoodType.None)
                continue;

            activeBuyEntries[i + 2] = new BuyEntryData
            {
                kind = BuyEntryKind.TradeGood,
                goodType = good.type,
                displayName = good.type.ToString(),
                price = good.price
            };
        }
    }

    private void ClearBuySelections()
    {
        for (int i = 0; i < selectedBuyQuantities.Length; i++)
            selectedBuyQuantities[i] = 0;
    }

    private void RefreshBuyPanel()
    {
        for (int i = 0; i < buySlots.Length; i++)
        {
            RefreshSingleBuySlot(i);
        }

        RefreshBuyTotals();
        RefreshBuyPreviewInventory();
    }

    private void RefreshSingleBuySlot(int index)
    {
        if (index < 0 || index >= buySlots.Length || buySlots[index] == null)
            return;

        var ui = buySlots[index];
        var entry = activeBuyEntries[index];

        bool hasEntry = entry != null && entry.kind != BuyEntryKind.None;

        if (ui.root != null)
            ui.root.SetActive(hasEntry);

        if (!hasEntry)
            return;

        if (ui.nameText != null)
            ui.nameText.text = entry.displayName;

        if (ui.priceText != null)
            ui.priceText.text = entry.price.ToString();

        if (ui.quantityText != null)
            ui.quantityText.text = selectedBuyQuantities[index].ToString();

        if (ui.iconImage != null)
        {
            Sprite icon = GetBuyIcon(entry);
            ui.iconImage.sprite = icon;
            ui.iconImage.enabled = icon != null;
        }

        if (ui.minusButton != null)
            ui.minusButton.interactable = selectedBuyQuantities[index] > 0;

        if (ui.plusButton != null)
            ui.plusButton.interactable = CanIncreaseBuyQuantity(index);
    }

    private void RefreshBuyTotals()
    {
        int totalCost = GetSelectedBuyTotalCost();
        int goldAfter = runState != null && runState.resources != null
            ? runState.resources.gold - totalCost
            : 0;

        int foodAfter = runState != null && runState.resources != null
            ? runState.resources.food + GetSelectedQuantityForKind(BuyEntryKind.Food)
            : 0;

        int waterAfter = runState != null && runState.resources != null
            ? runState.resources.water + GetSelectedQuantityForKind(BuyEntryKind.Water)
            : 0;

        if (buyTotalCostText != null)
            buyTotalCostText.text = totalCost.ToString();

        if (buyGoldAfterText != null)
            buyGoldAfterText.text = goldAfter.ToString();

        if (buyFoodAfterText != null)
            buyFoodAfterText.text = foodAfter.ToString();

        if (buyWaterAfterText != null)
            buyWaterAfterText.text = waterAfter.ToString();

        if (buyConfirmButton != null)
            buyConfirmButton.interactable = CanBuyCurrentSelection();
    }

    private bool CanBuyCurrentSelection()
    {
        if (runState == null || runState.resources == null || runState.party == null)
            return false;

        int totalCost = GetSelectedBuyTotalCost();
        if (totalCost <= 0)
            return false;

        if (runState.resources.gold < totalCost)
            return false;

        return CanFitSelectedTradeGoods();
    }

    private int GetSelectedBuyTotalCost()
    {
        int total = 0;

        for (int i = 0; i < activeBuyEntries.Length; i++)
        {
            if (activeBuyEntries[i] == null) continue;
            total += activeBuyEntries[i].price * selectedBuyQuantities[i];
        }

        return total;
    }

    private int GetSelectedQuantityForKind(BuyEntryKind kind)
    {
        int total = 0;

        for (int i = 0; i < activeBuyEntries.Length; i++)
        {
            if (activeBuyEntries[i] == null) continue;
            if (activeBuyEntries[i].kind != kind) continue;

            total += selectedBuyQuantities[i];
        }

        return total;
    }

    private bool CanIncreaseBuyQuantity(int index)
    {
        if (runState == null || runState.resources == null)
            return false;

        if (index < 0 || index >= activeBuyEntries.Length)
            return false;

        var entry = activeBuyEntries[index];
        if (entry == null)
            return false;

        int currentTotalCost = GetSelectedBuyTotalCost();
        int nextCost = currentTotalCost + entry.price;

        if (nextCost > runState.resources.gold)
            return false;

        if (entry.kind == BuyEntryKind.Food || entry.kind == BuyEntryKind.Water)
            return true;

        if (entry.kind == BuyEntryKind.TradeGood)
            return CanFitTradeGoodWithExtra(entry.goodType, 1);

        return false;
    }

    private bool CanFitSelectedTradeGoods()
    {
        List<CargoSlot> preview = BuildPreviewCargoSlotsFromActual();
        return ApplySelectedTradeGoodsToPreview(preview);
    }

    private bool CanFitTradeGoodWithExtra(TradeGoodType extraType, int extraAmount)
    {
        List<CargoSlot> preview = BuildPreviewCargoSlotsFromActual();

        if (!ApplySelectedTradeGoodsToPreview(preview))
            return false;

        return TryAddGoodToPreview(preview, extraType, extraAmount);
    }

    private List<CargoSlot> BuildPreviewCargoSlotsFromActual()
    {
        var result = new List<CargoSlot>();

        if (runState == null || runState.party == null || runState.party.members == null)
            return result;

        for (int i = 0; i < runState.party.members.Count; i++)
        {
            var member = runState.party.members[i];
            if (member == null)
                continue;
            
            CargoSlot cloneA = new CargoSlot();
            CargoSlot cloneB = new CargoSlot();

            if (member.slotA != null)
            {
                cloneA.goodType = member.slotA.goodType;
                cloneA.quantity = member.slotA.quantity;
            }

            if (member.slotB != null)
            {
                cloneB.goodType = member.slotB.goodType;
                cloneB.quantity = member.slotB.quantity;
            }

            result.Add(cloneA); 
            result.Add(cloneB); 
        }

        return result;
    }

    private bool ApplySelectedTradeGoodsToPreview(List<CargoSlot> preview)
    {
        for (int i = 0; i < activeBuyEntries.Length; i++)
        {
            var entry = activeBuyEntries[i];
            if (entry == null) continue;
            if (entry.kind != BuyEntryKind.TradeGood) continue;

            int amount = selectedBuyQuantities[i];
            if (amount <= 0) continue;

            if (!TryAddGoodToPreview(preview, entry.goodType, amount))
                return false;
        }

        return true;
    }

    private bool TryAddGoodToPreview(List<CargoSlot> preview, TradeGoodType type, int amount)
    {
        if (type == TradeGoodType.None || amount <= 0 || preview == null)
            return false;

        int remaining = amount;
        int stackLimit = runState != null && runState.party != null
            ? Mathf.Max(1, runState.party.stackLimitPerSlot)
            : 10;
        
        for (int i = 0; i < preview.Count; i++)
        {
            var slot = preview[i];
            if (slot == null) continue;
            if (!slot.CanStack(type, stackLimit)) continue;

            int add = Mathf.Min(remaining, slot.SpaceLeft(stackLimit));
            slot.quantity += add;
            remaining -= add;

            if (remaining <= 0)
                return true;
        }
        
        for (int i = 0; i < preview.Count; i++)
        {
            var slot = preview[i];
            if (slot == null) continue;
            if (!slot.IsEmpty) continue;

            int add = Mathf.Min(remaining, stackLimit);
            slot.goodType = type;
            slot.quantity = add;
            remaining -= add;

            if (remaining <= 0)
                return true;
        }

        return false;
    }

    private void RefreshBuyPreviewInventory()
    {
        if (buyPreviewRows == null)
            return;

        List<CargoSlot> preview = BuildPreviewCargoSlotsFromActual();
        ApplySelectedTradeGoodsToPreview(preview);

        int slotCursor = 0;
        int memberCount = runState != null && runState.party != null ? runState.party.MemberCount : 0;

        for (int i = 0; i < buyPreviewRows.Length; i++)
        {
            var row = buyPreviewRows[i];
            if (row == null || row.root == null)
                continue;

            bool hasMember = i < memberCount;
            row.root.SetActive(hasMember);

            if (!hasMember)
                continue;

            var member = runState.party.members[i];

            if (row.nameText != null)
                row.nameText.text = member != null ? member.memberName : $"Member {i + 1}";

            CargoSlot slotA = slotCursor < preview.Count ? preview[slotCursor] : null;
            slotCursor++;
            CargoSlot slotB = slotCursor < preview.Count ? preview[slotCursor] : null;
            slotCursor++;

            SetBuyPreviewCargoSlot(row.slotA, slotA);
            SetBuyPreviewCargoSlot(row.slotB, slotB);
        }
    }

    private void SetBuyPreviewCargoSlot(BuyPreviewCargoSlotUI ui, CargoSlot slot)
    {
        if (ui == null)
            return;

        if (ui.root != null)
            ui.root.SetActive(true);

        bool empty = slot == null || slot.IsEmpty;

        if (ui.iconImage != null)
        {
            if (empty)
            {
                ui.iconImage.enabled = false;
                ui.iconImage.sprite = null;
            }
            else
            {
                ui.iconImage.sprite = GetBuyIcon(slot.goodType);
                ui.iconImage.enabled = ui.iconImage.sprite != null;
            }
        }

        if (ui.quantityText != null)
            ui.quantityText.text = empty ? "" : slot.quantity.ToString();
    }

    private Sprite GetBuyIcon(BuyEntryData entry)
    {
        if (entry == null)
            return null;

        if (entry.kind == BuyEntryKind.TradeGood)
            return GetBuyIcon(entry.goodType);

        return null;
    }

    private Sprite GetBuyIcon(TradeGoodType type)
    {
        if (buyGoodIcons == null)
            return null;

        for (int i = 0; i < buyGoodIcons.Length; i++)
        {
            if (buyGoodIcons[i] != null && buyGoodIcons[i].goodType == type)
                return buyGoodIcons[i].icon;
        }

        return null;
    }

    public void IncreaseBuySlot0() => ChangeBuyQuantity(0, +1);
    public void IncreaseBuySlot1() => ChangeBuyQuantity(1, +1);
    public void IncreaseBuySlot2() => ChangeBuyQuantity(2, +1);
    public void IncreaseBuySlot3() => ChangeBuyQuantity(3, +1);
    public void IncreaseBuySlot4() => ChangeBuyQuantity(4, +1);
    public void IncreaseBuySlot5() => ChangeBuyQuantity(5, +1);

    public void DecreaseBuySlot0() => ChangeBuyQuantity(0, -1);
    public void DecreaseBuySlot1() => ChangeBuyQuantity(1, -1);
    public void DecreaseBuySlot2() => ChangeBuyQuantity(2, -1);
    public void DecreaseBuySlot3() => ChangeBuyQuantity(3, -1);
    public void DecreaseBuySlot4() => ChangeBuyQuantity(4, -1);
    public void DecreaseBuySlot5() => ChangeBuyQuantity(5, -1);

    private void ChangeBuyQuantity(int index, int delta)
    {
        if (index < 0 || index >= selectedBuyQuantities.Length)
            return;

        if (delta > 0)
        {
            if (!CanIncreaseBuyQuantity(index))
                return;

            selectedBuyQuantities[index]++;
        }
        else if (delta < 0)
        {
            selectedBuyQuantities[index] = Mathf.Max(0, selectedBuyQuantities[index] - 1);
        }

        RefreshBuyPanel();
    }

    public void ConfirmBuy()
    {
        if (!CanBuyCurrentSelection())
        {
            RefreshBuyPanel();
            return;
        }

        int totalCost = GetSelectedBuyTotalCost();
        if (!runState.resources.SpendGold(totalCost))
        {
            RefreshBuyPanel();
            return;
        }

        int foodToAdd = GetSelectedQuantityForKind(BuyEntryKind.Food);
        int waterToAdd = GetSelectedQuantityForKind(BuyEntryKind.Water);

        if (foodToAdd > 0)
            runState.resources.AddFood(foodToAdd);

        if (waterToAdd > 0)
            runState.resources.AddWater(waterToAdd);

        for (int i = 0; i < activeBuyEntries.Length; i++)
        {
            var entry = activeBuyEntries[i];
            if (entry == null) continue;
            if (entry.kind != BuyEntryKind.TradeGood) continue;

            int amount = selectedBuyQuantities[i];
            if (amount <= 0) continue;

            bool added = runState.party.TryAddGoods(entry.goodType, amount);
            if (!added)
                Debug.LogWarning($"Buy: Could not fully add {entry.goodType} x{amount}");
        }

        ClearBuySelections();
        partyHUD?.Refresh();
        resourceHUD?.Refresh();
        RefreshBuyPanel();
    }
    
    public void OpenSell()
    {
        mainMenuRoot.SetActive(false);

        if (sellPanel != null)
            sellPanel.SetActive(true);

        ClearSellSelections();
        RefreshSellPanel();
    }

    public void BackFromSell()
    {
        if (sellPanel != null)
            sellPanel.SetActive(false);

        mainMenuRoot.SetActive(true);
    }

    private void ClearSellSelections()
    {
        for (int i = 0; i < selectedSellQuantities.Length; i++)
            selectedSellQuantities[i] = 0;
    }

    private void RefreshSellPanel()
    {
        int slotIndex = 0;
        int memberCount = runState != null && runState.party != null ? runState.party.MemberCount : 0;

        for (int i = 0; i < sellRows.Length; i++)
        {
            var row = sellRows[i];
            if (row == null || row.root == null)
                continue;

            bool hasMember = i < memberCount;
            row.root.SetActive(hasMember);

            if (!hasMember)
                continue;

            var member = runState.party.members[i];

            if (row.nameText != null)
                row.nameText.text = member != null ? member.memberName : $"Member {i + 1}";

            CargoSlot slotA = member != null ? member.slotA : null;
            CargoSlot slotB = member != null ? member.slotB : null;

            RefreshSellCargoSlot(row.slotA, slotA, slotIndex);
            slotIndex++;

            RefreshSellCargoSlot(row.slotB, slotB, slotIndex);
            slotIndex++;
        }

        RefreshSellTotals();
    }

    private void RefreshSellCargoSlot(SellCargoSlotUI ui, CargoSlot slot, int slotIndex)
    {
        if (ui == null)
            return;

        if (ui.root != null)
            ui.root.SetActive(true);

        bool empty = slot == null || slot.IsEmpty;
        int ownedQuantity = empty ? 0 : slot.quantity;
        int selectedQuantity = slotIndex >= 0 && slotIndex < selectedSellQuantities.Length
            ? selectedSellQuantities[slotIndex]
            : 0;

        selectedQuantity = Mathf.Clamp(selectedQuantity, 0, ownedQuantity);
        if (slotIndex >= 0 && slotIndex < selectedSellQuantities.Length)
            selectedSellQuantities[slotIndex] = selectedQuantity;

        if (ui.iconImage != null)
        {
            if (empty)
            {
                ui.iconImage.enabled = false;
                ui.iconImage.sprite = null;
            }
            else
            {
                ui.iconImage.sprite = GetBuyIcon(slot.goodType);
                ui.iconImage.enabled = ui.iconImage.sprite != null;
            }
        }

        if (ui.sellQuantityText != null)
            ui.sellQuantityText.text = selectedQuantity.ToString();

        if (ui.sellPriceText != null)
            ui.sellPriceText.text = empty ? "0" : GetSellPrice(slot.goodType).ToString();

        if (ui.minusButton != null)
            ui.minusButton.interactable = !empty && selectedQuantity > 0;

        if (ui.plusButton != null)
            ui.plusButton.interactable = !empty && selectedQuantity < ownedQuantity;
    }

    private void RefreshSellTotals()
    {
        int totalSellValue = GetSelectedSellTotalValue();
        int goldAfter = runState != null && runState.resources != null
            ? runState.resources.gold + totalSellValue
            : totalSellValue;

        if (sellTotalText != null)
            sellTotalText.text = totalSellValue.ToString();

        if (sellGoldAfterText != null)
            sellGoldAfterText.text = goldAfter.ToString();

        if (sellConfirmButton != null)
            sellConfirmButton.interactable = totalSellValue > 0;
    }

    private int GetSelectedSellTotalValue()
    {
        if (runState == null || runState.party == null || runState.party.members == null)
            return 0;

        int total = 0;
        int slotIndex = 0;

        for (int i = 0; i < runState.party.members.Count; i++)
        {
            var member = runState.party.members[i];
            if (member == null) continue;

            total += GetSellValueForSlot(member.slotA, slotIndex);
            slotIndex++;

            total += GetSellValueForSlot(member.slotB, slotIndex);
            slotIndex++;
        }

        return total;
    }

    private int GetSellValueForSlot(CargoSlot slot, int slotIndex)
    {
        if (slot == null || slot.IsEmpty)
            return 0;

        if (slotIndex < 0 || slotIndex >= selectedSellQuantities.Length)
            return 0;

        int quantity = Mathf.Clamp(selectedSellQuantities[slotIndex], 0, slot.quantity);
        return quantity * GetSellPrice(slot.goodType);
    }

    private int GetSellPrice(TradeGoodType type)
    {
        if (currentTown == null || currentTown.marketData == null || currentTown.marketData.sellPrices == null)
            return 1;

        for (int i = 0; i < currentTown.marketData.sellPrices.Length; i++)
        {
            var entry = currentTown.marketData.sellPrices[i];
            if (entry != null && entry.type == type)
                return entry.price;
        }

        return 1;
    }

    private CargoSlot GetCargoSlotByFlatIndex(int slotIndex)
    {
        if (runState == null || runState.party == null || runState.party.members == null)
            return null;

        int cursor = 0;

        for (int i = 0; i < runState.party.members.Count; i++)
        {
            var member = runState.party.members[i];
            if (member == null) continue;

            if (cursor == slotIndex) return member.slotA;
            cursor++;

            if (cursor == slotIndex) return member.slotB;
            cursor++;
        }

        return null;
    }

    private void ChangeSellQuantity(int slotIndex, int delta)
    {
        if (slotIndex < 0 || slotIndex >= selectedSellQuantities.Length)
            return;

        CargoSlot slot = GetCargoSlotByFlatIndex(slotIndex);
        if (slot == null || slot.IsEmpty)
            return;

        int ownedQuantity = slot.quantity;
        int current = selectedSellQuantities[slotIndex];

        current += delta;
        current = Mathf.Clamp(current, 0, ownedQuantity);

        selectedSellQuantities[slotIndex] = current;

        RefreshSellPanel();
    }

    public void ConfirmSell()
    {
        int totalSellValue = GetSelectedSellTotalValue();
        if (totalSellValue <= 0 || runState == null || runState.resources == null || runState.party == null)
        {
            RefreshSellPanel();
            return;
        }

        int slotIndex = 0;

        for (int i = 0; i < runState.party.members.Count; i++)
        {
            var member = runState.party.members[i];
            if (member == null) continue;

            CommitSellForSlot(member.slotA, slotIndex);
            slotIndex++;

            CommitSellForSlot(member.slotB, slotIndex);
            slotIndex++;
        }

        runState.resources.AddGold(totalSellValue);

        ClearSellSelections();
        partyHUD?.Refresh();
        resourceHUD?.Refresh();
        RefreshSellPanel();
    }

    private void CommitSellForSlot(CargoSlot slot, int slotIndex)
    {
        if (slot == null || slot.IsEmpty)
            return;

        if (slotIndex < 0 || slotIndex >= selectedSellQuantities.Length)
            return;

        int amountToSell = Mathf.Clamp(selectedSellQuantities[slotIndex], 0, slot.quantity);
        if (amountToSell <= 0)
            return;

        slot.quantity -= amountToSell;
        if (slot.quantity <= 0)
            slot.Clear();
    }
    
    public void IncreaseSellSlot0() => ChangeSellQuantity(0, +1);
    public void IncreaseSellSlot1() => ChangeSellQuantity(1, +1);
    public void IncreaseSellSlot2() => ChangeSellQuantity(2, +1);
    public void IncreaseSellSlot3() => ChangeSellQuantity(3, +1);
    public void IncreaseSellSlot4() => ChangeSellQuantity(4, +1);
    public void IncreaseSellSlot5() => ChangeSellQuantity(5, +1);
    public void IncreaseSellSlot6() => ChangeSellQuantity(6, +1);
    public void IncreaseSellSlot7() => ChangeSellQuantity(7, +1);

    public void DecreaseSellSlot0() => ChangeSellQuantity(0, -1);
    public void DecreaseSellSlot1() => ChangeSellQuantity(1, -1);
    public void DecreaseSellSlot2() => ChangeSellQuantity(2, -1);
    public void DecreaseSellSlot3() => ChangeSellQuantity(3, -1);
    public void DecreaseSellSlot4() => ChangeSellQuantity(4, -1);
    public void DecreaseSellSlot5() => ChangeSellQuantity(5, -1);
    public void DecreaseSellSlot6() => ChangeSellQuantity(6, -1);
    public void DecreaseSellSlot7() => ChangeSellQuantity(7, -1);
    
}