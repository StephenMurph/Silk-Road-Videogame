using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private RectTransform fillHealthBar;

    private float fullWidth;

    private int currentHp;
    private int maxHp;

    private void Awake()
    {
        if (!root)
            root = gameObject;

        if (fillHealthBar != null)
            fullWidth = fillHealthBar.sizeDelta.x;

        root.SetActive(false);
    }
    
    public void ShowEnemy(string enemyName, int hp)
    {
        maxHp = hp;
        currentHp = hp;

        if (nameText)
            nameText.text = enemyName;

        root.SetActive(true);
        UpdateUI();
    }
    
    public void SetHealth(int hp)
    {
        currentHp = Mathf.Clamp(hp, 0, maxHp);
        UpdateUI();
    }

    private void UpdateUI()
    {
        float t = maxHp > 0 ? (float)currentHp / maxHp : 0f;

        if (fillHealthBar)
        {
            Vector2 size = fillHealthBar.sizeDelta;
            size.x = fullWidth * t;
            fillHealthBar.sizeDelta = size;
        }
    }
    
    public void Hide()
    {
        root.SetActive(false);
    }
}