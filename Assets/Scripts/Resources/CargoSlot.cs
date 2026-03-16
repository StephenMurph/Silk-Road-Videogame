using UnityEngine;

[System.Serializable]
public class CargoSlot
{
    public TradeGoodType goodType = TradeGoodType.None;
    [Min(0)] public int quantity = 0;

    public bool IsEmpty => goodType == TradeGoodType.None || quantity <= 0;

    public void Clear()
    {
        goodType = TradeGoodType.None;
        quantity = 0;
    }

    public bool CanStack(TradeGoodType type, int stackLimit)
    {
        if (type == TradeGoodType.None) return false;
        if (IsEmpty) return false;
        if (goodType != type) return false;
        return quantity < Mathf.Max(1, stackLimit);
    }

    public int SpaceLeft(int stackLimit)
    {
        if (IsEmpty) return Mathf.Max(1, stackLimit);
        return Mathf.Max(0, Mathf.Max(1, stackLimit) - quantity);
    }
}