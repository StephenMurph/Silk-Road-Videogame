using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainRoadGeneratorComponent : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private TownManager townManager;

    [Header("Terrain Layers")]
    [SerializeField] private int roadLayerIndex = 3;
    [SerializeField] private int trailLayerIndex = 4;

    [Header("Road Width")]
    [SerializeField] private float grassRoadHalfWidthWorld = 7f;
    [SerializeField] private float desertTrailHalfWidthWorld = 5f;
    [SerializeField] private float paintStrength = 1f;
    [SerializeField, Range(0f, 1f)] private float desertCutoff = 0.35f;

    [Header("Pathfinding")]
    [SerializeField] private int gridSize = 256;
    [SerializeField] private float maxSlopeDegrees = 35f;
    [SerializeField] private float mountainAvoidance = 12f;
    [SerializeField, Range(0f, 1f)] private float mountainBlockCutoff = 0.55f;
    [SerializeField, Range(0f, 1f)] private float edgeBlockCutoff = 0.05f;

    [Header("Graph")]
    [SerializeField] private int seed = 1234;
    [SerializeField] private bool addExtraLinks = true;
    [SerializeField, Min(0)] private int extraLinksToAdd = 2;
    [SerializeField, Min(1)] private int maxLinksPerTown = 3;
    [SerializeField, Min(1f)] private float maxExtraLinkDistanceWorld = 260f;

    [Header("Debug")]
    [SerializeField] private bool logResult = true;

    public List<TerrainRoadGenerator.RoadEdgeNZ> edgesNZ = new();
    public Dictionary<int, List<int>> adjacency = new();

    private void Awake()
    {
        if (!terrain) terrain = FindFirstObjectByType<Terrain>();
        if (!terrainManager) terrainManager = FindFirstObjectByType<TerrainManager>();
        if (!townManager) townManager = FindFirstObjectByType<TownManager>();
    }

    [ContextMenu("Generate Roads")]
    public void GenerateRoads()
    {
        if (!terrain) terrain = FindFirstObjectByType<Terrain>();
        if (!terrainManager) terrainManager = FindFirstObjectByType<TerrainManager>();
        if (!townManager) townManager = FindFirstObjectByType<TownManager>();

        if (!terrain || !terrainManager || !townManager)
        {
            Debug.LogError("TerrainRoadGeneratorComponent: missing refs.");
            return;
        }

        if (townManager.towns == null || townManager.towns.Count < 2)
        {
            Debug.LogWarning("TerrainRoadGeneratorComponent: need at least 2 towns.");
            edgesNZ.Clear();
            adjacency.Clear();
            TerrainRoadGenerator.ClearStoredRoads();
            return;
        }

        var townPoints = new List<Vector2>(townManager.towns.Count);
        for (int i = 0; i < townManager.towns.Count; i++)
            townPoints.Add(townManager.towns[i].nz);

        edgesNZ = BuildTownGraph(
            terrain.terrainData,
            townPoints,
            seed,
            addExtraLinks,
            extraLinksToAdd,
            maxLinksPerTown,
            maxExtraLinkDistanceWorld
        );

        BuildAdjacencyFromEdges(townPoints, edgesNZ, adjacency);

        TerrainRoadGenerator.GenerateRoadNetwork(
            terrain: terrain,
            desertMask01: terrainManager.SendMessageDesertMask,
            mountainMask01: terrainManager.SendMessageMountainMask,
            edgeMask01: terrainManager.SendMessageEdgeMask,
            edgesNZ: edgesNZ,
            gridSize: gridSize,
            desertCutoff: desertCutoff,
            maxSlopeDegrees: maxSlopeDegrees,
            mountainAvoidance: mountainAvoidance,
            mountainBlockCutoff: mountainBlockCutoff,
            edgeBlockCutoff: edgeBlockCutoff,
            roadLayerIndex: roadLayerIndex,
            trailLayerIndex: trailLayerIndex,
            grassRoadHalfWidthWorld: grassRoadHalfWidthWorld,
            desertTrailHalfWidthWorld: desertTrailHalfWidthWorld,
            paintStrength: paintStrength
        );

        if (logResult)
        {
            Debug.Log(
                $"TerrainRoadGeneratorComponent: towns={townPoints.Count}, edges={edgesNZ.Count}, storedPaths={TerrainRoadGenerator.StoredPathCount}"
            );
        }
    }

    private static void BuildAdjacencyFromEdges(
        List<Vector2> townsNZ,
        List<TerrainRoadGenerator.RoadEdgeNZ> edges,
        Dictionary<int, List<int>> result)
    {
        result.Clear();

        for (int i = 0; i < townsNZ.Count; i++)
            result[i] = new List<int>();

        foreach (var edge in edges)
        {
            int a = FindTownIndex(townsNZ, edge.aNZ);
            int b = FindTownIndex(townsNZ, edge.bNZ);

            if (a < 0 || b < 0 || a == b)
                continue;

            if (!result[a].Contains(b))
                result[a].Add(b);

            if (!result[b].Contains(a))
                result[b].Add(a);
        }
    }

    private static int FindTownIndex(List<Vector2> townsNZ, Vector2 nz)
    {
        for (int i = 0; i < townsNZ.Count; i++)
        {
            if ((townsNZ[i] - nz).sqrMagnitude <= 0.0000001f)
                return i;
        }

        return -1;
    }

    private struct CandidateEdge
    {
        public int a;
        public int b;
        public float distWorld;

        public CandidateEdge(int a, int b, float distWorld)
        {
            this.a = a;
            this.b = b;
            this.distWorld = distWorld;
        }
    }

    private static List<TerrainRoadGenerator.RoadEdgeNZ> BuildTownGraph(
        TerrainData terrainData,
        List<Vector2> townsNZ,
        int seed,
        bool addExtraLinks,
        int extraLinksToAdd,
        int maxLinksPerTown,
        float maxExtraLinkDistanceWorld)
    {
        var result = new List<TerrainRoadGenerator.RoadEdgeNZ>();
        if (townsNZ == null || townsNZ.Count < 2)
            return result;

        var allCandidates = new List<CandidateEdge>();
        for (int i = 0; i < townsNZ.Count; i++)
        {
            for (int j = i + 1; j < townsNZ.Count; j++)
            {
                float d = WorldDistance(terrainData, townsNZ[i], townsNZ[j]);
                allCandidates.Add(new CandidateEdge(i, j, d));
            }
        }

        allCandidates.Sort((x, y) => x.distWorld.CompareTo(y.distWorld));

        var parent = new int[townsNZ.Count];
        for (int i = 0; i < parent.Length; i++)
            parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        void Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra != rb)
                parent[rb] = ra;
        }

        var degree = new int[townsNZ.Count];
        var used = new HashSet<ulong>();

        ulong EdgeKey(int a, int b)
        {
            uint lo = (uint)Mathf.Min(a, b);
            uint hi = (uint)Mathf.Max(a, b);
            return ((ulong)lo << 32) | hi;
        }

        void AddEdgeByIndex(int a, int b)
        {
            ulong key = EdgeKey(a, b);
            if (!used.Add(key))
                return;

            result.Add(new TerrainRoadGenerator.RoadEdgeNZ(townsNZ[a], townsNZ[b]));
            degree[a]++;
            degree[b]++;
        }

        // 1) MST so every town is connected
        int added = 0;
        for (int i = 0; i < allCandidates.Count && added < townsNZ.Count - 1; i++)
        {
            var c = allCandidates[i];
            if (Find(c.a) == Find(c.b))
                continue;

            AddEdgeByIndex(c.a, c.b);
            Union(c.a, c.b);
            added++;
        }

        // 2) each town gets at least one short local link if possible
        for (int i = 0; i < townsNZ.Count; i++)
        {
            if (degree[i] > 0)
                continue;

            for (int j = 0; j < allCandidates.Count; j++)
            {
                var c = allCandidates[j];
                if (c.a != i && c.b != i)
                    continue;

                int other = c.a == i ? c.b : c.a;
                if (degree[i] >= maxLinksPerTown || degree[other] >= maxLinksPerTown)
                    continue;

                AddEdgeByIndex(i, other);
                break;
            }
        }

        // 3) optional extra short links
        if (addExtraLinks && extraLinksToAdd > 0)
        {
            var rng = new System.Random(seed);
            var extraPool = new List<CandidateEdge>();

            for (int i = 0; i < allCandidates.Count; i++)
            {
                var c = allCandidates[i];
                if (c.distWorld > maxExtraLinkDistanceWorld)
                    continue;

                if (used.Contains(EdgeKey(c.a, c.b)))
                    continue;

                extraPool.Add(c);
            }

            // shuffle a little among short edges so every map is not too identical
            for (int i = 0; i < extraPool.Count; i++)
            {
                int swap = rng.Next(i, extraPool.Count);
                (extraPool[i], extraPool[swap]) = (extraPool[swap], extraPool[i]);
            }

            // then sort mostly by shortness
            extraPool.Sort((x, y) => x.distWorld.CompareTo(y.distWorld));

            int extrasAdded = 0;
            for (int i = 0; i < extraPool.Count && extrasAdded < extraLinksToAdd; i++)
            {
                var c = extraPool[i];

                if (degree[c.a] >= maxLinksPerTown || degree[c.b] >= maxLinksPerTown)
                    continue;

                AddEdgeByIndex(c.a, c.b);
                extrasAdded++;
            }
        }

        return result;
    }

    private static float WorldDistance(TerrainData data, Vector2 aNZ, Vector2 bNZ)
    {
        Vector2 aW = new Vector2(aNZ.x * data.size.x, aNZ.y * data.size.z);
        Vector2 bW = new Vector2(bNZ.x * data.size.x, bNZ.y * data.size.z);
        return Vector2.Distance(aW, bW);
    }
}

