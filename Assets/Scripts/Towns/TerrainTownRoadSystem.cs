using System;
using System.Collections.Generic;
using UnityEngine;


public class TerrainTownRoadSystem : MonoBehaviour
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
    [Range(0f, 1f)] public float mountainBlockCutoff = 0.35f;

    [Tooltip("Avoid lake area (meters).")]
    public float lakeAvoidBufferWorld = 10f;

    [Header("Road Network")]
    [Tooltip("Max distance in world meters to connect towns.")]
    public float connectMaxDistanceWorld = 420f;

    [Tooltip("Max number of road connections per town.")]
    public int maxConnectionsPerTown = 2;

    [Tooltip("Chance to add one extra branch connection (if a valid neighbor exists).")]
    [Range(0f, 1f)] public float extraBranchChance = 0.25f;
    
    public List<TerrainRoadGenerator.RoadEdgeNZ> edgesNZ = new();
    public Dictionary<int, List<int>> adjacency = new();

    [Header("Road Paint Settings (match your TerrainRoadGenerator layers)")]
    public int roadLayerIndex = 3;   
    public int trailLayerIndex = 4;  

    public float grassRoadHalfWidthWorld = 7f;
    public float desertTrailHalfWidthWorld = 3.5f;
    [Range(0f, 1f)] public float desertCutoff = 0.35f;
    [Range(0f, 1f)] public float paintStrength = 0.85f;

    [Header("A* Settings")]
    public int gridSize = 256;
    public float maxSlopeDegrees = 35f;
    public float mountainAvoidance = 20f;
    [Range(0f, 1f)] public float mountainRoadBlockCutoff = 0.5f;

    [Header("Debug")]
    public bool clearExistingTowns = true;
    public string townRootName = "TownsRoot";
    
    [Header("Main Road Settings")]
    [Tooltip("How strongly branch roads prefer snapping onto the main road (0 = off).")]
    [Range(0f, 1f)] public float branchRoadAttraction = 0.85f;

    [Tooltip("Chance to add extra cross-links (minor roads) after the main road exists.")]
    [Range(0f, 1f)] public float crossLinkChance = 0.15f;

    [Tooltip("How many cross-links per town attempt.")]
    [Range(0, 3)] public int crossLinksPerTown = 1;

    [Serializable]
    public class TownInstance
    {
        public Vector2 nz;
        public GameObject go;
    }

    public readonly List<TownInstance> towns = new();

   public void GenerateTownsAndRoads()
{
    if (!terrainManager) terrainManager = GetComponent<TerrainManager>();
    if (!terrain) terrain = terrainManager ? terrainManager.GetComponent<Terrain>() : null;
    if (!terrainManager || !terrain || !housePrefab)
    {
        Debug.LogError("TerrainTownRoadSystem: missing refs (terrainManager/terrain/housePrefab).");
        return;
    }

    var data = terrain.terrainData;

    SpawnTowns(data);

    // --- Build a MAIN + BRANCH style edge list ---
    var townsNZ = new List<Vector2>(towns.Count);
    for (int i = 0; i < towns.Count; i++)
        townsNZ.Add(towns[i].nz);

    // This returns: [main chain edges first] + [optional cross-links]
    var allEdges = TerrainRoadGenerator.BuildMainAndBranchEdges(
        townsNZ: townsNZ,
        seed: townSeed,
        extraConnectionChance: crossLinkChance,
        extraConnectionsPerTown: crossLinksPerTown
    );

    if (townsNZ.Count < 2 || allEdges.Count == 0)
    {
        Debug.LogWarning("TownRoadSystem: not enough towns/edges to generate roads.");
        return;
    }

    int mainCount = Mathf.Max(0, townsNZ.Count - 1);
    mainCount = Mathf.Min(mainCount, allEdges.Count);

    var mainEdges = allEdges.GetRange(0, mainCount);
    var branchEdges = allEdges.GetRange(mainCount, allEdges.Count - mainCount);
    
    float edgeBlockCutoff = 0.05f;

    // --- Masks ---
    Func<float, float, float> desertMask = (nx, nz) => terrainManager.SendMessageDesertMask(nx, nz);
    Func<float, float, float> mountainMask = (nx, nz) => terrainManager.SendMessageMountainMask(nx, nz);
    Func<float, float, float> edgeMask = (nx, nz) => terrainManager.SendMessageEdgeMask(nx, nz);


    // --- PASS 1: paint main road backbone (clears road mask) ---
    TerrainRoadGenerator.GenerateRoadNetwork(
        terrain: terrain,
        desertMask01: desertMask,
        mountainMask01: mountainMask,
        edgeMask01: edgeMask,
        seed: townSeed ^ 0x51A71,
        edgesNZ: mainEdges,
        gridSize: gridSize,
        desertCutoff: desertCutoff,
        maxSlopeDegrees: maxSlopeDegrees,
        roadLayerIndex: roadLayerIndex,
        trailLayerIndex: trailLayerIndex,
        grassRoadHalfWidthWorld: grassRoadHalfWidthWorld,
        desertTrailHalfWidthWorld: desertTrailHalfWidthWorld,
        paintStrength: paintStrength,
        mountainAvoidance: mountainAvoidance,
        mountainBlockCutoff: mountainRoadBlockCutoff,
        edgeBlockCutoff: edgeBlockCutoff,
        clearRoadMaskFirst: true,
        roadAttraction: 0f
    );

    // --- PASS 2: paint branches that prefer existing main road (do NOT clear) ---
    if (branchEdges.Count > 0 && branchRoadAttraction > 0f)
    {
        TerrainRoadGenerator.GenerateRoadNetwork(
            terrain: terrain,
            desertMask01: desertMask,
            mountainMask01: mountainMask,
            edgeMask01: edgeMask,
            seed: townSeed ^ 0x51A71,
            edgesNZ: mainEdges,
            gridSize: gridSize,
            desertCutoff: desertCutoff,
            maxSlopeDegrees: maxSlopeDegrees,
            roadLayerIndex: roadLayerIndex,
            trailLayerIndex: trailLayerIndex,
            grassRoadHalfWidthWorld: grassRoadHalfWidthWorld,
            desertTrailHalfWidthWorld: desertTrailHalfWidthWorld,
            paintStrength: paintStrength,
            mountainAvoidance: mountainAvoidance,
            mountainBlockCutoff: mountainRoadBlockCutoff,
            edgeBlockCutoff: edgeBlockCutoff,
            clearRoadMaskFirst: true,
            roadAttraction: 0f
        );
    }

    // Update edgesNZ + adjacency to match the new network
    edgesNZ = allEdges;
    BuildAdjacencyFromEdges();

    Debug.Log($"TownRoadSystem: towns={towns.Count}, mainEdges={mainEdges.Count}, branchEdges={branchEdges.Count}, totalEdges={edgesNZ.Count}");
}

    void SpawnTowns(TerrainData data)
    {
        towns.Clear();

        // Clear old
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
        
        Dictionary<int, List<Vector2>> buckets = null;
        float cellSize = townMinSpacingWorld;
        if (townMinSpacingWorld > 0f) buckets = new Dictionary<int, List<Vector2>>(townCount);

        static int Hash(int x, int z) { unchecked { return x * 73856093 ^ z * 19349663; } }

        int safety = Mathf.Max(2000, townCount * 200);

        for (int tries = 0; tries < safety && towns.Count < townCount; tries++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();
            
            float m = terrainManager.SendMessageMountainMask(nx, nz);
            if (m >= mountainBlockCutoff) continue;
            
            if (terrainManager.LakeMask01(nx, nz, lakeAvoidBufferWorld) > 0f) continue;

            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            if (h01 <= terrainManager.seaLevel01 + seaBuffer01) continue;

            float slope = data.GetSteepness(nx, nz);
            if (slope > townMaxSlope) continue;
            
            if (buckets != null)
            {
                Vector2 pW = new Vector2(nx * data.size.x, nz * data.size.z);
                int cx = Mathf.FloorToInt(pW.x / cellSize);
                int cz = Mathf.FloorToInt(pW.y / cellSize);

                float minSqr = townMinSpacingWorld * townMinSpacingWorld;
                bool tooClose = false;

                for (int dz = -1; dz <= 1 && !tooClose; dz++)
                for (int dx = -1; dx <= 1 && !tooClose; dx++)
                {
                    int key = Hash(cx + dx, cz + dz);
                    if (!buckets.TryGetValue(key, out var list)) continue;
                    for (int i = 0; i < list.Count; i++)
                        if ((list[i] - pW).sqrMagnitude < minSqr) { tooClose = true; break; }
                }
                if (tooClose) continue;

                int myKey = Hash(cx, cz);
                if (!buckets.TryGetValue(myKey, out var mine))
                    buckets[myKey] = mine = new List<Vector2>(4);
                mine.Add(pW);
            }
            
            Vector3 world = new Vector3(nx * data.size.x, h01 * data.size.y, nz * data.size.z) + terrain.transform.position;
            var goTown = Instantiate(housePrefab, world, Quaternion.identity, root);
            
            goTown.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            towns.Add(new TownInstance { nz = new Vector2(nx, nz), go = goTown });
        }

        if (towns.Count < townCount)
            Debug.LogWarning($"TownRoadSystem: only spawned {towns.Count}/{townCount} towns (increase safety / relax constraints).");
    }

    List<TerrainRoadGenerator.RoadEdgeNZ> BuildLocalEdges(TerrainData data)
    {
        var edges = new List<TerrainRoadGenerator.RoadEdgeNZ>();
        if (towns.Count < 2) return edges;

        float maxD = Mathf.Max(1f, connectMaxDistanceWorld);
        
        for (int i = 0; i < towns.Count; i++)
        {
            var a = towns[i];
            
            var neigh = new List<(int idx, float dist)>();
            for (int j = 0; j < towns.Count; j++)
            {
                if (j == i) continue;
                float d = WorldDistance(data, a.nz, towns[j].nz);
                if (d <= maxD) neigh.Add((j, d));
            }

            neigh.Sort((p, q) => p.dist.CompareTo(q.dist));

            int targetConnections = Mathf.Clamp(maxConnectionsPerTown, 0, 32);
            if (targetConnections == 0) continue;
            
            var rng = new System.Random((townSeed * 397) ^ i);
            if (rng.NextDouble() < extraBranchChance) targetConnections += 1;

            int made = 0;
            for (int k = 0; k < neigh.Count && made < targetConnections; k++)
            {
                int j = neigh[k].idx;
                if (i < j)
                {
                    edges.Add(new TerrainRoadGenerator.RoadEdgeNZ(a.nz, towns[j].nz));
                    made++;
                }
            }
        }

        return edges;
    }
    
    void BuildAdjacencyFromEdges()
    {
        adjacency.Clear();
        for (int i = 0; i < towns.Count; i++)
            adjacency[i] = new List<int>(4);

        foreach (var e in edgesNZ)
        {
            int a = FindTownIndex(e.aNZ);
            int b = FindTownIndex(e.bNZ);
            if (a < 0 || b < 0 || a == b) continue;

            if (!adjacency[a].Contains(b)) adjacency[a].Add(b);
            if (!adjacency[b].Contains(a)) adjacency[b].Add(a);
        }
    }
    
    int FindTownIndex(Vector2 nz)
    {
        for (int i = 0; i < towns.Count; i++)
            if (towns[i].nz == nz) return i;
        return -1;
    }

    float WorldDistance(TerrainData data, Vector2 aNZ, Vector2 bNZ)
    {
        Vector2 aW = new Vector2(aNZ.x * data.size.x, aNZ.y * data.size.z);
        Vector2 bW = new Vector2(bNZ.x * data.size.x, bNZ.y * data.size.z);
        return Vector2.Distance(aW, bW);
    }
}