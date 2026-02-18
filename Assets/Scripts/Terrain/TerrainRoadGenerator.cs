using System;
using System.Collections.Generic;
using UnityEngine;

public static class TerrainRoadGenerator
{
    public static Texture2D roadMask01; // R channel stores 0..1 road influence
    private static int roadMaskW, roadMaskH;
    public static void GenerateRandomRoadBetweenTwoPoints(
        Terrain terrain,
        Func<float, float, float> desertMask01,   
        Func<float, float, float> mountainMask01,
        int seed,
        int gridSize = 256,                      
        float desertCutoff = 0.35f,              
        float maxSlopeDegrees = 35f,
        int roadLayerIndex = 2,                   
        int trailLayerIndex = 3,                  
        float grassRoadHalfWidthWorld = 3.5f,     
        float desertTrailHalfWidthWorld = 1.2f,   
        float paintStrength = 0.85f, 
        float mountainAvoidance = 8.0f,           
        float mountainBlockCutoff = 0.55f 
    )
    {
        if (!terrain) { Debug.LogError("RoadGen: missing terrain"); return; }
        var data = terrain.terrainData;
        if (data.terrainLayers == null || data.terrainLayers.Length <= Mathf.Max(roadLayerIndex, trailLayerIndex))
        {
            Debug.LogError("RoadGen: TerrainLayers missing road/trail layers. Add layers first.");
            return;
        }

        var rng = new System.Random(seed);

  
        Vector2 a = PickPoint(rng, data, desertMask01, mountainMask01, maxSlopeDegrees);
        Vector2 b = PickPoint(rng, data, desertMask01, mountainMask01, maxSlopeDegrees);


        var path = FindPathAStar(data, desertMask01, mountainMask01, a, b, gridSize, desertCutoff, maxSlopeDegrees, mountainAvoidance, mountainBlockCutoff);
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("RoadGen: no path found.");
            return;
        }
        
        EnsureRoadMask(data);
        ClearRoadMask();

        PaintPathLayers(terrain, desertMask01, path,
            roadLayerIndex, trailLayerIndex,
            grassRoadHalfWidthWorld, desertTrailHalfWidthWorld,
            desertCutoff, paintStrength);

        terrain.Flush();

