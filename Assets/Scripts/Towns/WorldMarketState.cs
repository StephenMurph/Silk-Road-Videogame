using UnityEngine;

public class WorldMarketState : MonoBehaviour
{
    [Header("Market Cycle")]
    public int currentMarketCycleId = 0;

    [Header("Refresh Rules")]
    public int arrivalsSinceRefresh = 0;
    public int nextRefreshThreshold = 3;

    private void Awake()
    {
        RollNextThreshold();
    }

    public void OnTownArrival(TownManager townSystem, int arrivedTownIndex)
    {
        arrivalsSinceRefresh++;

        if (arrivalsSinceRefresh >= nextRefreshThreshold)
        {
            Debug.Log($"=== MARKET REFRESH on arrival at town index {arrivedTownIndex} (excluding current town) ===");

            currentMarketCycleId++;
            arrivalsSinceRefresh = 0;

            RollNextThreshold();

            if (townSystem != null)
                townSystem.RefreshAllTownMarketsExcept(currentMarketCycleId, arrivedTownIndex);
        }
    }

    private void RollNextThreshold()
    {
        nextRefreshThreshold = Random.Range(3, 5);
    }
}