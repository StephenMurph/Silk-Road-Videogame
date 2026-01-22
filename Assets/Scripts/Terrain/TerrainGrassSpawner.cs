using System.Collections.Generic;
using UnityEngine;

public static class TerrainGrassSpawner
{
    /// <summary>
    /// Spawns grass across the terrain using Terrain tree instances (fast).
    /// Your grassPrefab must be suitable as a Terrain Tree (billboard/mesh/shader).
    /// </summary>
    public static void SpawnGrassAsTrees(
        Terrain terrain,
        GameObject grassPrefab,
        int instanceCount,
        int seed,
        System.Func<float, float, float> grassDensity01,
        bool clearExisting = true,
        float maxSlopeDegrees = 90f,
        Vector2 baseScaleRange = default,
        float yRotationRandom = 360f
    )
    {
        if (!terrain || !grassPrefab) { Debug.LogError("Missing terrain or grassPrefab."); return; }

        if (baseScaleRange == default) baseScaleRange = new Vector2(0.8f, 1.2f);

        TerrainData data = terrain.terrainData;

        // 1) Ensure grass prefab exists in TreePrototypes; get its index
        int protoIndex = EnsureTreePrototype(data, grassPrefab);

        // 2) Build instances
        var rng = new System.Random(seed);
        var instances = new List<TreeInstance>(instanceCount);

        for (int i = 0; i < instanceCount; i++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            float density = grassDensity01 != null ? Mathf.Clamp01(grassDensity01(nx, nz)) : 1f;
            if ((float)rng.NextDouble() > density) continue;

            float slope = data.GetSteepness(nx, nz);
            if (slope > maxSlopeDegrees) continue;

            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            float s = Mathf.Lerp(baseScaleRange.x, baseScaleRange.y, (float)rng.NextDouble());

            instances.Add(new TreeInstance
            {
                prototypeIndex = protoIndex,
                position = new Vector3(nx, h01, nz),
                widthScale = s,
                heightScale = s,
                rotation = Mathf.Deg2Rad * (float)rng.NextDouble() * yRotationRandom,
                color = Color.white,
                lightmapColor = Color.white
            });
        }

        // 3) Apply (optionally clearing existing)
        if (clearExisting)
        {
            data.treeInstances = instances.ToArray();
        }
        else
        {
            var combined = new List<TreeInstance>(data.treeInstances);
            combined.AddRange(instances);
            data.treeInstances = combined.ToArray();
        }

        // Important to update visuals
        terrain.Flush();
    }

    private static int EnsureTreePrototype(TerrainData data, GameObject prefab)
    {
        var protos = data.treePrototypes;
        for (int i = 0; i < protos.Length; i++)
        {
            if (protos[i].prefab == prefab)
                return i;
        }

        // Not found: add it
        var list = new List<TreePrototype>(protos)
        {
            new TreePrototype { prefab = prefab, bendFactor = 0f }
        };
        data.treePrototypes = list.ToArray();
        return list.Count - 1;
    }
}