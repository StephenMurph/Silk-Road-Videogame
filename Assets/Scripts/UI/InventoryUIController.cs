using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class InventoryUIController : MonoBehaviour
{
    [Serializable]
    public class GoodIconEntry
    {
        public TradeGoodType goodType;
        public Sprite icon;
    }

    [Serializable]
    public class InventoryCargoSlotUI
    {
        public GameObject root;
        public Image slotBackground;
        public Image itemIcon;
        public TMP_Text quantityText;
    }

    [Serializable]
    public class InventoryPartySlotUI
    {
        public GameObject root;
        public TMP_Text nameText;
        public Image portraitImage;
        public Image healthFill;
        public InventoryCargoSlotUI slotA;
        public InventoryCargoSlotUI slotB;
    }

    [Serializable]
    public class InventoryLayoutUI
    {
        public GameObject root;
        public InventoryPartySlotUI[] partySlots;
    }

    [Header("Core Refs")]
    [SerializeField] private RunState runState;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject inventoryButton;

    [Header("Layouts By Party Size")]
    [SerializeField] private InventoryLayoutUI layout1;
    [SerializeField] private InventoryLayoutUI layout2;
    [SerializeField] private InventoryLayoutUI layout3;
    [SerializeField] private InventoryLayoutUI layout4;

    [Header("Item Icons")]
    [SerializeField] private GoodIconEntry[] goodIcons;

    private bool isOpen;
    private bool travelUIVisible;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (!runState)
            runState = FindFirstObjectByType<RunState>();
    }

    void Start()
    {
        if (inventoryPanel)
            inventoryPanel.SetActive(false);

        isOpen = false;
        travelUIVisible = false;
    }

    void Update()
    {
        if (!travelUIVisible)
            return;

        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            ToggleInventory();

        if (isOpen)
            Refresh();
    }

    public void SetTravelUIVisible(bool visible)
    {
        travelUIVisible = visible;

        if (inventoryButton)
            inventoryButton.SetActive(visible && !isOpen);

        if (!visible && isOpen)
            CloseInventory();
    }

    public void OpenInventory()
    {
        if (isOpen)
            return;

        isOpen = true;

        if (inventoryPanel)
            inventoryPanel.SetActive(true);

        if (inventoryButton)
            inventoryButton.SetActive(false);

        Refresh();
        Time.timeScale = 0f;
    }

    public void CloseInventory()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (inventoryPanel)
            inventoryPanel.SetActive(false);

        if (inventoryButton)
            inventoryButton.SetActive(travelUIVisible);

        Time.timeScale = 1f;
    }

    public void ToggleInventory()
    {
        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void Refresh()
    {
        if (!runState || runState.party == null)
            return;

        int partyCount = Mathf.Clamp(runState.party.MemberCount, 1, 4);

        SetLayoutVisible(layout1, partyCount == 1);
        SetLayoutVisible(layout2, partyCount == 2);
        SetLayoutVisible(layout3, partyCount == 3);
        SetLayoutVisible(layout4, partyCount == 4);

        InventoryLayoutUI activeLayout = GetLayoutForPartyCount(partyCount);
        if (activeLayout == null || activeLayout.partySlots == null)
            return;

        for (int i = 0; i < activeLayout.partySlots.Length; i++)
        {
            bool memberExists = i < runState.party.members.Count;
            SetPartySlot(activeLayout.partySlots[i], memberExists ? runState.party.members[i] : null);
        }
    }

    private InventoryLayoutUI GetLayoutForPartyCount(int partyCount)
    {
        return partyCount switch
        {
            1 => layout1,
            2 => layout2,
            3 => layout3,
            4 => layout4,
            _ => layout1
        };
    }

    private void SetLayoutVisible(InventoryLayoutUI layout, bool visible)
    {
        if (layout != null && layout.root != null)
            layout.root.SetActive(visible);
    }

    private void SetPartySlot(InventoryPartySlotUI ui, PartyMemberState member)
    {
        if (ui == null || ui.root == null)
            return;

        bool hasMember = member != null;
        ui.root.SetActive(hasMember);

        if (!hasMember)
            return;

        if (ui.nameText != null)
            ui.nameText.text = string.IsNullOrWhiteSpace(member.memberName) ? "Unknown" : member.memberName;

        if (ui.healthFill != null)
        {
            float fill = member.maxHealth > 0
                ? member.currentHealth / (float)member.maxHealth
                : 0f;

            ui.healthFill.fillAmount = Mathf.Clamp01(fill);
        }

        SetCargoSlot(ui.slotA, member.slotA);
        SetCargoSlot(ui.slotB, member.slotB);
    }

    private void SetCargoSlot(InventoryCargoSlotUI ui, CargoSlot slot)
    {
        if (ui == null)
            return;

        if (ui.root != null)
            ui.root.SetActive(true);

        bool empty = slot == null || slot.IsEmpty;

        if (ui.itemIcon != null)
        {
            if (empty)
            {
                ui.itemIcon.enabled = false;
                ui.itemIcon.sprite = null;
            }
            else
            {
                Sprite icon = GetIconForGood(slot.goodType);
                ui.itemIcon.sprite = icon;
                ui.itemIcon.enabled = icon != null;
            }
        }

        if (ui.quantityText != null)
        {
            ui.quantityText.text = empty ? "" : slot.quantity.ToString();
        }
    }

    private Sprite GetIconForGood(TradeGoodType type)
    {
        if (goodIcons == null)
            return null;

        for (int i = 0; i < goodIcons.Length; i++)
        {
            if (goodIcons[i] != null && goodIcons[i].goodType == type)
                return goodIcons[i].icon;
        }

        return null;
    }
    
    void LateUpdate()
    {
        if (Keyboard.current == null || runState == null || runState.party == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            runState.party.TryAddGoods(TradeGoodType.Paper, 10);
            Refresh();
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            runState.party.TryAddGoods(TradeGoodType.Porcelain, 5);
            Refresh();
        }
    }
}