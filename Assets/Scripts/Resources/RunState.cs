using UnityEngine;

public class RunState : MonoBehaviour
{
    [Header("Resources")]
    public RunResources resources = new RunResources();

    [Header("Party")]
    public PartyState party = new PartyState();

    [Header("Rules")]
    [Min(0)] public int foodPerMemberPerRoll = 1;
    [Min(0)] public int waterPerMemberPerRoll = 1;
    [Min(0)] public int goldPerMemberPerRoll = 0;

    [Min(0)] public int starvationDamagePerRoll = 10;
    [Min(0)] public int dehydrationDamagePerRoll = 10;

    void Awake()
    {
        party.InitializeIfNeeded();
    }

    public int GetFoodCostForTravelRoll()
    {
        return party.GetFoodCostPerRoll(foodPerMemberPerRoll);
    }

    public int GetWaterCostForTravelRoll()
    {
        return party.GetWaterCostPerRoll(waterPerMemberPerRoll);
    }

    public int GetGoldCostForTravelRoll()
    {
        return party.GetGoldCostPerRoll(goldPerMemberPerRoll);
    }

    public TravelSupplyResult ResolveSuppliesAfterTravelRoll()
    {
        int requiredFood = GetFoodCostForTravelRoll();
        int consumedFood = resources.ConsumeFood(requiredFood);
        bool foodShortage = consumedFood < requiredFood;

        int requiredWater = GetWaterCostForTravelRoll();
        int consumedWater = resources.ConsumeWater(requiredWater);
        bool waterShortage = consumedWater < requiredWater;

        int requiredGold = GetGoldCostForTravelRoll();
        int consumedGold = resources.ConsumeGold(requiredGold);
        bool goldShortage = consumedGold < requiredGold;

        return new TravelSupplyResult
        {
            requiredFood = requiredFood,
            consumedFood = consumedFood,
            foodShortageTriggered = foodShortage,

            requiredWater = requiredWater,
            consumedWater = consumedWater,
            waterShortageTriggered = waterShortage,

            requiredGold = requiredGold,
            consumedGold = consumedGold,
            goldShortageTriggered = goldShortage
        };
    }

    public int ApplyStarvationDamageToAllMembers()
    {
        return party.ApplyDamageToAllMembers(starvationDamagePerRoll);
    }

    public int ApplyDehydrationDamageToAllMembers()
    {
        return party.ApplyDamageToAllMembers(dehydrationDamagePerRoll);
    }
}

public struct TravelSupplyResult
{
    public int requiredFood;
    public int consumedFood;
    public bool foodShortageTriggered;

    public int requiredWater;
    public int consumedWater;
    public bool waterShortageTriggered;

    public int requiredGold;
    public int consumedGold;
    public bool goldShortageTriggered;
}