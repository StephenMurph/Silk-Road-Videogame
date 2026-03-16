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

    [Header("Refs")]
    [SerializeField] private RunState runState;
    [SerializeField] private PartyRowUI[] rows = new PartyRowUI[4];

    [Header("Background Variants")]
    [SerializeField] private GameObject background1;
    [SerializeField] private GameObject background2;
    [SerializeField] private GameObject background3;
    [SerializeField] private GameObject background4;

    void Awake()
    {
        if (!runState)
            runState = FindFirstObjectByType<RunState>();
    }

    void Update()
    {
        Refresh();
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void Refresh()
    {
        if (!runState || runState.party == null)
            return;

        int partyCount = Mathf.Clamp(runState.party.MemberCount, 1, 4);

        RefreshBackground(partyCount);
        RefreshRows(partyCount);
    }

    private void RefreshBackground(int partyCount)
    {
        if (background1) background1.SetActive(partyCount == 1);
        if (background2) background2.SetActive(partyCount == 2);
        if (background3) background3.SetActive(partyCount == 3);
        if (background4) background4.SetActive(partyCount == 4);
    }

    private void RefreshRows(int partyCount)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].root == null)
                continue;

            bool isUsedRow = i < partyCount;
            rows[i].root.SetActive(isUsedRow);

            if (!isUsedRow)
                continue;

            if (i < runState.party.members.Count)
            {
                var member = runState.party.members[i];
                if (member == null) continue;

                if (rows[i].nameText != null)
                    rows[i].nameText.text = member.memberName;

                if (rows[i].barFill != null)
                {
                    float fill = member.maxHealth > 0
                        ? member.currentHealth / (float)member.maxHealth
                        : 0f;

                    rows[i].barFill.fillAmount = Mathf.Clamp01(fill);
                }
            }
        }
    }
}