public static class TerrainRoadGenerator
{
    public readonly struct RoadEdgeNZ
    {
        public readonly Vector2 aNZ;
        public readonly Vector2 bNZ;

        public RoadEdgeNZ(Vector2 aNZ, Vector2 bNZ)
        {
            this.aNZ = aNZ;
            this.bNZ = bNZ;
        }
    }

    private struct Node
    {
        public int x;
        public int z;
        public float g;
        public float f;
        public int parent;
    }

    public static Texture2D roadMask01;
    private static int roadMaskW;
    private static int roadMaskH;

    public static readonly Dictionary<string, List<Vector2>> roadCenterlineNZ = new();

    public static int StoredPathCount => roadCenterlineNZ.Count;

    public static void ClearStoredRoads()
    {
        roadCenterlineNZ.Clear();
        ClearRoadMask();
    }

    public static bool TryGetRoadPathNZ(Vector2 aNZ, Vector2 bNZ, out List<Vector2> pathNZ)
    {
        return roadCenterlineNZ.TryGetValue(Key(aNZ, bNZ), out pathNZ) && pathNZ != null && pathNZ.Count > 1;
    }

    public static float SampleRoadMask01(TerrainData data, float nx, float nz)
    {
        if (roadMask01 == null)
            return 0f;

        return roadMask01.GetPixelBilinear(Mathf.Clamp01(nx), Mathf.Clamp01(nz)).r;
    }

