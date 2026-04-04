using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyHUDController : MonoBehaviour
{
    [System.Serializable]
    public class PartyRowUI
    {
        public GameObject root;
        public TMP_Text nameText;
        public Image barFill;
    }

    [System.Serializable]
    public class PartyHUDGroup
    {
        public GameObject root;
        public PartyRowUI[] rows = new PartyRowUI[4];

        public GameObject background1;
        public GameObject background2;
        public GameObject background3;
        public GameObject background4;
    }

    [Header("Refs")]
    [SerializeField] private RunState runState;

    [Header("HUD Groups")]
    [SerializeField] private PartyHUDGroup travelHUD;

    [SerializeField] private PartyHUDGroup restHUD;
    
    private bool useCombatOverride;
    private Dictionary<int, float> combatFillOverride = new Dictionary<int, float>();

    void Awake()
    {
        if (!runState)
            runState = FindFirstObjectByType<RunState>();
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

    public void Refresh()
    {
        if (!runState || runState.party == null)
            return;

        int partyCount = Mathf.Clamp(runState.party.MemberCount, 1, 4);

        RefreshGroup(travelHUD, partyCount);
        RefreshGroup(restHUD, partyCount);
    }

    private void RefreshGroup(PartyHUDGroup group, int partyCount)
    {
        RefreshBackground(group, partyCount);
        RefreshRows(group, partyCount);
    }

    private void RefreshBackground(PartyHUDGroup group, int partyCount)
    {
        if (group.background1) group.background1.SetActive(partyCount == 1);
        if (group.background2) group.background2.SetActive(partyCount == 2);
        if (group.background3) group.background3.SetActive(partyCount == 3);
        if (group.background4) group.background4.SetActive(partyCount == 4);
    }

    private void RefreshRows(PartyHUDGroup group, int partyCount)
    {
        if (group.rows == null)
            return;

        for (int i = 0; i < group.rows.Length; i++)
        {
            if (group.rows[i].root == null)
                continue;

            bool isUsedRow = i < partyCount;
            group.rows[i].root.SetActive(isUsedRow);

            if (!isUsedRow)
                continue;

            if (i < runState.party.members.Count)
            {
                var member = runState.party.members[i];
                if (member == null) continue;

                if (group.rows[i].nameText != null)
                    group.rows[i].nameText.text = member.memberName;

                if (group.rows[i].barFill != null)
                {
                    float fill;

                    if (useCombatOverride && combatFillOverride.TryGetValue(i, out float overrideFill))
                    {
                        fill = overrideFill;
                    }
                    else
                    {
                        fill = member.maxHealth > 0
                            ? member.currentHealth / (float)member.maxHealth
                            : 0f;
                    }

                    group.rows[i].barFill.fillAmount = Mathf.Clamp01(fill);
                }
            }
        }
    }
    
    public void SetMemberHealth(int index, int current, int max)
    {
        useCombatOverride = true;

        float fill = max > 0 ? current / (float)max : 0f;
        combatFillOverride[index] = Mathf.Clamp01(fill);
    }
    
    public void ClearCombatOverride()
    {
        useCombatOverride = false;
        combatFillOverride.Clear();
    }
}