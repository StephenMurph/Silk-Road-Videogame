using System;
using System.Collections.Generic;
using UnityEngine;


public class TownManager : MonoBehaviour
{
    [Header("Refs")]
    public TerrainManager terrainManager;   
    public Terrain terrain;                

    [Header("Towns")]
    public GameObject housePrefab;
    public int townCount = 8;
    public int townSeed = 1234;

    [Tooltip("Minimum spacing between towns in world meters.")]
    public float townMinSpacingWorld = 120f;

    [Tooltip("Reject town placement if slope is above this.")]
    public float townMaxSlope = 18f;

    [Tooltip("Reject town placement if height01 <= seaLevel + buffer.")]
    public float seaBuffer01 = 0.01f;

    [Tooltip("Don’t place towns in strong mountain biome.")]
    [Range(0f, 1f)] public float mountainBlockCutoff = 0.5f;
    
    [Header("Debug")]
    public bool clearExistingTowns = true;
    public string townRootName = "TownsRoot";

    [Header("Biome / Market")]
    [Range(0f, 1f)] public float desertCutoff = 0.35f;
    
    [Header("Biome Rules")]
    [Range(0f, 0.5f)] public float biomeEdgeBuffer = 0.08f;
    
    [Header("Town Photos")]
    public List<TownPhotoEntry> townPhotos = new();
    
    [Header("Town Name Pools")]
    public List<string> grasslandTownNames = new()
    {
        "Xi'an",
        "Kaifeng",
        "Luoyang",
        "Chengdu",
        "Hangzhou",
        "Suzhou",
        "Lahore",
        "Multan",
        "Varanasi",
        "Kannauj",
        "Ahmedabad",
        "Gwalior"
    };

    public List<string> desertTownNames = new()
    {
        "Samarkand",
        "Bukhara",
        "Khiva",
        "Balkh",
        "Kashgar",
        "Turpan",
        "Dunhuang",
        "Yazd",
        "Isfahan",
        "Shiraz",
        "Bam",
        "Delhi",
        "Lanzhou"
    };
    
    public List<string> chineseRecruitNames = new()
    {
        "Wei", "Jun", "Liang", "Shen", "Bao", "Ming", "Tao", "Rui", "Qiao", "Fen", "Lian", "Mei"
    };

    public List<string> middleEasternRecruitNames = new()
    {
        "Hasan", "Yusuf", "Karim", "Farid", "Rashid", "Hamza", "Tariq", "Layla", "Zaynab", "Maryam", "Safiya", "Nadia"
    };

    public List<string> indianRecruitNames = new()
    {
        "Arjun", "Dev", "Ravi", "Kiran", "Vikram", "Anand", "Priya", "Asha", "Meera", "Kavita", "Leela", "Sita"
    };
    
    public enum TownBiomeType
    {
        Grassland,
        Desert
    }
    
    [Serializable]
    public class TownPhotoEntry
    {
        public string townName;
        public Sprite photo;
    }
    
    [Serializable]
    public class TownInstance
    {
        public Vector2 nz;
        public GameObject go;

        public string townName;
        public TownBiomeType biomeType;
        public Sprite townPhoto;
        
        public TownMarketData marketData;
        
        public RecruitCandidateData[] recruitCandidates = new RecruitCandidateData[3];
    }
    
    [System.Serializable]
    public class ShopItemData
    {
        public TradeGoodType type;
        public int price;
    }
    
    [Serializable]
    public class SellPriceData
    {
        public TradeGoodType type;
        public int price;
    }

    [System.Serializable]
    public class TownMarketData
    {
        public int marketCycleId;

        public int foodPrice;
        public int waterPrice;
        
        public ShopItemData[] goods = new ShopItemData[4];
        public SellPriceData[] sellPrices;
    }

    public readonly List<TownInstance> towns = new();

