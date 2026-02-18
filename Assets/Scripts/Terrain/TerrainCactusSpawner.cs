using System.Collections.Generic;
using UnityEngine;

public static class TerrainCactusSpawner
{
    public static void SpawnCactuses(
        Terrain terrain,
        GameObject[] cactusPrefabs,
        int instanceCount,
        int seed,
        System.Func<float, float, float> biomeDesertMask01,
        float desertCutoff,
        System.Func<float, float, float> biomeMountainMask01,
        float mountainCutoff,
        float maxSlopeDegrees,
        float minHeight01,
        float maxHeight01,
        Vector2 scaleRange,
        float minSpacingWorld = 0f,
        bool clearExistingCactuses = false,
        System.Func<float, float, float> lakeMask01 = null,
        float blockedCutoff = 0.5f,
        float seaLevel01 = 0.08f,
        float seaBuffer01 = 0.01f
    )
    {
        if (!terrain) { Debug.LogError("SpawnCactuses: missing terrain"); return; }
        if (cactusPrefabs == null || cactusPrefabs.Length == 0) { Debug.LogError("SpawnCactuses: no cactusPrefabs"); return; }
        
        var validPrefabs = new List<GameObject>(cactusPrefabs.Length);
        foreach (var p in cactusPrefabs)
            if (p) validPrefabs.Add(p);

        if (validPrefabs.Count == 0)
        {
            Debug.LogError("SpawnCactuses: all entries in cactusPrefabs are null.");
            return;
        }
        
        if (scaleRange.x <= 0f && scaleRange.y <= 0f)
        {
            Debug.LogWarning("SpawnCactuses: scaleRange is (0,0). Forcing to (1,1).");
            scaleRange = new Vector2(1f, 1f);
        }
        if (scaleRange.y < scaleRange.x) (scaleRange.x, scaleRange.y) = (scaleRange.y, scaleRange.x);

        var data = terrain.terrainData;
        
        int[] protoIndices = new int[validPrefabs.Count];
        for (int i = 0; i < validPrefabs.Count; i++)
            protoIndices[i] = EnsureTreePrototype(data, validPrefabs[i]);

        var rng = new System.Random(seed);
        var instances = new List<TreeInstance>(instanceCount);

        Dictionary<int, List<Vector2>> buckets = null;
        float cellSize = minSpacingWorld;

        static int Hash(int x, int z)
        {
            unchecked { return x * 73856093 ^ z * 19349663; }
        }

        if (minSpacingWorld > 0f)
        {
            buckets = new Dictionary<int, List<Vector2>>(instanceCount);
        }

        int safety = Mathf.Max(1000, instanceCount * 20);
        
        int rejNotDesert = 0, rejMountain = 0, rejBlocked = 0, rejSlope = 0, rejHeight = 0, rejRoad = 0, rejSpacing = 0;

        for (int tries = 0; tries < safety && instances.Count < instanceCount; tries++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();
            
            float desert = biomeDesertMask01 != null ? Mathf.Clamp01(biomeDesertMask01(nx, nz)) : 0f;
            if (desert < desertCutoff) { rejNotDesert++; continue; }

            float mountain = biomeMountainMask01 != null ? Mathf.Clamp01(biomeMountainMask01(nx, nz)) : 0f;
            if (mountain >= mountainCutoff) { rejMountain++; continue; }

            float blocked = lakeMask01 != null ? Mathf.Clamp01(lakeMask01(nx, nz)) : 0f;
            if (blocked >= blockedCutoff) { rejBlocked++; continue; }

            float slope = data.GetSteepness(nx, nz);
            if (slope > maxSlopeDegrees) { rejSlope++; continue; }

            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            if (h01 <= seaLevel01 + seaBuffer01) { rejHeight++; continue; }
            if (h01 < minHeight01 || h01 > maxHeight01) { rejHeight++; continue; }

            float road = TerrainRoadGenerator.SampleRoadMask01(data, nx, nz);
            if (road > 0.25f) { rejRoad++; continue; }

            if (buckets != null)
            {
                Vector2 p = new Vector2(nx * data.size.x, nz * data.size.z); 
                int cx = Mathf.FloorToInt(p.x / cellSize);
                int cz = Mathf.FloorToInt(p.y / cellSize);

                float minSqr = minSpacingWorld * minSpacingWorld;
                bool tooClose = false;

                for (int dz = -1; dz <= 1 && !tooClose; dz++)
                for (int dx = -1; dx <= 1 && !tooClose; dx++)
                {
                    int key = Hash(cx + dx, cz + dz);
                    if (!buckets.TryGetValue(key, out var list)) continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if ((list[i] - p).sqrMagnitude < minSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }

                if (tooClose) { rejSpacing++; continue; }

                int myKey = Hash(cx, cz);
                if (!buckets.TryGetValue(myKey, out var mine))
                    buckets[myKey] = mine = new List<Vector2>(4);
                mine.Add(p);
            }

            int pick = rng.Next(0, protoIndices.Length);
            float s = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
            float rot = Mathf.Deg2Rad * 360f * (float)rng.NextDouble();

            instances.Add(new TreeInstance
            {
                prototypeIndex = protoIndices[pick],
                position = new Vector3(nx, h01, nz),
                widthScale = s,
                heightScale = s,
                rotation = rot,
                color = Color.white,
                lightmapColor = Color.white
            });
        }

        if (clearExistingCactuses)
        {
            data.treeInstances = instances.ToArray();
        }
        else
        {
            var combined = new List<TreeInstance>(data.treeInstances);
            combined.AddRange(instances);
            data.treeInstances = combined.ToArray();
        }

        terrain.Flush();

        Debug.Log(
            $"SpawnCactuses: accepted {instances.Count}/{instanceCount} (safety tries {safety}). " +
            $"Rejects: notDesert {rejNotDesert}, mountain {rejMountain}, blocked {rejBlocked}, " +
            $"slope {rejSlope}, height {rejHeight}, road {rejRoad}, spacing {rejSpacing}. " +
            $"Total treeInstances now: {data.treeInstances.Length}. Prototypes: {data.treePrototypes.Length}"
        );
    }

    private static int EnsureTreePrototype(TerrainData data, GameObject prefab)
    {
        var protos = data.treePrototypes;
        for (int i = 0; i < protos.Length; i++)
            if (protos[i].prefab == prefab) return i;

        var list = new List<TreePrototype>(protos)
        {
            new TreePrototype { prefab = prefab, bendFactor = 0f }
        };
        data.treePrototypes = list.ToArray();
        return list.Count - 1;
    }
}

