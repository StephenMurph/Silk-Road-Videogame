using UnityEngine;

[ExecuteAlways]
public class TerrainManager : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Terrain terrain;
    
    [Header("Road Layers")]
    public TerrainLayer dirtRoadLayer;   
    public TerrainLayer desertTrailLayer; 
    
    [Header("Heightmap Settings")]
    [Min(33)] public int heightmapResolution = 513; 
    [Min(1f)] public float terrainHeight = 80f;     
    
    [Header("Biome Height Ranges (Normalized 0..1)")]
    [Range(0f, 1f)] public float grassMin01 = 0.00f;
    [Range(0f, 1f)] public float grassMax01 = 0.25f;

    [Range(0f, 1f)] public float desertMin01 = 0.10f;
    [Range(0f, 1f)] public float desertMax01 = 0.35f;

    [Range(0f, 1f)] public float mountainMin01 = 0.40f;
    [Range(0f, 1f)] public float mountainMax01 = 1.00f;

    [Header("Noise (Layered Perlin / fBm)")]
    [Min(1f)] public float scale = 200f;            
    [Min(1)] public int octaves = 5;
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Min(1f)] public float lacunarity = 2f;
    public int seed = 12345;
    public Vector2 offset;

    [Header("Shaping")]
    public bool ridged = false; 
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Desert Shape (Dunes)")]
    [Min(1f)] public float desertScale = 90f;          
    [Min(1)] public int desertOctaves = 3;
    [Range(0f, 1f)] public float desertPersistence = 0.45f;
    [Min(1f)] public float desertLacunarity = 2.4f;
    [Range(0f, 1f)] public float desertDuneStrength = 0.65f; 
    public AnimationCurve desertHeightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Mountain Biome")]
    public TerrainLayer mountainLayer;

    [Min(1f)] public float mountainScale = 800f;
    [Min(1)]  public int mountainOctaves = 4;
    [Range(0f, 1f)] public float mountainPersistence = 0.5f;
    [Min(1f)] public float mountainLacunarity = 2.2f;
    public bool mountainRidged = true;
    public AnimationCurve mountainHeightCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Objects")] 
    public GameObject grassPrefab;
    public int grassInstanceCount = 50000;
    
    [Header("Trees (Grass Biome Only)")]
    public GameObject[] treePrefabs;     
    public int treeInstanceCount = 2000; 

    [Range(0f, 90f)] public float treeMaxSlope = 25f;
    [Range(0f, 1f)] public float treeMinHeight01 = 0.05f; 
    [Range(0f, 1f)] public float treeMaxHeight01 = 0.85f; 
    [Min(0f)] public float treeMinSpacingWorld = 6f;
    public Vector2 treeScaleRange = new Vector2(0.8f, 1.4f);

    [Header("Cactuses (Desert Biome Only)")]
    public GameObject[] cactusPrefabs;
    public int cactusInstanceCount = 1500;

    [Range(0f, 90f)] public float cactusMaxSlope = 25f;
    [Range(0f, 1f)] public float cactusMinHeight01 = 0.05f;
    [Range(0f, 1f)] public float cactusMaxHeight01 = 0.85f;
    [Min(0f)] public float cactusMinSpacingWorld = 6f;
    public Vector2 cactusScaleRange = new Vector2(0.8f, 1.4f);

    [Tooltip("Extra coast/edge exclusion for cactuses. Set to 0 to allow spawning all the way to the edge. 0.2 is a good starting point.")]
    [Range(0f, 1f)] public float cactusCoastBlock01 = 0.2f;
    
    public float SendMessageDesertMask(float nx, float nz) => DesertMaskFromRegions01(nx, nz);
    public float SendMessageMountainMask(float nx, float nz) => MountainMaskFromRegions01(nx, nz);
    
    public float SendMessageEdgeMask(float nx, float nz) => EdgeMask01(nx, nz);

    public enum BiomeType { Grassland, Desert, Mountain }

    [System.Serializable]
    public struct BiomeRegionRect
    {
        public BiomeType biome;

        [Tooltip("Normalized 0..1 rect over the terrain (x,z).")]
        public Rect normalizedRect;

        [Tooltip("Blend width in normalized units (0..0.5).")]
        [Range(0f, 0.5f)] public float blend;
    }
    
    [Header("Fixed Biome Regions")]
    public BiomeRegionRect[] regions;

    [Header("Random Region Generation")]
    public bool generateRegionsRandomly = true;

    [Range(0f, 0.25f)] public float regionEdgePadding01 = 0.06f;

    [Range(0.05f, 0.8f)] public float minRegionWidth01 = 0.22f;
    [Range(0.05f, 0.8f)] public float maxRegionWidth01 = 0.42f;
    [Range(0.05f, 0.8f)] public float minRegionHeight01 = 0.22f;
    [Range(0.05f, 0.8f)] public float maxRegionHeight01 = 0.42f;

    [Range(0.05f, 0.4f)] public float minMountainRegionWidth01 = 0.10f;
    [Range(0.05f, 0.4f)] public float maxMountainRegionWidth01 = 0.18f;
    [Range(0.05f, 0.4f)] public float minMountainRegionHeight01 = 0.10f;
    [Range(0.05f, 0.4f)] public float maxMountainRegionHeight01 = 0.18f;

    [Range(0f, 0.5f)] public float minRegionBlend01 = 0.08f;
    [Range(0f, 0.5f)] public float maxRegionBlend01 = 0.18f;
    
    [Range(4, 12)] public int biomeSplitBands = 7;
    [Range(0.02f, 0.25f)] public float biomeSplitJitter01 = 0.08f;
    [Range(0.35f, 0.65f)] public float biomeSplitCenter01 = 0.5f;

    [Header("Biome Texture Layers")]
    public TerrainLayer grassLayer;
    public TerrainLayer desertLayer;
    
    [Header("Ocean Edge / Beaches")]
    public bool edgeDropoff = true;

    [Range(0f, 0.5f)] public float edgeWidth01 = 0.08f;     
    [Range(0f, 1f)] public float seaLevel01 = 0.08f;         
    [Range(0f, 0.2f)] public float edgeBelowSea01 = 0.03f;   
    public AnimationCurve edgeFalloffCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Range(0f, 1f)] public float edgeBeachStrength = 1f;

    [Range(0f, 1f)] public float defaultDesert = 0f; 
    
    private void OnValidate()
    {
        if (!terrain) terrain = GetComponent<Terrain>();
        if (heightmapResolution < 33) heightmapResolution = 33;
        if (scale < 1f) scale = 1f;
        if (octaves < 1) octaves = 1;
        if (lacunarity < 1f) lacunarity = 1f;
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        if (!terrain) terrain = GetComponent<Terrain>();
        if (!terrain)
        {
            Debug.LogError("TerrainHeightGenerator: No Terrain assigned/found.");
            return;
        }
        
        if (generateRegionsRandomly)
            GenerateRandomRegions();

        TerrainData data = terrain.terrainData;

        data.heightmapResolution = heightmapResolution;
        data.size = new Vector3(data.size.x, terrainHeight, data.size.z);

        float[,] heights = GenerateHeights(heightmapResolution, heightmapResolution, data.size);


        data.SetHeights(0, 0, heights);
        
        
        ApplyFixedBiomeTextures();
        
        var townManager = GetComponent<TownManager>();
        if (townManager != null)
        {
            townManager.GenerateTowns();
        }
        else
        {
            Debug.LogWarning("No TownManager found (no towns generated).");
        }

        var roadGenerator = GetComponent<TerrainRoadGeneratorComponent>();
        if (roadGenerator != null)
        {
            roadGenerator.GenerateRoads();
        }
        else
        {
            Debug.LogWarning("No TerrainRoadGeneratorComponent found (no roads generated).");
        }
        
        TerrainGrassSpawner.SpawnGrassAsTrees(
            terrain: terrain,
            grassPrefab: grassPrefab,
            instanceCount: grassInstanceCount,
            seed: seed,
            grassDensity01: (nx, nz) =>
            {
                float mountain = MountainMaskFromRegions01(nx, nz);
                if (mountain > 0.20f) return 0f;

                float desert = DesertMaskFromRegions01(nx, nz);
                if (desert > 0.2f) return 0f;

                float grass = Mathf.Pow(1f - desert, 3.0f);

                float road = TerrainRoadGenerator.SampleRoadMask01(terrain.terrainData, nx, nz);
                grass *= (1f - road);          

                
                if (road > 0.10f) grass = 0f;

                return grass;
            },
            clearExisting: true,
            maxSlopeDegrees: 35f,
            baseScaleRange: new Vector2(0.7f, 1.3f),
            yRotationRandom: 360f,
            seaLevel01: seaLevel01,
            seaBuffer01: 0.01f
        );
        
        TerrainTreeSpawner.SpawnTrees(
            terrain: terrain,
            treePrefabs: treePrefabs,
            instanceCount: treeInstanceCount,
            seed: seed ^ 0xBEEF123,
            biomeDesertMask01: (nx, nz) => DesertMaskFromRegions01(nx, nz),
            desertCutoff: 0.35f,
            biomeMountainMask01: (nx, nz) => MountainMaskFromRegions01(nx, nz),
            mountainCutoff: 0.20f,
            maxSlopeDegrees: treeMaxSlope,
            minHeight01: treeMinHeight01,
            maxHeight01: treeMaxHeight01,
            scaleRange: treeScaleRange,
            minSpacingWorld: treeMinSpacingWorld,
            clearExistingTrees: true,
            blockedCutoff: 0.5f,
            seaLevel01: seaLevel01,
            seaBuffer01: 0.01f
        );
        
        TerrainCactusSpawner.SpawnCactuses(
            terrain: terrain,
            cactusPrefabs: cactusPrefabs,
            instanceCount: cactusInstanceCount,
            seed: seed ^ 0xCAC,
            biomeDesertMask01: (nx, nz) => CactusDesertInteriorMask01(nx, nz),
            desertCutoff: 0.35f,
            biomeMountainMask01: (nx, nz) => MountainMaskFromRegions01(nx, nz),
            mountainCutoff: 0.20f,
            maxSlopeDegrees: cactusMaxSlope,
            minHeight01: cactusMinHeight01,
            maxHeight01: cactusMaxHeight01,
            scaleRange: cactusScaleRange,
            minSpacingWorld: cactusMinSpacingWorld,
            clearExistingCactuses: true,
            blockedCutoff: 0.5f,
            seaLevel01: seaLevel01,
            seaBuffer01: 0.01f
        );
        
    }

    private static float Remap01ToRange(float v01, float min01, float max01)
    {
        if (max01 < min01) (min01, max01) = (max01, min01);
        return Mathf.Lerp(min01, max01, Mathf.Clamp01(v01));
    }

    private static void FixRange01(ref float min01, ref float max01)
    {
        min01 = Mathf.Clamp01(min01);
        max01 = Mathf.Clamp01(max01);
        if (max01 < min01) (min01, max01) = (max01, min01);
    }
    private void GenerateRandomRegions()
    {
        System.Random prng = new System.Random(seed ^ 0x51A7);

        bool verticalSplit = prng.NextDouble() < 0.5; 
        int bands = Mathf.Max(4, biomeSplitBands);

        float center = Mathf.Clamp(biomeSplitCenter01, 0.35f, 0.65f);
        float jitter = Mathf.Clamp01(biomeSplitJitter01);

        var generated = new System.Collections.Generic.List<BiomeRegionRect>(bands * 2 + 1);

        for (int i = 0; i < bands; i++)
        {
            float t0 = i / (float)bands;
            float t1 = (i + 1) / (float)bands;

            float split = center + RandomRange(prng, -jitter, jitter);
            split = Mathf.Clamp(split, 0.2f, 0.8f);

            float blend = RandomRange(prng, minRegionBlend01, maxRegionBlend01);

            BiomeRegionRect grassRect;
            BiomeRegionRect desertRect;

            if (verticalSplit)
            {
                grassRect = new BiomeRegionRect
                {
                    biome = BiomeType.Grassland,
                    normalizedRect = Rect.MinMaxRect(
                        0f,
                        t0,
                        split,
                        t1
                    ),
                    blend = blend
                };

                desertRect = new BiomeRegionRect
                {
                    biome = BiomeType.Desert,
                    normalizedRect = Rect.MinMaxRect(
                        split,
                        t0,
                        1f,
                        t1
                    ),
                    blend = blend
                };
            }
            else
            {
                grassRect = new BiomeRegionRect
                {
                    biome = BiomeType.Grassland,
                    normalizedRect = Rect.MinMaxRect(
                        t0,
                        0f,
                        t1,
                        split
                    ),
                    blend = blend
                };

                desertRect = new BiomeRegionRect
                {
                    biome = BiomeType.Desert,
                    normalizedRect = Rect.MinMaxRect(
                        t0,
                        split,
                        t1,
                        1f
                    ),
                    blend = blend
                };
            }
            
            if ((i & 1) == 0)
            {
                generated.Add(grassRect);
                generated.Add(desertRect);
            }
            else
            {
                generated.Add(desertRect);
                generated.Add(grassRect);
            }
        }

        Vector2 mountainCenter;
        if (verticalSplit)
        {
            mountainCenter = new Vector2(
                RandomRange(prng, center - 0.16f, center - 0.05f),
                RandomRange(prng, 0.25f, 0.75f)
            );
        }
        else
        {
            mountainCenter = new Vector2(
                RandomRange(prng, 0.25f, 0.75f),
                RandomRange(prng, center - 0.16f, center - 0.05f)
            );
        }

        BiomeRegionRect mainMountain = MakeRegion(
            prng,
            BiomeType.Mountain,
            mountainCenter,
            minMountainRegionWidth01,
            maxMountainRegionWidth01,
            minMountainRegionHeight01,
            maxMountainRegionHeight01
        );

        generated.Add(mainMountain);
        
        int extraMountainChunks = 2 + prng.Next(0, 2); 

        for (int i = 0; i < extraMountainChunks; i++)
        {
            Vector2 dir = RandomInsideUnitCircle(prng);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;

            dir.Normalize();

            float offsetX = dir.x * RandomRange(prng, 0.03f, 0.09f);
            float offsetY = dir.y * RandomRange(prng, 0.03f, 0.09f);

            Vector2 chunkCenter = new Vector2(
                Mathf.Clamp01(mountainCenter.x + offsetX),
                Mathf.Clamp01(mountainCenter.y + offsetY)
            );

            generated.Add(MakeRegion(
                prng,
                BiomeType.Mountain,
                chunkCenter,
                minMountainRegionWidth01 * 0.45f,
                maxMountainRegionWidth01 * 0.75f,
                minMountainRegionHeight01 * 0.45f,
                maxMountainRegionHeight01 * 0.75f
            ));
        }

        regions = generated.ToArray();
    }
    
    private BiomeRegionRect MakeOffsetRegion(
        System.Random prng,
        BiomeType biome,
        Vector2 baseCenter,
        float offsetScale,
        float minWidth,
        float maxWidth,
        float minHeight,
        float maxHeight)
    {
        Vector2 dir = RandomInsideUnitCircle(prng).normalized;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        float width = RandomRange(prng, minWidth, maxWidth) * RandomRange(prng, 0.55f, 0.8f);
        float height = RandomRange(prng, minHeight, maxHeight) * RandomRange(prng, 0.55f, 0.8f);

        float offsetX = dir.x * width * offsetScale;
        float offsetY = dir.y * height * offsetScale;

        Vector2 center = new Vector2(baseCenter.x + offsetX, baseCenter.y + offsetY);
        center.x = Mathf.Clamp01(center.x);
        center.y = Mathf.Clamp01(center.y);

        float blend = RandomRange(prng, minRegionBlend01, maxRegionBlend01);

        float pad = Mathf.Clamp01(regionEdgePadding01);

        float xMin = Mathf.Clamp(center.x - width * 0.5f, pad, 1f - pad);
        float yMin = Mathf.Clamp(center.y - height * 0.5f, pad, 1f - pad);
        float xMax = Mathf.Clamp(center.x + width * 0.5f, pad, 1f - pad);
        float yMax = Mathf.Clamp(center.y + height * 0.5f, pad, 1f - pad);

        return new BiomeRegionRect
        {
            biome = biome,
            normalizedRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax),
            blend = blend
        };
    }

    private static Vector2 RandomInsideUnitCircle(System.Random prng)
    {
        float a = RandomRange(prng, 0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt((float)prng.NextDouble());
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    private BiomeRegionRect MakeRegion(
        System.Random prng,
        BiomeType biome,
        Vector2 center,
        float minWidth,
        float maxWidth,
        float minHeight,
        float maxHeight)
    {
        float width = RandomRange(prng, minWidth, maxWidth);
        float height = RandomRange(prng, minHeight, maxHeight);
        float blend = RandomRange(prng, minRegionBlend01, maxRegionBlend01);

        float pad = Mathf.Clamp01(regionEdgePadding01);

        float xMin = Mathf.Clamp(center.x - width * 0.5f, pad, 1f - pad);
        float yMin = Mathf.Clamp(center.y - height * 0.5f, pad, 1f - pad);
        float xMax = Mathf.Clamp(center.x + width * 0.5f, pad, 1f - pad);
        float yMax = Mathf.Clamp(center.y + height * 0.5f, pad, 1f - pad);

        return new BiomeRegionRect
        {
            biome = biome,
            normalizedRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax),
            blend = blend
        };
    }

    private static float RandomRange(System.Random prng, float min, float max)
    {
        if (max < min) (min, max) = (max, min);
        return Mathf.Lerp(min, max, (float)prng.NextDouble());
    }
    
    private float[,] GenerateHeights(int width, int height, Vector3 terrainSize)
    {
        float[,] heights = new float[height, width];
        
        System.Random prngGrass = new System.Random(seed);
        System.Random prngDesert = new System.Random(seed * 73856093 ^ 19349663);

        Vector2[] grassOffsets = MakeOctaveOffsets(prngGrass, octaves, offset);
        Vector2[] desertOffsets = MakeOctaveOffsets(prngDesert, desertOctaves, offset + new Vector2(777, 333));
        System.Random prngMountain = new System.Random(seed * 83492791 ^ 1299709);
        Vector2[] mountainOffsets = MakeOctaveOffsets(prngMountain, mountainOctaves, offset + new Vector2(999, 111));
        float mountainMaxPossible = MaxPossibleAmplitude(mountainOctaves, mountainPersistence);

        float grassMaxPossible = MaxPossibleAmplitude(octaves, persistence);
        float desertMaxPossible = MaxPossibleAmplitude(desertOctaves, desertPersistence);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (float)x / (width - 1);  
                float nz = (float)y / (height - 1); 

                float grassH = FractalPerlin01(
                    nx, nz, terrainSize,
                    scale, octaves, persistence, lacunarity,
                    grassOffsets, grassMaxPossible,
                    ridged, heightCurve
                );

                float desertH = DesertDunes01(
                    nx, nz, terrainSize,
                    desertScale, desertOctaves, desertPersistence, desertLacunarity,
                    desertOffsets, desertMaxPossible,
                    desertDuneStrength, desertHeightCurve
                );
                float mountainH = FractalPerlin01(
                    nx, nz, terrainSize,
                    mountainScale, mountainOctaves, mountainPersistence, mountainLacunarity,
                    mountainOffsets, mountainMaxPossible,
                    mountainRidged, mountainHeightCurve
                );
                
                float gMin = grassMin01, gMax = grassMax01;
                float dMin = desertMin01, dMax = desertMax01;
                float mMin = mountainMin01, mMax = mountainMax01;
                FixRange01(ref gMin, ref gMax);
                FixRange01(ref dMin, ref dMax);
                FixRange01(ref mMin, ref mMax);


                float grassBand    = Remap01ToRange(grassH,    gMin, gMax);
                float desertBand   = Remap01ToRange(desertH,   dMin, dMax);
                float mountainBand = Remap01ToRange(mountainH, mMin, mMax);


                var w = BiomeWeightsFromRegions(nx, nz);
                
                float finalH =
                    grassBand    * w.grass +
                    desertBand   * w.desert +
                    mountainBand * w.mountain;
                
                if (edgeDropoff)
                {
                    float e = EdgeMask01(nx, nz);
                    
                    float edgeTarget = seaLevel01 - edgeBelowSea01;
                    
                    finalH = Mathf.Lerp(finalH, edgeTarget, e);
                }

                heights[y, x] = Mathf.Clamp01(finalH);
            }
        }

        return heights;
    }
    
    private static Vector2[] MakeOctaveOffsets(System.Random prng, int octaves, Vector2 baseOffset)
{
    var offs = new Vector2[octaves];
    for (int i = 0; i < octaves; i++)
    {
        float ox = prng.Next(-100000, 100000) + baseOffset.x;
        float oy = prng.Next(-100000, 100000) + baseOffset.y;
        offs[i] = new Vector2(ox, oy);
    }
    return offs;
}
    
