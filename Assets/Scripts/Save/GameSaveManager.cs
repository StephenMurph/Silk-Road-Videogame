using System.Collections.Generic;
using UnityEngine;

public class GameSaveManager : MonoBehaviour
{
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private RunState runState;
    [SerializeField] private WorldMapUI worldMapUI;
    [SerializeField] private WorldMarketState worldMarketState;
    [SerializeField] private TownManager townManager;

    private void Awake()
    {
        if (!terrainManager) terrainManager = FindFirstObjectByType<TerrainManager>();
        if (!runState) runState = FindFirstObjectByType<RunState>();
        if (!worldMapUI) worldMapUI = FindFirstObjectByType<WorldMapUI>();
        if (!worldMarketState) worldMarketState = FindFirstObjectByType<WorldMarketState>();
        if (!townManager) townManager = FindFirstObjectByType<TownManager>();
    }

    public void AutoSaveAtTown(int townId)
    {
        if (terrainManager == null || runState == null || townManager == null)
        {
            Debug.LogError("GameSaveManager: Missing references.");
            return;
        }

        var data = new GameSaveData
        {
            worldSeed = terrainManager.seed,
            worldOffset = terrainManager.offset,
            currentTownId = townId,

            food = runState.resources.food,
            water = runState.resources.water,
            gold = runState.resources.gold,

            marketCycleId = worldMarketState != null ? worldMarketState.currentMarketCycleId : 0,
            arrivalsSinceRefresh = worldMarketState != null ? worldMarketState.arrivalsSinceRefresh : 0,
            nextRefreshThreshold = worldMarketState != null ? worldMarketState.nextRefreshThreshold : 3
        };

        SaveParty(data);
        SaveTowns(data);

        GameSaveSystem.Save(data);
    }

    public void ApplyLoadedSave(GameSaveData data)
    {
        if (data == null)
            return;

        if (terrainManager != null)
        {
            terrainManager.seed = data.worldSeed;
            terrainManager.offset = data.worldOffset;
        }

        RestoreRunState(data);

        if (worldMapUI != null)
        {
            worldMapUI.startAtRandomTown = false;
            worldMapUI.currentTownId = data.currentTownId;
        }

        if (worldMarketState != null)
        {
            worldMarketState.currentMarketCycleId = data.marketCycleId;
            worldMarketState.arrivalsSinceRefresh = data.arrivalsSinceRefresh;
            worldMarketState.nextRefreshThreshold = data.nextRefreshThreshold;
        }

        if (townManager != null)
            RestoreTownData(data);

        GameLaunchState.SuppressTownArrivalEffects = true;
    }

    private void SaveParty(GameSaveData data)
    {
        if (runState.party == null || runState.party.members == null)
            return;

        foreach (var member in runState.party.members)
        {
            if (member == null) continue;

            data.partyMembers.Add(new PartyMemberSaveData
            {
                memberName = member.memberName,
                maxHealth = member.maxHealth,
                currentHealth = member.currentHealth,
                goldCostPerTurn = member.goldCostPerTurn,
                foodCostPerTurn = member.foodCostPerTurn,
                waterCostPerTurn = member.waterCostPerTurn,
                slotA = ToCargoSave(member.slotA),
                slotB = ToCargoSave(member.slotB)
            });
        }
    }

    private void RestoreRunState(GameSaveData data)
    {
        if (runState == null)
            return;

        runState.resources.food = data.food;
        runState.resources.water = data.water;
        runState.resources.gold = data.gold;

        if (runState.party == null)
            return;

        runState.party.members = new List<PartyMemberState>();

        foreach (var savedMember in data.partyMembers)
        {
            var member = new PartyMemberState
            {
                memberName = savedMember.memberName,
                maxHealth = savedMember.maxHealth,
                currentHealth = savedMember.currentHealth,
                goldCostPerTurn = savedMember.goldCostPerTurn,
                foodCostPerTurn = savedMember.foodCostPerTurn,
                waterCostPerTurn = savedMember.waterCostPerTurn,
                slotA = FromCargoSave(savedMember.slotA),
                slotB = FromCargoSave(savedMember.slotB)
            };

            member.InitializeIfNeeded();
            runState.party.members.Add(member);
        }

        runState.party.InitializeIfNeeded();
    }

