using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PartyState
{
    [Header("Party Rules")]
    [Min(1)] public int maxPartySize = 4;
    [Min(1)] public int cargoSlotsPerMember = 2;
    [Min(1)] public int stackLimitPerSlot = 10;

    [Header("Members")]
    public List<PartyMemberState> members = new();

    public int MemberCount => members != null ? members.Count : 0;

    public void InitializeIfNeeded()
    {
        maxPartySize = Mathf.Max(1, maxPartySize);
        cargoSlotsPerMember = Mathf.Max(1, cargoSlotsPerMember);
        stackLimitPerSlot = Mathf.Max(1, stackLimitPerSlot);

        if (members == null)
            members = new List<PartyMemberState>();

        if (members.Count == 0)
        {
            members.Add(new PartyMemberState
            {
                memberName = "Stephen",
                maxHealth = 100,
                currentHealth = 100
            });
        }

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null)
                members[i] = new PartyMemberState();

            members[i].InitializeIfNeeded();
        }

        if (members.Count > maxPartySize)
            members.RemoveRange(maxPartySize, members.Count - maxPartySize);
    }

    public int GetFoodCostPerRoll(int foodPerMemberPerRoll)
    {
        return Mathf.Max(0, MemberCount) * Mathf.Max(0, foodPerMemberPerRoll);
    }

    public int GetWaterCostPerRoll(int waterPerMemberPerRoll)
    {
        return Mathf.Max(0, MemberCount) * Mathf.Max(0, waterPerMemberPerRoll);
    }

    public int GetGoldCostPerRoll(int goldPerMemberPerRoll)
    {
        return Mathf.Max(0, MemberCount) * Mathf.Max(0, goldPerMemberPerRoll);
    }

    public PartyMemberState GetLeader()
    {
        if (members == null || members.Count == 0)
            return null;

        return members[0];
    }

    public int TotalCargoSlotCount()
    {
        return MemberCount * cargoSlotsPerMember;
    }

    public int UsedCargoSlotCount()
    {
        int used = 0;

        if (members == null) return 0;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null) continue;

            var slots = members[i].GetSlots();
            for (int j = 0; j < slots.Length; j++)
            {
                if (slots[j] != null && !slots[j].IsEmpty)
                    used++;
            }
        }

        return used;
    }

    public bool TryAddGoods(TradeGoodType type, int amount)
    {
        if (type == TradeGoodType.None || amount <= 0)
            return false;

        if (members == null || members.Count == 0)
            return false;

        int remaining = amount;

        for (int i = 0; i < members.Count; i++)
        {
            var slots = members[i].GetSlots();
            for (int j = 0; j < slots.Length; j++)
            {
                var slot = slots[j];
                if (slot == null) continue;
                if (!slot.CanStack(type, stackLimitPerSlot)) continue;

                int add = Mathf.Min(remaining, slot.SpaceLeft(stackLimitPerSlot));
                slot.quantity += add;
                remaining -= add;

                if (remaining <= 0)
                    return true;
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            var slots = members[i].GetSlots();
            for (int j = 0; j < slots.Length; j++)
            {
                var slot = slots[j];
                if (slot == null) continue;
                if (!slot.IsEmpty) continue;

                int add = Mathf.Min(remaining, stackLimitPerSlot);
                slot.goodType = type;
                slot.quantity = add;
                remaining -= add;

                if (remaining <= 0)
                    return true;
            }
        }

        return false;
    }

    public int RemoveGoods(TradeGoodType type, int amount)
    {
        if (type == TradeGoodType.None || amount <= 0)
            return 0;

        if (members == null || members.Count == 0)
            return 0;

        int removed = 0;
        int remaining = amount;

        for (int i = 0; i < members.Count; i++)
        {
            var slots = members[i].GetSlots();
            for (int j = 0; j < slots.Length; j++)
            {
                var slot = slots[j];
                if (slot == null || slot.IsEmpty) continue;
                if (slot.goodType != type) continue;

                int take = Mathf.Min(remaining, slot.quantity);
                slot.quantity -= take;
                removed += take;
                remaining -= take;

                if (slot.quantity <= 0)
                    slot.Clear();

                if (remaining <= 0)
                    return removed;
            }
        }

        return removed;
    }

    public int GetTotalQuantityOfGood(TradeGoodType type)
    {
        if (type == TradeGoodType.None || members == null)
            return 0;

        int total = 0;

        for (int i = 0; i < members.Count; i++)
        {
            var slots = members[i].GetSlots();
            for (int j = 0; j < slots.Length; j++)
            {
                var slot = slots[j];
                if (slot == null || slot.IsEmpty) continue;
                if (slot.goodType != type) continue;

                total += slot.quantity;
            }
        }

        return total;
    }

    public bool CanFitGoods(TradeGoodType type, int amount)
    {
        if (type == TradeGoodType.None || amount <= 0)
            return false;

        int remaining = amount;

        if (members == null || members.Count == 0)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            var slots = members[i].GetSlots();
            for (int j = 0; j < slots.Length; j++)
            {
                var slot = slots[j];
                if (slot == null) continue;

                if (slot.IsEmpty)
                {
                    remaining -= stackLimitPerSlot;
                }
                else if (slot.goodType == type)
                {
                    remaining -= slot.SpaceLeft(stackLimitPerSlot);
                }

                if (remaining <= 0)
                    return true;
            }
        }

        return false;
    }

    public bool TryAddMember(string newMemberName)
    {
        InitializeIfNeeded();

        if (MemberCount >= maxPartySize)
            return false;

        members.Add(new PartyMemberState
        {
            memberName = string.IsNullOrWhiteSpace(newMemberName) ? $"Member {MemberCount + 1}" : newMemberName,
            maxHealth = 100,
            currentHealth = 100
        });

        return true;
    }

    public bool TryRemoveMemberAt(int memberIndex)
    {
        if (members == null) return false;
        if (memberIndex < 0 || memberIndex >= members.Count) return false;
        if (members.Count <= 1) return false;

        members.RemoveAt(memberIndex);
        return true;
    }

    public int ApplyDamageToLeader(int amount)
    {
        var leader = GetLeader();
        if (leader == null) return 0;

        return leader.ApplyDamage(amount);
    }

    public int HealLeader(int amount)
    {
        var leader = GetLeader();
        if (leader == null) return 0;

        return leader.Heal(amount);
    }

    public bool IsLeaderDead()
    {
        var leader = GetLeader();
        return leader == null || leader.IsDead();
    }

    public int ApplyDamageToAllMembers(int amount)
    {
        if (members == null || members.Count == 0)
            return 0;

        int totalDamageApplied = 0;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null) continue;
            totalDamageApplied += members[i].ApplyDamage(amount);
        }

        return totalDamageApplied;
    }

    public void HealAllMembers(int amount)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].IsDead()) continue;

            members[i].Heal(amount);
        }
    }

    public int CountDeadMembers()
    {
        if (members == null || members.Count == 0)
            return 0;

        int dead = 0;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null) continue;
            if (members[i].IsDead()) dead++;
        }

        return dead;
    }
}