    public void GenerateTowns()
    {
        if (!terrainManager) terrainManager = GetComponent<TerrainManager>();
        if (!terrain) terrain = terrainManager ? terrainManager.GetComponent<Terrain>() : null;

        if (!terrainManager || !terrain || !housePrefab)
        {
            Debug.LogError("TownManager: missing refs (terrainManager/terrain/housePrefab).");
            return;
        }

        SpawnTowns(terrain.terrainData);
        RefreshAllTownMarkets(0);

        Debug.Log($"TownManager: spawned {towns.Count} towns.");
    }

    void SpawnTowns(TerrainData data)
    {
        towns.Clear();

        Transform root = transform.Find(townRootName);
        if (!root)
        {
            var go = new GameObject(townRootName);
            go.transform.SetParent(transform, false);
            root = go.transform;
        }

        if (clearExistingTowns)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
        }

        var rng = new System.Random(townSeed);
        HashSet<string> usedTownNames = new HashSet<string>();

        Dictionary<int, List<Vector2>> buckets = null;
        float cellSize = townMinSpacingWorld;
        if (townMinSpacingWorld > 0f)
            buckets = new Dictionary<int, List<Vector2>>(townCount);

        static int Hash(int x, int z) { unchecked { return x * 73856093 ^ z * 19349663; } }

        bool IsValidTownSpot(float nx, float nz)
        {
            float m = terrainManager.SendMessageMountainMask(nx, nz);
            if (m >= mountainBlockCutoff) return false;
            
            float edge = terrainManager.SendMessageEdgeMask(nx, nz);
            if (edge > 0.10f) return false;
            
            float desert = terrainManager.SendMessageDesertMask(nx, nz);
            
            if (Mathf.Abs(desert - desertCutoff) < biomeEdgeBuffer)
                return false;

            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            if (h01 <= terrainManager.seaLevel01 + seaBuffer01) return false;

            float slope = data.GetSteepness(nx, nz);
            if (slope > townMaxSlope) return false;

            return true;
        }

        bool RespectsSpacing(float nx, float nz)
        {
            if (buckets == null) return true;

            Vector2 pW = new Vector2(nx * data.size.x, nz * data.size.z);
            int cx = Mathf.FloorToInt(pW.x / cellSize);
            int cz = Mathf.FloorToInt(pW.y / cellSize);

            float minSqr = townMinSpacingWorld * townMinSpacingWorld;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = Hash(cx + dx, cz + dz);
                if (!buckets.TryGetValue(key, out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    if ((list[i] - pW).sqrMagnitude < minSqr)
                        return false;
                }
            }

            return true;
        }

        void RegisterSpacing(float nx, float nz)
        {
            if (buckets == null) return;

            Vector2 pW = new Vector2(nx * data.size.x, nz * data.size.z);
            int cx = Mathf.FloorToInt(pW.x / cellSize);
            int cz = Mathf.FloorToInt(pW.y / cellSize);

            int key = Hash(cx, cz);
            if (!buckets.TryGetValue(key, out var list))
                buckets[key] = list = new List<Vector2>(4);

            list.Add(pW);
        }

        void AddTown(float nx, float nz)
        {
            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            Vector3 world = new Vector3(nx * data.size.x, h01 * data.size.y, nz * data.size.z) + terrain.transform.position;

            var goTown = Instantiate(housePrefab, world, Quaternion.identity, root);
            goTown.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            TownBiomeType biomeType = GetTownBiomeType(nx, nz);
            string townName = GetRandomTownName(biomeType, rng, usedTownNames);
            usedTownNames.Add(townName);

            Sprite townPhoto = GetTownPhoto(townName);
            RecruitCandidateData[] recruitCandidates = GenerateRecruitCandidates(biomeType, rng);

            towns.Add(new TownInstance
            {
                nz = new Vector2(nx, nz),
                go = goTown,
                townName = townName,
                biomeType = biomeType,
                townPhoto = townPhoto,
                recruitCandidates = recruitCandidates,
            });

            RegisterSpacing(nx, nz);

            Debug.Log($"Spawned town: {townName} ({biomeType})");
        }

