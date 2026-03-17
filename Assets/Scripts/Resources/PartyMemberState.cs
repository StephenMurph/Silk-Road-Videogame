using UnityEngine;

[System.Serializable]
public class PartyMemberState
{
    public string memberName = "Stephen";

    [Min(1)] public int maxHealth = 100;
    [Min(0)] public int currentHealth = 100;

    [Min(0)] public int goldCostPerTurn = 0;
    [Min(0)] public int foodCostPerTurn = 0;
    [Min(0)] public int waterCostPerTurn = 0;

    public CargoSlot slotA = new CargoSlot();
    public CargoSlot slotB = new CargoSlot();

    public void InitializeIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(memberName))
            memberName = "Stephen";

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        goldCostPerTurn = Mathf.Max(0, goldCostPerTurn);
        foodCostPerTurn = Mathf.Max(0, foodCostPerTurn);
        waterCostPerTurn = Mathf.Max(0, waterCostPerTurn);

        if (slotA == null) slotA = new CargoSlot();
        if (slotB == null) slotB = new CargoSlot();
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

    public CargoSlot[] GetSlots()
    {
        return new[] { slotA, slotB };
    }
}