    public static void GenerateRoadNetwork(
        Terrain terrain,
        Func<float, float, float> desertMask01,
        Func<float, float, float> mountainMask01,
        Func<float, float, float> edgeMask01,
        List<RoadEdgeNZ> edgesNZ,
        int gridSize,
        float desertCutoff,
        float maxSlopeDegrees,
        float mountainAvoidance,
        float mountainBlockCutoff,
        float edgeBlockCutoff,
        int roadLayerIndex,
        int trailLayerIndex,
        float grassRoadHalfWidthWorld,
        float desertTrailHalfWidthWorld,
        float paintStrength)
    {
        if (!terrain)
        {
            Debug.LogError("TerrainRoadGenerator: missing terrain.");
            return;
        }

        if (edgesNZ == null || edgesNZ.Count == 0)
        {
            Debug.LogWarning("TerrainRoadGenerator: no edges to generate.");
            ClearStoredRoads();
            return;
        }

        var data = terrain.terrainData;

        EnsureRoadMask(data);
        ClearRoadMask();
        roadCenterlineNZ.Clear();

        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;
        int layers = data.alphamapLayers;

        var maps = data.GetAlphamaps(0, 0, aw, ah);

        // wipe only road/trail layers
        for (int z = 0; z < ah; z++)
        {
            for (int x = 0; x < aw; x++)
            {
                maps[z, x, roadLayerIndex] = 0f;
                maps[z, x, trailLayerIndex] = 0f;
            }
        }

        int ok = 0;

        for (int i = 0; i < edgesNZ.Count; i++)
        {
            var edge = edgesNZ[i];

            var path = FindPathAStar(
                data,
                desertMask01,
                mountainMask01,
                edgeMask01,
                edge.aNZ,
                edge.bNZ,
                gridSize,
                desertCutoff,
                maxSlopeDegrees,
                mountainAvoidance,
                mountainBlockCutoff,
                edgeBlockCutoff
            );

            if (path == null || path.Count < 2)
            {
                // hard fallback: direct segment so towns are never left disconnected
                path = new List<Vector2> { edge.aNZ, edge.bNZ };
            }

            StoreRoadPath(edge.aNZ, edge.bNZ, path);

            PaintPathIntoMaps(
                terrain,
                maps,
                desertMask01,
                edgeMask01,
                path,
                roadLayerIndex,
                trailLayerIndex,
                grassRoadHalfWidthWorld,
                desertTrailHalfWidthWorld,
                desertCutoff,
                edgeBlockCutoff,
                paintStrength
            );

            ok++;
        }

        if (roadMask01 != null)
            roadMask01.Apply(false, false);

        NormalizeAlphamaps(maps, layers);
        data.SetAlphamaps(0, 0, maps);
        terrain.Flush();

        Debug.Log($"TerrainRoadGenerator: painted {ok}/{edgesNZ.Count} roads.");
    }