        bool TryFindValidPointInRect(Rect rect, int tries, out Vector2 point)
        {
            for (int i = 0; i < tries; i++)
            {
                float nx = Mathf.Lerp(rect.xMin, rect.xMax, (float)rng.NextDouble());
                float nz = Mathf.Lerp(rect.yMin, rect.yMax, (float)rng.NextDouble());

                if (!IsValidTownSpot(nx, nz)) continue;
                if (!RespectsSpacing(nx, nz)) continue;

                point = new Vector2(nx, nz);
                return true;
            }

            point = default;
            return false;
        }

        bool TryFindCorridorPoint(Vector2 a, Vector2 b, float width01, int tries, out Vector2 point)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            for (int i = 0; i < tries; i++)
            {
                float t = Mathf.Lerp(0.08f, 0.92f, (float)rng.NextDouble());
                float lateral = ((float)rng.NextDouble() * 2f - 1f) * width01;

                Vector2 p = Vector2.Lerp(a, b, t) + perp * lateral;
                p.x = Mathf.Clamp01(p.x);
                p.y = Mathf.Clamp01(p.y);

                if (!IsValidTownSpot(p.x, p.y)) continue;
                if (!RespectsSpacing(p.x, p.y)) continue;

                point = p;
                return true;
            }

            point = default;
            return false;
        }

        bool TryFindBranchPoint(Vector2 a, Vector2 b, float minOffset01, float maxOffset01, int tries, out Vector2 point)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            for (int i = 0; i < tries; i++)
            {
                float t = Mathf.Lerp(0.12f, 0.88f, (float)rng.NextDouble());
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float lateral = Mathf.Lerp(minOffset01, maxOffset01, (float)rng.NextDouble()) * side;

                Vector2 p = Vector2.Lerp(a, b, t) + perp * lateral;
                p.x = Mathf.Clamp01(p.x);
                p.y = Mathf.Clamp01(p.y);

                if (!IsValidTownSpot(p.x, p.y)) continue;
                if (!RespectsSpacing(p.x, p.y)) continue;

                point = p;
                return true;
            }

            point = default;
            return false;
        }

        // ---------- 1) choose two endpoint regions ----------
        Rect leftRect = new Rect(0.05f, 0.10f, 0.20f, 0.80f);
        Rect rightRect = new Rect(0.75f, 0.10f, 0.20f, 0.80f);

        if (!TryFindValidPointInRect(leftRect, 500, out Vector2 startTown) ||
            !TryFindValidPointInRect(rightRect, 500, out Vector2 endTown))
        {
            Debug.LogWarning("TownRoadSystem: failed to find corridor endpoints, falling back to random spawn.");
            SpawnTownsFallback(data, root, rng, usedTownNames, buckets);
            return;
        }

        AddTown(startTown.x, startTown.y);
        AddTown(endTown.x, endTown.y);

        // ---------- 2) spawn corridor towns ----------
        int remaining = Mathf.Max(0, townCount - 2);
        int corridorCount = Mathf.RoundToInt(remaining * 0.6f);
        int branchCount = remaining - corridorCount;

        for (int i = 0; i < corridorCount && towns.Count < townCount; i++)
        {
            if (TryFindCorridorPoint(startTown, endTown, 0.10f, 600, out Vector2 p))
                AddTown(p.x, p.y);
        }

        // ---------- 3) spawn branch towns ----------
        for (int i = 0; i < branchCount && towns.Count < townCount; i++)
        {
            if (TryFindBranchPoint(startTown, endTown, 0.12f, 0.24f, 700, out Vector2 p))
                AddTown(p.x, p.y);
        }

        // ---------- 4) emergency fill ----------
        int safety = Mathf.Max(2000, townCount * 300);
        for (int tries = 0; tries < safety && towns.Count < townCount; tries++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            if (!IsValidTownSpot(nx, nz)) continue;
            if (!RespectsSpacing(nx, nz)) continue;

            AddTown(nx, nz);
        }

        if (towns.Count < townCount)
            Debug.LogWarning($"TownRoadSystem: only spawned {towns.Count}/{townCount} towns.");
    }
    
        private void SpawnTownsFallback(
        TerrainData data,
        Transform root,
        System.Random rng,
        HashSet<string> usedTownNames,
        Dictionary<int, List<Vector2>> buckets
    )
    {
        towns.Clear();

        float cellSize = townMinSpacingWorld;
        static int Hash(int x, int z) { unchecked { return x * 73856093 ^ z * 19349663; } }

        bool RespectsSpacing(float nx, float nz)
        {
            if (buckets == null) return true;

            Vector2 pW = new Vector2(nx * data.size.x, nz * data.size.z);
            int cx = Mathf.FloorToInt(pW.x / cellSize);
            int cz = Mathf.FloorToInt(pW.y / cellSize);

            float minSqr = townMinSpacingWorld * townMinSpacingWorld;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = Hash(cx + dx, cz + dz);
                if (!buckets.TryGetValue(key, out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    if ((list[i] - pW).sqrMagnitude < minSqr)
                        return false;
                }
            }

            return true;
        }

        void RegisterSpacing(float nx, float nz)
        {
            if (buckets == null) return;

            Vector2 pW = new Vector2(nx * data.size.x, nz * data.size.z);
            int cx = Mathf.FloorToInt(pW.x / cellSize);
            int cz = Mathf.FloorToInt(pW.y / cellSize);

            int key = Hash(cx, cz);
            if (!buckets.TryGetValue(key, out var list))
                buckets[key] = list = new List<Vector2>(4);

            list.Add(pW);
        }

        int safety = Mathf.Max(2000, townCount * 200);

        for (int tries = 0; tries < safety && towns.Count < townCount; tries++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            float m = terrainManager.SendMessageMountainMask(nx, nz);
            if (m >= mountainBlockCutoff) continue;

            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            if (h01 <= terrainManager.seaLevel01 + seaBuffer01) continue;

            float slope = data.GetSteepness(nx, nz);
            if (slope > townMaxSlope) continue;

            if (!RespectsSpacing(nx, nz)) continue;

            Vector3 world = new Vector3(nx * data.size.x, h01 * data.size.y, nz * data.size.z) + terrain.transform.position;
            var goTown = Instantiate(housePrefab, world, Quaternion.identity, root);
            goTown.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            TownBiomeType biomeType = GetTownBiomeType(nx, nz);
            string townName = GetRandomTownName(biomeType, rng, usedTownNames);
            usedTownNames.Add(townName);

            Sprite townPhoto = GetTownPhoto(townName);
            RecruitCandidateData[] recruitCandidates = GenerateRecruitCandidates(biomeType, rng);

            towns.Add(new TownInstance
            {
                nz = new Vector2(nx, nz),
                go = goTown,
                townName = townName,
                biomeType = biomeType,
                townPhoto = townPhoto,
                recruitCandidates = recruitCandidates
            });

            RegisterSpacing(nx, nz);
        }
    }
    
    private string GetRandomTownName(TownBiomeType biomeType, System.Random rng, HashSet<string> usedNames)
    {
        List<string> source = biomeType == TownBiomeType.Desert
            ? desertTownNames
            : grasslandTownNames;

        List<string> available = new List<string>();
        for (int i = 0; i < source.Count; i++)
        {
            if (!usedNames.Contains(source[i]))
                available.Add(source[i]);
        }

        if (available.Count == 0)
        {
            string fallback = source[rng.Next(source.Count)];
            return fallback;
        }

        return available[rng.Next(available.Count)];
    }

    private TownBiomeType GetTownBiomeType(float nx, float nz)
    {
        float desert = terrainManager.SendMessageDesertMask(nx, nz);
        return desert >= desertCutoff ? TownBiomeType.Desert : TownBiomeType.Grassland;
    }
    
    private Sprite GetTownPhoto(string townName)
    {
        if (string.IsNullOrWhiteSpace(townName) || townPhotos == null)
            return null;

        for (int i = 0; i < townPhotos.Count; i++)
        {
            if (townPhotos[i] == null) continue;

            if (string.Equals(townPhotos[i].townName, townName, StringComparison.OrdinalIgnoreCase))
                return townPhotos[i].photo;
        }

        return null;
    }
    
    private string GetRandomRecruitName(TownBiomeType biomeType, System.Random rng)
    {
        List<string> pool = new List<string>();

        if (biomeType == TownBiomeType.Grassland)
        {
            pool.AddRange(chineseRecruitNames);
            pool.AddRange(indianRecruitNames);
        }
        else
        {
            pool.AddRange(middleEasternRecruitNames);
            pool.AddRange(indianRecruitNames);
            pool.AddRange(chineseRecruitNames);
        }

        return pool[rng.Next(pool.Count)];
    }

    private RecruitCandidateData[] GenerateRecruitCandidates(TownBiomeType biomeType, System.Random rng)
    {
        RecruitCandidateData[] result = new RecruitCandidateData[3];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new RecruitCandidateData
            {
                candidateName = GetRandomRecruitName(biomeType, rng),
                goldCostPerTurn = rng.Next(1, 6),
                foodCostPerTurn = rng.Next(1, 6),
                waterCostPerTurn = rng.Next(1, 6)
            };
        }

        return result;
    }
    
    public void RefreshAllTownMarkets(int marketCycleId)
    {
        var rng = new System.Random(townSeed + marketCycleId * 999);

        for (int i = 0; i < towns.Count; i++)
        {
            GenerateMarketForTown(towns[i], rng, marketCycleId);
        }
    }
    
    public void RefreshAllTownMarketsExcept(int marketCycleId, int excludedTownIndex)
    {
        var rng = new System.Random(townSeed + marketCycleId * 999);

        for (int i = 0; i < towns.Count; i++)
        {
            if (i == excludedTownIndex)
                continue;

            GenerateMarketForTown(towns[i], rng, marketCycleId);
        }
    }
    
    private void GenerateMarketForTown(TownInstance town, System.Random rng, int cycleId)
    {
        if (town == null)
            return;

        var data = new TownMarketData();
        data.marketCycleId = cycleId;

        data.foodPrice = rng.Next(1, 4);
        data.waterPrice = rng.Next(1, 4);

        TradeGoodType[] allGoods = (TradeGoodType[])Enum.GetValues(typeof(TradeGoodType));
        var weightedPool = new List<TradeGoodType>();

        for (int i = 0; i < allGoods.Length; i++)
        {
            var g = allGoods[i];
            if (g == TradeGoodType.None) continue;

            bool isGrassland = IsGrasslandGood(g);
            bool isDesert = IsDesertGood(g);

            int weight = 1;

            if (town.biomeType == TownBiomeType.Grassland)
            {
                if (isGrassland) weight = 18;
                else if (isDesert) weight = 1;
            }
            else
            {
                if (isDesert) weight = 18;
                else if (isGrassland) weight = 1;
            }

            for (int w = 0; w < weight; w++)
                weightedPool.Add(g);
        }

        data.goods = new ShopItemData[4];

        var chosenGoods = new HashSet<TradeGoodType>();

        for (int i = 0; i < data.goods.Length; i++)
        {
            TradeGoodType chosen = TradeGoodType.None;

            int safety = 100;
            while (safety-- > 0)
            {
                var candidate = weightedPool[rng.Next(weightedPool.Count)];
                if (candidate == TradeGoodType.None) continue;
                if (chosenGoods.Contains(candidate)) continue;

                chosen = candidate;
                break;
            }

            if (chosen == TradeGoodType.None)
                break;

            chosenGoods.Add(chosen);

            int basePrice = GetBasePrice(chosen);
            float modifier = (float)(0.9 + rng.NextDouble() * 0.2);

            bool crossBiome =
                (town.biomeType == TownBiomeType.Grassland && IsDesertGood(chosen)) ||
                (town.biomeType == TownBiomeType.Desert && IsGrasslandGood(chosen));

            if (crossBiome)
                modifier *= 3f;

            int finalPrice = Mathf.Max(1, Mathf.RoundToInt(basePrice * modifier));

            data.goods[i] = new ShopItemData
            {
                type = chosen,
                price = finalPrice
            };
        }
        
        data.sellPrices = GenerateSellPricesForTown(town, data, rng);

        town.marketData = data;
    }
    
    private bool IsGrasslandGood(TradeGoodType g)
    {
        return g == TradeGoodType.Tea ||
               g == TradeGoodType.Paper ||
               g == TradeGoodType.Porcelain ||
               g == TradeGoodType.Jade ||
               g == TradeGoodType.Silk;
    }

    private bool IsDesertGood(TradeGoodType g)
    {
        return g == TradeGoodType.Dyes ||
               g == TradeGoodType.Incense ||
               g == TradeGoodType.Spices ||
               g == TradeGoodType.Glassware ||
               g == TradeGoodType.Carpets;
    }

    private int GetBasePrice(TradeGoodType g)
    {
        switch (g)
        {
            case TradeGoodType.Silk: return 10;
            case TradeGoodType.Tea: return 6;
            case TradeGoodType.Paper: return 5;
            case TradeGoodType.Porcelain: return 8;
            case TradeGoodType.Dyes: return 7;
            case TradeGoodType.Glassware: return 9;
            case TradeGoodType.Spices: return 8;
            case TradeGoodType.Incense: return 7;
            case TradeGoodType.Jade: return 12;
            case TradeGoodType.Carpets: return 9;
            default: return 5;
        }
    }
    
    private bool TownCurrentlySellsGood(TownMarketData data, TradeGoodType type)
    {
        if (data == null || data.goods == null)
            return false;

        for (int i = 0; i < data.goods.Length; i++)
        {
            if (data.goods[i] != null && data.goods[i].type == type)
                return true;
        }

        return false;
    }
    
    private SellPriceData[] GenerateSellPricesForTown(TownInstance town, TownMarketData data, System.Random rng)
    {
        List<SellPriceData> result = new List<SellPriceData>();

        TradeGoodType[] allGoods = (TradeGoodType[])Enum.GetValues(typeof(TradeGoodType));

        for (int i = 0; i < allGoods.Length; i++)
        {
            TradeGoodType type = allGoods[i];
            if (type == TradeGoodType.None)
                continue;

            int basePrice = GetBasePrice(type);

            bool isGrassland = IsGrasslandGood(type);
            bool isDesert = IsDesertGood(type);

            bool sameBiome =
                (town.biomeType == TownBiomeType.Grassland && isGrassland) ||
                (town.biomeType == TownBiomeType.Desert && isDesert);

            bool townSellsThis = TownCurrentlySellsGood(data, type);
            
            if (townSellsThis)
            {
                int stockedSellPrice = Mathf.Max(1, Mathf.RoundToInt(basePrice * 0.6f));

                result.Add(new SellPriceData
                {
                    type = type,
                    price = stockedSellPrice
                });

                continue;
            }

            float multiplier;

            if (sameBiome)
                multiplier = Mathf.Lerp(0.95f, 1.25f, (float)rng.NextDouble());
            else
                multiplier = Mathf.Lerp(1.6f, 2.4f, (float)rng.NextDouble());

            multiplier *= 1.2f;

            int sellPrice = Mathf.Max(1, Mathf.RoundToInt(basePrice * multiplier));

            result.Add(new SellPriceData
            {
                type = type,
                price = sellPrice
            });
        }

        return result.ToArray();
    }
    
    private int GetShopPriceForGood(TownMarketData data, TradeGoodType type)
    {
        if (data == null || data.goods == null)
            return -1;

        for (int i = 0; i < data.goods.Length; i++)
        {
            if (data.goods[i] != null && data.goods[i].type == type)
                return data.goods[i].price;
        }

        return -1;
    }
}