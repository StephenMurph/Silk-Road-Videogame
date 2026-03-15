using UnityEngine;

[System.Serializable]
public class PartyState
{
    [Header("Party")]
    [Min(1)] public int memberCount = 1;

    [Header("Leader")]
    public string leaderName = "Stephen";

    [Header("Leader Health")]
    [Min(1)] public int maxHealth = 100;
    [Min(0)] public int currentHealth = 100;

    public int GetFoodCostPerRoll(int foodPerMemberPerRoll)
    {
        return Mathf.Max(1, memberCount) * Mathf.Max(0, foodPerMemberPerRoll);
    }

    public void InitializeHealthIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(leaderName))
            leaderName = "Stephen";

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public int ApplyDamage(int amount)
    {
        amount = Mathf.Max(0, amount);

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);

        return before - currentHealth;
    }

    public int Heal(int amount)
    {
        amount = Mathf.Max(0, amount);

        int before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        return currentHealth - before;
    }

    public bool IsDead()
    {
        return currentHealth <= 0;
    }
}