    private static string Key(Vector2 a, Vector2 b)
    {
        float ax = Mathf.Round(a.x * 100000f) / 100000f;
        float ay = Mathf.Round(a.y * 100000f) / 100000f;
        float bx = Mathf.Round(b.x * 100000f) / 100000f;
        float by = Mathf.Round(b.y * 100000f) / 100000f;
        return $"{ax},{ay}->{bx},{by}";
    }

    private static void StoreRoadPath(Vector2 aNZ, Vector2 bNZ, List<Vector2> path)
    {
        if (path == null || path.Count < 2)
            return;

        roadCenterlineNZ[Key(aNZ, bNZ)] = new List<Vector2>(path);

        var rev = new List<Vector2>(path);
        rev.Reverse();
        roadCenterlineNZ[Key(bNZ, aNZ)] = rev;
    }

    private static void EnsureRoadMask(TerrainData data)
    {
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;

        if (roadMask01 != null && roadMaskW == w && roadMaskH == h)
            return;

        roadMaskW = w;
        roadMaskH = h;

        roadMask01 = new Texture2D(w, h, TextureFormat.R8, false, true);
        roadMask01.wrapMode = TextureWrapMode.Clamp;
        roadMask01.filterMode = FilterMode.Bilinear;
    }

    public static void ClearRoadMask()
    {
        if (roadMask01 == null)
            return;

        var cols = new Color32[roadMask01.width * roadMask01.height];
        roadMask01.SetPixels32(cols);
        roadMask01.Apply(false, false);
    }

    private static List<Vector2> FindPathAStar(
        TerrainData data,
        Func<float, float, float> desertMask01,
        Func<float, float, float> mountainMask01,
        Func<float, float, float> edgeMask01,
        Vector2 startNZ,
        Vector2 goalNZ,
        int gridSize,
        float desertCutoff,
        float maxSlopeDegrees,
        float mountainAvoidance,
        float mountainBlockCutoff,
        float edgeBlockCutoff)
    {
        int n = Mathf.Max(32, gridSize);
        int count = n * n;

        int Pack(int x, int z) => z * n + x;
        void Unpack(int idx, out int x, out int z)
        {
            z = idx / n;
            x = idx - z * n;
        }

        int sx = Mathf.Clamp(Mathf.RoundToInt(startNZ.x * (n - 1)), 0, n - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt(startNZ.y * (n - 1)), 0, n - 1);
        int gx = Mathf.Clamp(Mathf.RoundToInt(goalNZ.x * (n - 1)), 0, n - 1);
        int gz = Mathf.Clamp(Mathf.RoundToInt(goalNZ.y * (n - 1)), 0, n - 1);

        int start = Pack(sx, sz);
        int goal = Pack(gx, gz);

        var open = new SimpleMinHeap(count);
        var closed = new bool[count];
        var nodes = new Node[count];

        for (int i = 0; i < count; i++)
        {
            nodes[i].g = float.PositiveInfinity;
            nodes[i].f = float.PositiveInfinity;
            nodes[i].parent = -1;
        }

        float Heuristic(int x, int z)
        {
            return Vector2.Distance(new Vector2(x, z), new Vector2(gx, gz));
        }

        nodes[start].x = sx;
        nodes[start].z = sz;
        nodes[start].g = 0f;
        nodes[start].f = Heuristic(sx, sz);
        open.Push(start, nodes[start].f);

        int[] dx8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dz8 = { -1, -1, -1, 0, 0, 1, 1, 1 };

        int safety = count * 20;

        for (int iter = 0; iter < safety && open.Count > 0; iter++)
        {
            int current = open.PopMin(out _);
            if (closed[current])
                continue;

            closed[current] = true;

            if (current == goal)
                return ReconstructPath(nodes, start, goal, n);

            Unpack(current, out int cx, out int cz);

            for (int k = 0; k < 8; k++)
            {
                int nx = cx + dx8[k];
                int nz = cz + dz8[k];

                if ((uint)nx >= (uint)n || (uint)nz >= (uint)n)
                    continue;

                int ni = Pack(nx, nz);
                if (closed[ni])
                    continue;

                float nnx = nx / (float)(n - 1);
                float nnz = nz / (float)(n - 1);

                float edge = edgeMask01 != null ? Mathf.Clamp01(edgeMask01(nnx, nnz)) : 0f;
                float mountain = mountainMask01 != null ? Mathf.Clamp01(mountainMask01(nnx, nnz)) : 0f;
                float desert = desertMask01 != null ? Mathf.Clamp01(desertMask01(nnx, nnz)) : 0f;
                float slope = data.GetSteepness(nnx, nnz);

                if (edge >= edgeBlockCutoff)
                    continue;

                if (mountain >= mountainBlockCutoff)
                    continue;

                if (slope > maxSlopeDegrees + 12f)
                    continue;

                float step = (dx8[k] != 0 && dz8[k] != 0) ? 1.4142f : 1f;

                float slopePenalty = Mathf.InverseLerp(0f, maxSlopeDegrees, Mathf.Min(slope, maxSlopeDegrees)) * 2.5f;
                float mountainPenalty = mountain * mountainAvoidance;
                float edgePenalty = edge * 8f;
                float desertPenalty = desert > desertCutoff ? 0.25f : 0f;

                float moveCost = step * (1f + slopePenalty + mountainPenalty + edgePenalty + desertPenalty);
                float tentativeG = nodes[current].g + moveCost;

                if (tentativeG < nodes[ni].g)
                {
                    nodes[ni].x = nx;
                    nodes[ni].z = nz;
                    nodes[ni].g = tentativeG;
                    nodes[ni].f = tentativeG + Heuristic(nx, nz);
                    nodes[ni].parent = current;
                    open.Push(ni, nodes[ni].f);
                }
            }
        }

        return null;
    }