    private void SaveTowns(GameSaveData data)
    {
        if (townManager == null || townManager.towns == null)
            return;

        for (int i = 0; i < townManager.towns.Count; i++)
        {
            var town = townManager.towns[i];
            if (town == null) continue;

            var savedTown = new TownSaveData
            {
                townName = town.townName,
                biomeType = (int)town.biomeType,
                marketCycleId = town.marketData != null ? town.marketData.marketCycleId : 0,
                foodPrice = town.marketData != null ? town.marketData.foodPrice : 0,
                waterPrice = town.marketData != null ? town.marketData.waterPrice : 0
            };

            if (town.marketData != null && town.marketData.goods != null)
            {
                foreach (var item in town.marketData.goods)
                {
                    if (item == null) continue;

                    savedTown.goods.Add(new ShopItemSaveData
                    {
                        type = (int)item.type,
                        price = item.price
                    });
                }
            }

            if (town.marketData != null && town.marketData.sellPrices != null)
            {
                foreach (var item in town.marketData.sellPrices)
                {
                    if (item == null) continue;

                    savedTown.sellPrices.Add(new SellPriceSaveData
                    {
                        type = (int)item.type,
                        price = item.price
                    });
                }
            }

            if (town.recruitCandidates != null)
            {
                foreach (var recruit in town.recruitCandidates)
                {
                    if (recruit == null) continue;

                    savedTown.recruits.Add(new RecruitCandidateSaveData
                    {
                        candidateName = recruit.candidateName,
                        goldCostPerTurn = recruit.goldCostPerTurn,
                        foodCostPerTurn = recruit.foodCostPerTurn,
                        waterCostPerTurn = recruit.waterCostPerTurn,
                        recruited = recruit.recruited
                    });
                }
            }

            data.towns.Add(savedTown);
        }
    }

    private void RestoreTownData(GameSaveData data)
    {
        if (townManager == null || townManager.towns == null)
            return;

        int count = Mathf.Min(townManager.towns.Count, data.towns.Count);

        for (int i = 0; i < count; i++)
        {
            var town = townManager.towns[i];
            var saved = data.towns[i];

            if (town == null || saved == null)
                continue;

            town.townName = saved.townName;
            town.biomeType = (TownManager.TownBiomeType)saved.biomeType;
            town.townPhoto = GetPhotoForTownName(saved.townName);

            town.marketData = new TownManager.TownMarketData
            {
                marketCycleId = saved.marketCycleId,
                foodPrice = saved.foodPrice,
                waterPrice = saved.waterPrice,
                goods = new TownManager.ShopItemData[4],
                sellPrices = new TownManager.SellPriceData[saved.sellPrices.Count]
            };

            for (int g = 0; g < town.marketData.goods.Length; g++)
            {
                if (g >= saved.goods.Count) break;

                town.marketData.goods[g] = new TownManager.ShopItemData
                {
                    type = (TradeGoodType)saved.goods[g].type,
                    price = saved.goods[g].price
                };
            }

            for (int s = 0; s < saved.sellPrices.Count; s++)
            {
                town.marketData.sellPrices[s] = new TownManager.SellPriceData
                {
                    type = (TradeGoodType)saved.sellPrices[s].type,
                    price = saved.sellPrices[s].price
                };
            }

            town.recruitCandidates = new RecruitCandidateData[saved.recruits.Count];
            for (int r = 0; r < saved.recruits.Count; r++)
            {
                town.recruitCandidates[r] = new RecruitCandidateData
                {
                    candidateName = saved.recruits[r].candidateName,
                    goldCostPerTurn = saved.recruits[r].goldCostPerTurn,
                    foodCostPerTurn = saved.recruits[r].foodCostPerTurn,
                    waterCostPerTurn = saved.recruits[r].waterCostPerTurn,
                    recruited = saved.recruits[r].recruited
                };
            }
        }
    }

    private Sprite GetPhotoForTownName(string townName)
    {
        if (townManager == null || townManager.townPhotos == null || string.IsNullOrWhiteSpace(townName))
            return null;

        for (int i = 0; i < townManager.townPhotos.Count; i++)
        {
            var entry = townManager.townPhotos[i];
            if (entry == null) continue;

            if (string.Equals(entry.townName, townName, System.StringComparison.OrdinalIgnoreCase))
                return entry.photo;
        }

        return null;
    }

    private static CargoSlotSaveData ToCargoSave(CargoSlot slot)
    {
        if (slot == null)
            return new CargoSlotSaveData { goodType = 0, quantity = 0 };

        return new CargoSlotSaveData
        {
            goodType = (int)slot.goodType,
            quantity = slot.quantity
        };
    }

    private static CargoSlot FromCargoSave(CargoSlotSaveData saved)
    {
        if (saved == null)
            return new CargoSlot();

        return new CargoSlot
        {
            goodType = (TradeGoodType)saved.goodType,
            quantity = saved.quantity
        };
    }
}