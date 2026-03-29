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
        if (party == null || party.members == null || party.members.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < party.members.Count; i++)
        {
            var member = party.members[i];
            if (member == null) continue;
            if (member.IsDead()) continue;

            if (i == 0)
                total += Mathf.Max(0, foodPerMemberPerRoll);
            else
                total += Mathf.Max(0, member.foodCostPerTurn);
        }

        return total;
    }

    public int GetWaterCostForTravelRoll()
    {
        if (party == null || party.members == null || party.members.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < party.members.Count; i++)
        {
            var member = party.members[i];
            if (member == null) continue;
            if (member.IsDead()) continue;

            if (i == 0)
                total += Mathf.Max(0, waterPerMemberPerRoll);
            else
                total += Mathf.Max(0, member.waterCostPerTurn);
        }

        return total;
    }

    public int GetGoldCostForTravelRoll()
    {
        if (party == null || party.members == null || party.members.Count == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < party.members.Count; i++)
        {
            var member = party.members[i];
            if (member == null) continue;
            if (member.IsDead()) continue;

            if (i == 0)
                total += Mathf.Max(0, goldPerMemberPerRoll);
            else
                total += Mathf.Max(0, member.goldCostPerTurn);
        }

        return total;
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
    
    public bool IsLeaderDead()
    {
        if (party == null || party.members == null || party.members.Count == 0)
            return true;

        var leader = party.members[0];
        return leader == null || leader.IsDead();
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