    private static List<Vector2> ReconstructPath(Node[] nodes, int start, int goal, int n)
    {
        var rev = new List<Vector2>();
        int cur = goal;
        int safety = nodes.Length + 10;

        while (cur != -1 && safety-- > 0)
        {
            int x = nodes[cur].x;
            int z = nodes[cur].z;

            rev.Add(new Vector2(
                x / (float)(n - 1),
                z / (float)(n - 1)
            ));

            if (cur == start)
                break;

            cur = nodes[cur].parent;
        }

        rev.Reverse();
        return rev;
    }

    private static void PaintPathIntoMaps(
        Terrain terrain,
        float[,,] maps,
        Func<float, float, float> desertMask01,
        Func<float, float, float> edgeMask01,
        List<Vector2> pathNZ,
        int roadLayerIndex,
        int trailLayerIndex,
        float grassHalfWidthWorld,
        float desertHalfWidthWorld,
        float desertCutoff,
        float edgeBlockCutoff,
        float paintStrength)
    {
        var data = terrain.terrainData;
        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;
        int layers = data.alphamapLayers;

        float worldPerAlphaX = data.size.x / (aw - 1);
        float worldPerAlphaZ = data.size.z / (ah - 1);

        const float stepMeters = 1.0f;

        for (int i = 0; i < pathNZ.Count - 1; i++)
        {
            Vector2 aNZ = pathNZ[i];
            Vector2 bNZ = pathNZ[i + 1];

            Vector2 aW = new Vector2(aNZ.x * data.size.x, aNZ.y * data.size.z);
            Vector2 bW = new Vector2(bNZ.x * data.size.x, bNZ.y * data.size.z);

            float segLen = Vector2.Distance(aW, bW);
            int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / stepMeters));

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector2 pW = Vector2.Lerp(aW, bW, t);

                float nx = pW.x / data.size.x;
                float nz = pW.y / data.size.z;