        Debug.Log($"RoadGen: road created between A {a} and B {b} with {path.Count} nodes.");
    }

    private static Vector2 PickPoint(
        System.Random rng,
        TerrainData data,
        Func<float,float,float> desertMask01,
        Func<float,float,float> mountainMask01,
        float maxSlope
    )
    {

        for (int i = 0; i < 500; i++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            float slope = data.GetSteepness(nx, nz);
            if (slope > maxSlope) continue;
            
            float mountain = mountainMask01 != null ? Mathf.Clamp01(mountainMask01(nx, nz)) : 0f;
            if (mountain > 0.25f) continue; 
            

            return new Vector2(nx, nz);
        }

        // fallback
        return new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
    }

    // -------------------- A* --------------------

    private struct Node
    {
        public int x, z;
        public float g, f;
        public int parent; 
    }

    private static List<Vector2> FindPathAStar(
        TerrainData data,
        Func<float, float, float> desertMask01,
        Func<float, float, float> mountainMask01,
        Vector2 startNZ,
        Vector2 goalNZ,
        int gridSize,
        float desertCutoff,
        float maxSlopeDegrees,
        float mountainAvoidance,
        float mountainBlockCutoff
    )
    {
        int N = Mathf.Max(16, gridSize);
        int Count = N * N;

        int Pack(int x, int z) => z * N + x;
        void Unpack(int idx, out int x, out int z) { z = idx / N; x = idx - z * N; }

        int sx = Mathf.Clamp(Mathf.RoundToInt(startNZ.x * (N - 1)), 0, N - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt(startNZ.y * (N - 1)), 0, N - 1);
        int gx = Mathf.Clamp(Mathf.RoundToInt(goalNZ.x * (N - 1)), 0, N - 1);
        int gz = Mathf.Clamp(Mathf.RoundToInt(goalNZ.y * (N - 1)), 0, N - 1);

        int start = Pack(sx, sz);
        int goal = Pack(gx, gz);
        
        var open = new SimpleMinHeap(Count);
        var inOpen = new bool[Count];
        var closed = new bool[Count];

        var nodes = new Node[Count];
        for (int i = 0; i < Count; i++)
        {
            nodes[i].parent = -1;
            nodes[i].g = float.PositiveInfinity;
            nodes[i].f = float.PositiveInfinity;
        }

        float Heuristic(int x, int z) => Mathf.Abs(x - gx) + Mathf.Abs(z - gz);

        nodes[start].x = sx; nodes[start].z = sz;
        nodes[start].g = 0f;
        nodes[start].f = Heuristic(sx, sz);
        open.Push(start, nodes[start].f);
        inOpen[start] = true;
        
        int[] dx8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dz8 = { -1, -1, -1, 0, 0, 1, 1, 1 };

        int safety = Count * 20;

        for (int iter = 0; iter < safety && open.Count > 0; iter++)
        {
            int current = open.PopMin(out _);
            inOpen[current] = false;
            if (closed[current]) continue;
            closed[current] = true;

            if (current == goal)
                return ReconstructPath(nodes, start, goal, N);

            Unpack(current, out int cx, out int cz);
            
            float cnx = cx / (float)(N - 1);
            float cnz = cz / (float)(N - 1);

            float cDesert = desertMask01 != null ? Mathf.Clamp01(desertMask01(cnx, cnz)) : 0f;
            float cSlope = data.GetSteepness(cnx, cnz);
            
            if (cSlope > maxSlopeDegrees + 10f) continue;

            for (int k = 0; k < 8; k++)
            {
                int nx = cx + dx8[k];
                int nz = cz + dz8[k];
                if ((uint)nx >= (uint)N || (uint)nz >= (uint)N) continue;

                int ni = Pack(nx, nz);
                if (closed[ni]) continue;

                float nnx = nx / (float)(N - 1);
                float nnz = nz / (float)(N - 1);

                float nDesert = desertMask01 != null ? Mathf.Clamp01(desertMask01(nnx, nnz)) : 0f;
                float nSlope = data.GetSteepness(nnx, nnz);
                float nMountain = mountainMask01 != null ? Mathf.Clamp01(mountainMask01(nnx, nnz)) : 0f;
                
                if (nMountain >= mountainBlockCutoff) continue;
                
                float step = (k % 2 == 0) ? 1.4142f : 1f; 
                float desertPenalty = Mathf.Lerp(0.0f, 1.25f, nDesert); 
                float slopePenalty = Mathf.InverseLerp(0f, maxSlopeDegrees, Mathf.Min(nSlope, maxSlopeDegrees)) * 2.0f;
                
                if (nDesert > desertCutoff) desertPenalty += 0.5f;

                float mountainPenalty = nMountain * mountainAvoidance; 
                float tentativeG = nodes[current].g + step * (1f + desertPenalty + slopePenalty + mountainPenalty);

                if (tentativeG < nodes[ni].g)
                {
                    nodes[ni].x = nx; nodes[ni].z = nz;
                    nodes[ni].g = tentativeG;
                    nodes[ni].f = tentativeG + Heuristic(nx, nz);
                    nodes[ni].parent = current;

                    if (!inOpen[ni])
                    {
                        open.Push(ni, nodes[ni].f);
                        inOpen[ni] = true;
                    }
                    else
                    {
                        open.Push(ni, nodes[ni].f); 
                    }
                }
            }
        }

        return null;
    }

    private static List<Vector2> ReconstructPath(Node[] nodes, int start, int goal, int N)
    {
        var rev = new List<Vector2>(1024);
        int cur = goal;

        int safety = nodes.Length + 10;
        while (cur != -1 && safety-- > 0)
        {
            int x = nodes[cur].x;
            int z = nodes[cur].z;
            float nx = x / (float)(N - 1);
            float nz = z / (float)(N - 1);
            rev.Add(new Vector2(nx, nz));
            if (cur == start) break;
            cur = nodes[cur].parent;
        }

        rev.Reverse();
        return rev;
    }
    
    private static void EnsureRoadMask(TerrainData data)
    {
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;

        if (roadMask01 != null && roadMaskW == w && roadMaskH == h) return;

        roadMaskW = w;
        roadMaskH = h;

        roadMask01 = new Texture2D(w, h, TextureFormat.R8, false, true);
        roadMask01.wrapMode = TextureWrapMode.Clamp;
        roadMask01.filterMode = FilterMode.Bilinear;

        ClearRoadMask(); 
    }

    public static void ClearRoadMask()
    {
        if (roadMask01 == null) return;
        
        var cols = new Color32[roadMask01.width * roadMask01.height];
        roadMask01.SetPixels32(cols);
        roadMask01.Apply(false, false);
    }

    public static float SampleRoadMask01(TerrainData data, float nx, float nz)
    {
        if (roadMask01 == null) return 0f;

        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);
        
        float x = nx * (roadMaskW - 1);
        float y = nz * (roadMaskH - 1);
        
        return roadMask01.GetPixelBilinear(nx, nz).r;
    }
    

    private static void PaintPathLayers(
        Terrain terrain,
        Func<float, float, float> desertMask01,
        List<Vector2> pathNZ,
        int roadLayerIndex,
        int trailLayerIndex,
        float grassHalfWidthWorld,
        float desertHalfWidthWorld,
        float desertCutoff,
        float paintStrength
    )
    {
        var data = terrain.terrainData;

        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;
        int layers = data.alphamapLayers;

        var maps = data.GetAlphamaps(0, 0, aw, ah);

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
                    maps, aw, ah, layers,
                    worldPerAlphaX, worldPerAlphaZ,
                    desertMask01, nx, nz,
                    roadLayerIndex, trailLayerIndex,
                    grassHalfWidthWorld, desertHalfWidthWorld,
                    desertCutoff, paintStrength
                );
            }
        }
        if (roadMask01 != null) roadMask01.Apply(false, false);
        data.SetAlphamaps(0, 0, maps);
    }
    
    private static void StampAtNormalized(
    float[,,] maps,
    int aw, int ah, int layers,
    float worldPerAlphaX, float worldPerAlphaZ,
    Func<float, float, float> desertMask01,
    float nx, float nz,
    int roadLayerIndex,
    int trailLayerIndex,
    float grassHalfWidthWorld,
    float desertHalfWidthWorld,
    float desertCutoff,
    float paintStrength
)
{
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
    int xmax = Mathf.Clamp(Mathf.CeilToInt (ax + radiusPx), 0, aw - 1);
    int zmin = Mathf.Clamp(Mathf.FloorToInt(az - radiusPx), 0, ah - 1);
    int zmax = Mathf.Clamp(Mathf.CeilToInt (az + radiusPx), 0, ah - 1);

    for (int z = zmin; z <= zmax; z++)
    for (int x = xmin; x <= xmax; x++)
    {
        float dxW = (x - ax) * worldPerAlphaX;
        float dzW = (z - az) * worldPerAlphaZ;
        float dist = Mathf.Sqrt(dxW * dxW + dzW * dzW);

        if (dist > halfWidthWorld) continue;
        
        float t = 1f - (dist / Mathf.Max(0.0001f, halfWidthWorld));
        t = t * t * (3f - 2f * t); 
        float w = t * paintStrength;
        
        float curTarget = maps[z, x, targetLayer];
        float newTarget = Mathf.Lerp(curTarget, 1f, w);

        if (roadMask01 != null)
        {
            float cur = roadMask01.GetPixel(x, z).r;
            if (w > cur)
                roadMask01.SetPixel(x, z, new Color(w, 0f, 0f, 1f));
        }
        
        float otherSum = 0f;
        for (int l = 0; l < layers; l++)
            if (l != targetLayer) otherSum += maps[z, x, l];

        float remain = Mathf.Max(0f, 1f - newTarget);
        if (otherSum > 0.0001f)
        {
            float scale = remain / otherSum;
            for (int l = 0; l < layers; l++)
                if (l != targetLayer) maps[z, x, l] *= scale;
        }
        else
        {
            for (int l = 0; l < layers; l++)
                if (l != targetLayer) maps[z, x, l] = 0f;
        }

        maps[z, x, targetLayer] = newTarget;
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
                if (pri[parent] <= pri[i]) break;
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
                if (l >= Count) break;
                int r = l + 1;

                int best = (r < Count && pri[r] < pri[l]) ? r : l;
                if (pri[i] <= pri[best]) break;

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