private static float MaxPossibleAmplitude(int octaves, float persistence)
{
    float max = 0f;
    float amp = 1f;
    for (int i = 0; i < octaves; i++)
    {
        max += amp;
        amp *= persistence;
    }
    return Mathf.Max(0.0001f, max);
}

private float EdgeMask01(float nx, float nz)
{

    float d = Mathf.Min(nx, 1f - nx, nz, 1f - nz); 

    float w = Mathf.Max(0.0001f, edgeWidth01);
    float t = Mathf.InverseLerp(w, 0f, d); 
    t = Mathf.Clamp01(t);

    return edgeFalloffCurve != null ? Mathf.Clamp01(edgeFalloffCurve.Evaluate(t)) : t;
}

private static float FractalPerlin01(
    float nx, float nz, Vector3 terrainSize,
    float scale, int octaves, float persistence, float lacunarity,
    Vector2[] octaveOffsets, float maxPossible,
    bool ridged, AnimationCurve curve
)
{
    float amplitude = 1f;
    float frequency = 1f;
    float sum = 0f;

    for (int i = 0; i < octaves; i++)
    {
        float sx = nx * (terrainSize.x / scale) * frequency + octaveOffsets[i].x;
        float sz = nz * (terrainSize.z / scale) * frequency + octaveOffsets[i].y;

        float n = Mathf.PerlinNoise(sx, sz);
        if (ridged) n = 1f - Mathf.Abs(n * 2f - 1f);

        sum += n * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    float h = sum / maxPossible;
    h = Mathf.Clamp01(curve.Evaluate(h));
    return h;
}

private static float DesertDunes01(
    float nx, float nz, Vector3 terrainSize,
    float scale, int octaves, float persistence, float lacunarity,
    Vector2[] octaveOffsets, float maxPossible,
    float duneStrength, AnimationCurve curve
)
{
    float amplitude = 1f;
    float frequency = 1f;
    float sum = 0f;

    for (int i = 0; i < octaves; i++)
    {
        float sx = nx * (terrainSize.x / scale) * frequency + octaveOffsets[i].x;
        float sz = nz * (terrainSize.z / scale) * frequency + octaveOffsets[i].y;

        float n = Mathf.PerlinNoise(sx, sz); 
        
        float centered = n * 2f - 1f;


        float bands = 1f - Mathf.Abs(centered); 


        float shaped = Mathf.Lerp(n, bands, Mathf.Clamp01(duneStrength));

        sum += shaped * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    float h = sum / maxPossible;
    h = Mathf.Clamp01(curve.Evaluate(h));
    return h;
}
    
private struct BiomeWeights
{
    public float grass, desert, mountain;
}

private float DesertMaskFromRegions01(float nx, float nz)
{
    return BiomeWeightsFromRegions(nx, nz).desert;
}


private float MountainMaskFromRegions01(float nx, float nz)
{
    return BiomeWeightsFromRegions(nx, nz).mountain;
}

private float CactusDesertInteriorMask01(float nx, float nz)
{

    if (!edgeDropoff || cactusCoastBlock01 <= 0f) return DesertMaskFromRegions01(nx, nz);

    float desert = DesertMaskFromRegions01(nx, nz);
    float e = EdgeMask01(nx, nz) * edgeBeachStrength; 


    if (e >= cactusCoastBlock01) return 0f;

    return desert;
}

private BiomeWeights BiomeWeightsFromRegions(float nx, float nz)
{

    float grass = 1f - Mathf.Clamp01(defaultDesert);
    float desert = Mathf.Clamp01(defaultDesert);
    float mountain = 0f;

    if (regions != null)
    {
        for (int i = 0; i < regions.Length; i++)
        {
            var r = regions[i];
            Rect rect = r.normalizedRect;

            float dx = 0f;
            if (nx < rect.xMin) dx = rect.xMin - nx;
            else if (nx > rect.xMax) dx = nx - rect.xMax;

            float dz = 0f;
            if (nz < rect.yMin) dz = rect.yMin - nz;
            else if (nz > rect.yMax) dz = nz - rect.yMax;

            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            float b = Mathf.Max(0.0001f, r.blend);

            float influence = 1f - Mathf.Clamp01(dist / b);
            influence = influence * influence * (3f - 2f * influence); 


            float tg = 0f, td = 0f, tm = 0f;
            switch (r.biome)
            {
                case BiomeType.Grassland: tg = 1f; break;
                case BiomeType.Desert:    td = 1f; break;
                case BiomeType.Mountain:  tm = 1f; break;
            }
            
            grass    = Mathf.Lerp(grass, tg, influence);
            desert   = Mathf.Lerp(desert, td, influence);
            mountain = Mathf.Lerp(mountain, tm, influence);
        }
    }
    
    if (edgeDropoff && edgeBeachStrength > 0f)
    {
        float e = EdgeMask01(nx, nz) * edgeBeachStrength; 
        if (e > 0f)
        {
            grass    = Mathf.Lerp(grass,    0f, e);
            desert   = Mathf.Lerp(desert,   1f, e);
            mountain = Mathf.Lerp(mountain, 0f, e);
        }
    }


    float sum = grass + desert + mountain;
    if (sum < 0.0001f) { grass = 1f; desert = 0f; mountain = 0f; sum = 1f; }
    grass /= sum; desert /= sum; mountain /= sum;

    return new BiomeWeights { grass = grass, desert = desert, mountain = mountain };
}

    [ContextMenu("Apply Fixed Biome Textures")]
    public void ApplyFixedBiomeTextures()
    {
        if (!terrain) terrain = GetComponent<Terrain>();
        if (!terrain) { Debug.LogError("No Terrain found."); return; }

        TerrainData data = terrain.terrainData;

        if (!grassLayer || !desertLayer || !mountainLayer || !dirtRoadLayer || !desertTrailLayer)
        {
            Debug.LogError("Assign grassLayer, desertLayer, mountainLayer, dirtRoadLayer, desertTrailLayer.");
            return;
        }

        data.terrainLayers = new[] { grassLayer, desertLayer, mountainLayer, dirtRoadLayer, desertTrailLayer };

        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;


        float[,,] maps = new float[ah, aw, 5];

        for (int y = 0; y < ah; y++)
        for (int x = 0; x < aw; x++)
        {
            float nx = (float)x / (aw - 1);
            float nz = (float)y / (ah - 1);

            var w = BiomeWeightsFromRegions(nx, nz);


            float desert = Mathf.Pow(Mathf.SmoothStep(0f, 1f, w.desert), 1.5f);
            float mountain = Mathf.Pow(Mathf.SmoothStep(0f, 1f, w.mountain), 1.2f);
            float grass = Mathf.Max(0f, 1f - desert - mountain);

            float sum = grass + desert + mountain;
            grass /= sum; desert /= sum; mountain /= sum;

            maps[y, x, 0] = grass;
            maps[y, x, 1] = desert;
            maps[y, x, 2] = mountain;
            maps[y, x, 3] = 0f; 
            maps[y, x, 4] = 0f; 
        }

        data.SetAlphamaps(0, 0, maps);
        terrain.Flush();
    }
    
    public Texture2D BuildBiomeMapTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = x / (float)(size - 1);
            float nz = y / (float)(size - 1);

            float desert = DesertMaskFromRegions01(nx, nz);
            float mountain = MountainMaskFromRegions01(nx, nz);
            float grass = Mathf.Max(0f, 1f - desert - mountain);

            Color c;
            if (mountain >= desert && mountain >= grass)
                c = Color.gray;
            else if (desert >= grass)
                c = new Color(1f, 0.85f, 0.2f);
            else
                c = Color.green;

            tex.SetPixel(x, y, c);
        }

        tex.Apply(false, false);
        return tex;
    }
}