                StampAtNormalized(
                    maps,
                    aw,
                    ah,
                    layers,
                    worldPerAlphaX,
                    worldPerAlphaZ,
                    desertMask01,
                    edgeMask01,
                    nx,
                    nz,
                    edgeBlockCutoff,
                    roadLayerIndex,
                    trailLayerIndex,
                    grassHalfWidthWorld,
                    desertHalfWidthWorld,
                    desertCutoff,
                    paintStrength
                );
            }
        }
    }

    private static void StampAtNormalized(
        float[,,] maps,
        int aw,
        int ah,
        int layers,
        float worldPerAlphaX,
        float worldPerAlphaZ,
        Func<float, float, float> desertMask01,
        Func<float, float, float> edgeMask01,
        float nx,
        float nz,
        float edgeBlockCutoff,
        int roadLayerIndex,
        int trailLayerIndex,
        float grassHalfWidthWorld,
        float desertHalfWidthWorld,
        float desertCutoff,
        float paintStrength)
    {
        float edge = edgeMask01 != null ? Mathf.Clamp01(edgeMask01(nx, nz)) : 0f;
        if (edge >= edgeBlockCutoff)
            return;

        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        float desert = desertMask01 != null ? Mathf.Clamp01(desertMask01(nx, nz)) : 0f;
        bool isDesert = desert >= desertCutoff;

        float halfWidthWorld = isDesert ? desertHalfWidthWorld : grassHalfWidthWorld;
        int targetLayer = isDesert ? trailLayerIndex : roadLayerIndex;

        float ax = nx * (aw - 1);
        float az = nz * (ah - 1);

        float radiusPxX = halfWidthWorld / Mathf.Max(0.0001f, worldPerAlphaX);
        float radiusPxZ = halfWidthWorld / Mathf.Max(0.0001f, worldPerAlphaZ);
        float radiusPx = Mathf.Max(radiusPxX, radiusPxZ);

        int xmin = Mathf.Clamp(Mathf.FloorToInt(ax - radiusPx), 0, aw - 1);
        int xmax = Mathf.Clamp(Mathf.CeilToInt(ax + radiusPx), 0, aw - 1);
        int zmin = Mathf.Clamp(Mathf.FloorToInt(az - radiusPx), 0, ah - 1);
        int zmax = Mathf.Clamp(Mathf.CeilToInt(az + radiusPx), 0, ah - 1);

        for (int z = zmin; z <= zmax; z++)
        {
            for (int x = xmin; x <= xmax; x++)
            {
                float dxW = (x - ax) * worldPerAlphaX;
                float dzW = (z - az) * worldPerAlphaZ;
                float dist = Mathf.Sqrt(dxW * dxW + dzW * dzW);

                if (dist > halfWidthWorld)
                    continue;

                if (dist <= halfWidthWorld)
                {
                    // wipe all layers
                    for (int l = 0; l < layers; l++)
                        maps[z, x, l] = 0f;

                    // set ONLY road layer
                    maps[z, x, targetLayer] = 1f;

                    if (roadMask01 != null)
                        roadMask01.SetPixel(x, z, new Color(1f, 0f, 0f, 1f));
                }
            }
        }
    }

    private static void NormalizeAlphamaps(float[,,] maps, int layers)
    {
        int h = maps.GetLength(0);
        int w = maps.GetLength(1);

        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int l = 0; l < layers; l++)
                    sum += maps[z, x, l];

                if (sum <= 0.0001f)
                {
                    maps[z, x, 0] = 1f;
                    for (int l = 1; l < layers; l++)
                        maps[z, x, l] = 0f;
                    continue;
                }

                for (int l = 0; l < layers; l++)
                    maps[z, x, l] /= sum;
            }
        }
    }

    private class SimpleMinHeap
    {
        private int[] ids;
        private float[] pri;

        public int Count { get; private set; }

        public SimpleMinHeap(int cap)
        {
            ids = new int[Mathf.Max(16, cap)];
            pri = new float[ids.Length];
            Count = 0;
        }

        public void Push(int id, float p)
        {
            if (Count >= ids.Length)
            {
                Array.Resize(ref ids, ids.Length * 2);
                Array.Resize(ref pri, pri.Length * 2);
            }

            int i = Count++;
            ids[i] = id;
            pri[i] = p;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (pri[parent] <= pri[i])
                    break;

                Swap(i, parent);
                i = parent;
            }
        }

        public int PopMin(out float p)
        {
            int root = ids[0];
            p = pri[0];

            Count--;
            ids[0] = ids[Count];
            pri[0] = pri[Count];

            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                if (l >= Count)
                    break;

                int r = l + 1;
                int best = (r < Count && pri[r] < pri[l]) ? r : l;

                if (pri[i] <= pri[best])
                    break;

                Swap(i, best);
                i = best;
            }

            return root;
        }

        private void Swap(int a, int b)
        {
            (ids[a], ids[b]) = (ids[b], ids[a]);
            (pri[a], pri[b]) = (pri[b], pri[a]);
        }
    }
}