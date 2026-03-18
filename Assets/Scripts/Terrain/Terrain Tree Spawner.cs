using System.Collections.Generic;
using UnityEngine;

public static class TerrainTreeSpawner
{
    public static void SpawnTrees(
        Terrain terrain,
        GameObject[] treePrefabs,
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
        bool clearExistingTrees = false,
        float blockedCutoff = 0.5f,
        float seaLevel01 = 0.08f,
        float seaBuffer01 = 0.01f
    )
    {
        if (!terrain)
        {
            Debug.LogError("SpawnTrees: missing terrain");
            return;
        }

        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("SpawnTrees: no treePrefabs");
            return;
        }

        var validPrefabs = new List<GameObject>(treePrefabs.Length);
        for (int i = 0; i < treePrefabs.Length; i++)
        {
            if (treePrefabs[i] != null)
                validPrefabs.Add(treePrefabs[i]);
        }

        if (validPrefabs.Count == 0)
        {
            Debug.LogError("SpawnTrees: all tree prefabs are null");
            return;
        }

        if (scaleRange.x <= 0f && scaleRange.y <= 0f)
        {
            Debug.LogWarning("SpawnTrees: scaleRange was (0,0), forcing to (1,1)");
            scaleRange = new Vector2(1f, 1f);
        }

        if (scaleRange.y < scaleRange.x)
            (scaleRange.x, scaleRange.y) = (scaleRange.y, scaleRange.x);

        TerrainData data = terrain.terrainData;
        Transform terrainTransform = terrain.transform;

        Transform root = terrainTransform.Find("TreesRoot");
        if (root == null)
        {
            GameObject rootGo = new GameObject("TreesRoot");
            rootGo.transform.SetParent(terrainTransform, false);
            root = rootGo.transform;
        }

        if (clearExistingTrees)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(root.GetChild(i).gameObject);
                else
                    Object.Destroy(root.GetChild(i).gameObject);
#else
                Object.Destroy(root.GetChild(i).gameObject);
#endif
            }
        }

        var rng = new System.Random(seed);

        Dictionary<int, List<Vector2>> buckets = null;
        float cellSize = minSpacingWorld;

        static int Hash(int x, int z)
        {
            unchecked { return x * 73856093 ^ z * 19349663; }
        }

        if (minSpacingWorld > 0f)
            buckets = new Dictionary<int, List<Vector2>>(instanceCount);

        int safety = Mathf.Max(1000, instanceCount * 20);

        int accepted = 0;
        int rejDesert = 0;
        int rejMountain = 0;
        int rejSlope = 0;
        int rejHeight = 0;
        int rejRoad = 0;
        int rejSpacing = 0;

        for (int tries = 0; tries < safety && accepted < instanceCount; tries++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            float desert = biomeDesertMask01 != null ? Mathf.Clamp01(biomeDesertMask01(nx, nz)) : 0f;
            if (desert >= desertCutoff) { rejDesert++; continue; }

            float mountain = biomeMountainMask01 != null ? Mathf.Clamp01(biomeMountainMask01(nx, nz)) : 0f;
            if (mountain >= mountainCutoff) { rejMountain++; continue; }

            float slope = data.GetSteepness(nx, nz);
            if (slope > maxSlopeDegrees) { rejSlope++; continue; }

            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            if (h01 <= seaLevel01 + seaBuffer01) { rejHeight++; continue; }
            if (h01 < minHeight01 || h01 > maxHeight01) { rejHeight++; continue; }

            float road = TerrainRoadGenerator.SampleRoadMask01(data, nx, nz);
            if (road > 0.20f) { rejRoad++; continue; }

            Vector2 pWorld2D = new Vector2(nx * data.size.x, nz * data.size.z);

            if (buckets != null)
            {
                int cx = Mathf.FloorToInt(pWorld2D.x / cellSize);
                int cz = Mathf.FloorToInt(pWorld2D.y / cellSize);

                float minSqr = minSpacingWorld * minSpacingWorld;
                bool tooClose = false;

                for (int dz = -1; dz <= 1 && !tooClose; dz++)
                {
                    for (int dx = -1; dx <= 1 && !tooClose; dx++)
                    {
                        int key = Hash(cx + dx, cz + dz);
                        if (!buckets.TryGetValue(key, out var list))
                            continue;

                        for (int i = 0; i < list.Count; i++)
                        {
                            if ((list[i] - pWorld2D).sqrMagnitude < minSqr)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                    }
                }

                if (tooClose) { rejSpacing++; continue; }

                int myKey = Hash(cx, cz);
                if (!buckets.TryGetValue(myKey, out var mine))
                    buckets[myKey] = mine = new List<Vector2>(4);

                mine.Add(pWorld2D);
            }

            int pick = rng.Next(validPrefabs.Count);
            GameObject prefab = validPrefabs[pick];

            float worldX = nx * data.size.x + terrainTransform.position.x;
            float worldZ = nz * data.size.z + terrainTransform.position.z;
            float worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrainTransform.position.y;

            Vector3 spawnPos = new Vector3(worldX, worldY, worldZ);
            Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float uniformScale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());

            GameObject tree =
#if UNITY_EDITOR
                Application.isPlaying
                ? Object.Instantiate(prefab, spawnPos, rot, root)
                : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root);
#else
                Object.Instantiate(prefab, spawnPos, rot, root);
#endif

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                tree.transform.SetPositionAndRotation(spawnPos, rot);
            }
#endif

            tree.transform.localScale = prefab.transform.localScale * uniformScale;

            EnsurePhysicsSetup(tree);
            EnsureLeafBurstSetup(tree);

            accepted++;
        }

        terrain.Flush();

        Debug.Log(
            $"SpawnTrees: accepted {accepted}/{instanceCount} (safety tries {safety}). " +
            $"Rejects: desert {rejDesert}, mountain {rejMountain}, slope {rejSlope}, " +
            $"height {rejHeight}, road {rejRoad}, spacing {rejSpacing}."
        );
    }

    private static void EnsurePhysicsSetup(GameObject tree)
    {
        Collider col = tree.GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider capsule = tree.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 2f, 0f);
            capsule.height = 4f;
            capsule.radius = 0.5f;
            capsule.direction = 1;
            col = capsule;
        }

        Rigidbody rb = tree.GetComponent<Rigidbody>();
        if (rb == null)
            rb = tree.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private static void EnsureLeafBurstSetup(GameObject tree)
    {
        if (tree.GetComponent<TreeLeafBurst>() == null)
            tree.AddComponent<TreeLeafBurst>();
    }
}