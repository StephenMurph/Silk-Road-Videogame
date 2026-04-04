using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public int worldSeed;
    public Vector2 worldOffset;
    public int currentTownId;

    public int food;
    public int water;
    public int gold;

    public List<PartyMemberSaveData> partyMembers = new();
    public List<TownSaveData> towns = new();

    public int marketCycleId;
    public int arrivalsSinceRefresh;
    public int nextRefreshThreshold;
}

[Serializable]
public class PartyMemberSaveData
{
    public string memberName;
    public int maxHealth;
    public int currentHealth;
    public int goldCostPerTurn;
    public int foodCostPerTurn;
    public int waterCostPerTurn;

    public CargoSlotSaveData slotA;
    public CargoSlotSaveData slotB;
}

[Serializable]
public class CargoSlotSaveData
{
    public int goodType;
    public int quantity;
}

[Serializable]
public class TownSaveData
{
    public string townName;
    public int biomeType;
    public int marketCycleId;
    public int foodPrice;
    public int waterPrice;

    public List<ShopItemSaveData> goods = new();
    public List<SellPriceSaveData> sellPrices = new();
    public List<RecruitCandidateSaveData> recruits = new();
}

[Serializable]
public class ShopItemSaveData
{
    public int type;
    public int price;
}

[Serializable]
public class SellPriceSaveData
{
    public int type;
    public int price;
}

[Serializable]
public class RecruitCandidateSaveData
{
    public string candidateName;
    public int goldCostPerTurn;
    public int foodCostPerTurn;
    public int waterCostPerTurn;
    public bool recruited;
}