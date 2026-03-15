using System;
using UnityEngine;

[Serializable]
public class RunResources
{
    [Min(0)] public int food = 20;
    [Min(0)] public int water = 20;
    [Min(0)] public int gold = 0;

    public int ConsumeFood(int amount)
    {
        amount = Mathf.Max(0, amount);
        int consumed = Mathf.Min(food, amount);
        food -= consumed;
        return consumed;
    }

    public int ConsumeWater(int amount)
    {
        amount = Mathf.Max(0, amount);
        int consumed = Mathf.Min(water, amount);
        water -= consumed;
        return consumed;
    }

    public void AddFood(int amount) => food += Mathf.Max(0, amount);
    public void AddWater(int amount) => water += Mathf.Max(0, amount);
    public void AddGold(int amount) => gold += Mathf.Max(0, amount);

    public bool SpendGold(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (gold < amount) return false;
        gold -= amount;
        return true;
    }
}