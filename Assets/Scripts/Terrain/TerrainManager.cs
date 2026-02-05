using UnityEngine;

[ExecuteAlways]
public class TerrainManager : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Terrain terrain;
    
    [Header("Road Layers")]
    public TerrainLayer dirtRoadLayer;   // used in grasslands
    public TerrainLayer desertTrailLayer; // used in deserts (more subtle)
    
    [Header("Heightmap Settings")]
    [Min(33)] public int heightmapResolution = 513; // 2^n + 1
    [Min(1f)] public float terrainHeight = 80f;     // world units
    
    [Header("Biome Height Ranges (Normalized 0..1)")]
    [Range(0f, 1f)] public float grassMin01 = 0.00f;
    [Range(0f, 1f)] public float grassMax01 = 0.25f;

    [Range(0f, 1f)] public float desertMin01 = 0.10f;
    [Range(0f, 1f)] public float desertMax01 = 0.35f;

    [Range(0f, 1f)] public float mountainMin01 = 0.40f;
    [Range(0f, 1f)] public float mountainMax01 = 1.00f;

    [Header("Noise (Layered Perlin / fBm)")]
    [Min(1f)] public float scale = 200f;            // bigger = smoother
    [Min(1)] public int octaves = 5;
    [Range(0f, 1f)] public float persistence = 0.5f; // amplitude drop per octave
    [Min(1f)] public float lacunarity = 2f;          // frequency rise per octave
    public int seed = 12345;
    public Vector2 offset;

    [Header("Shaping")]
    public bool ridged = false; // makes sharper mountain-like ridges
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Desert Shape (Dunes)")]
    [Min(1f)] public float desertScale = 90f;          // smaller => more frequent dunes
    [Min(1)] public int desertOctaves = 3;
    [Range(0f, 1f)] public float desertPersistence = 0.45f;
    [Min(1f)] public float desertLacunarity = 2.4f;
    [Range(0f, 1f)] public float desertDuneStrength = 0.65f; // how “dune-y” the desert gets
    public AnimationCurve desertHeightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Mountain Biome")]
    public TerrainLayer mountainLayer;

    [Min(1f)] public float mountainScale = 800f;     // BIG features
    [Min(1)]  public int mountainOctaves = 4;
    [Range(0f, 1f)] public float mountainPersistence = 0.5f;
    [Min(1f)] public float mountainLacunarity = 2.2f;
    public bool mountainRidged = true;
    public AnimationCurve mountainHeightCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Objects")] 
    public GameObject grassPrefab;
    public int grassInstanceCount = 50000;
    
    [Header("Trees (Grass Biome Only)")]
    public GameObject[] treePrefabs;     // your tree prefabs
    public int treeInstanceCount = 2000; // start low (500–5000)
    
    [Range(0f, 90f)] public float treeMaxSlope = 25f;
    [Range(0f, 1f)] public float treeMinHeight01 = 0.05f; // avoid underwater/very low
    [Range(0f, 1f)] public float treeMaxHeight01 = 0.85f; // avoid peaks if you want
    [Min(0f)] public float treeMinSpacingWorld = 6f;       // meters, 0 = no spacing
    public Vector2 treeScaleRange = new Vector2(0.8f, 1.4f);
    
    [Header("Lake Generation")]
    public bool generateLake = true;
    [Min(5f)] public float lakeRadiusWorld = 35f;
    [Range(0f, 0.5f)] public float lakeMaxDepth01 = 0.06f;

    [Range(0f, 0.5f)] public float lakeEdgeMargin01 = 0.12f; // keep away from map edge
    [Range(0f, 0.5f)] public float lakeShoreClearance01 = 0.02f; // water sits BELOW local ground by this
    [Range(0f, 0.5f)] public float lakeMinAboveSea01 = 0.03f;     // lake water must be at least this above sea
    [Range(0f, 0.5f)] public float lakeMaxDropFromLocal01 = 0.12f; // prevent giant "crater lakes"
    
    [Header("Lake Shape")]
    [Range(0.5f, 2f)] public float lakeOvalAspect = 1.35f; // >1 = oval (stretched)
    [Range(0f, 180f)] public float lakeRotationDeg = 0f;   // 0 = random each regen if you want
    public bool randomizeLakeRotation = true;

    [Range(0f, 0.6f)] public float lakeShoreWobble01 = 0.18f; // 0 = perfect ellipse
    [Min(0.1f)] public float lakeWobbleScale = 2.5f;          // bigger = smoother bumps

    [Header("Lake Water Object")]
    public Material waterMaterial; // your ocean water material/shader
    public string lakeObjectName = "Generated Lake";
    private bool hasLake;
    private Vector2 lakeCenterNZ;
    private float lakeWaterLevel01Actual;

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

    [Header("Biome Texture Layers")]
    public TerrainLayer grassLayer;
    public TerrainLayer desertLayer;
    
    [Header("Ocean Edge / Beaches")]
    public bool edgeDropoff = true;

    [Range(0f, 0.5f)] public float edgeWidth01 = 0.08f;     // thickness of beach+drop zone
    [Range(0f, 1f)] public float seaLevel01 = 0.08f;         // your water plane level (normalized)
    [Range(0f, 0.2f)] public float edgeBelowSea01 = 0.03f;   // how far below sea the edge sinks
    public AnimationCurve edgeFalloffCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

// how strongly edge becomes desert (1 = full desert at edge)
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
    private float lakeRotationRad;

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        if (!terrain) terrain = GetComponent<Terrain>();
        if (!terrain)
        {
            Debug.LogError("TerrainHeightGenerator: No Terrain assigned/found.");
            return;
        }

        TerrainData data = terrain.terrainData;

        data.heightmapResolution = heightmapResolution;
        data.size = new Vector3(data.size.x, terrainHeight, data.size.z);

        float[,] heights = GenerateHeights(heightmapResolution, heightmapResolution, data.size);

// 1) Apply base heights so we can sample terrain height for lake placement
        data.SetHeights(0, 0, heights);

// 2) Reset lake state each regen
        hasLake = false;

        if (generateLake)
        {
            if (TryPickLakeCenterNZ(data, tries: 2000, out lakeCenterNZ))
            {
                if (randomizeLakeRotation)
                {
                    var rng = new System.Random(seed ^ 0x1234ABCD);
                    lakeRotationRad = Mathf.Deg2Rad * (float)(rng.NextDouble() * 180.0);
                }
                else
                {
                    lakeRotationRad = Mathf.Deg2Rad * lakeRotationDeg;
                }
                // Compute local water level (prevents floating + mega craters)
                float localH01 = Mathf.Clamp01(data.GetInterpolatedHeight(lakeCenterNZ.x, lakeCenterNZ.y) / data.size.y);

                float desired = localH01 - lakeShoreClearance01;        // water slightly below ground
                float minAllowed = seaLevel01 + lakeMinAboveSea01;      // must be above ocean
                float maxAllowed = localH01 - lakeMaxDropFromLocal01;   // cap crater depth

                lakeWaterLevel01Actual = Mathf.Clamp(desired, minAllowed, maxAllowed);

                // Carve lake basin into the SAME heights array, then re-apply
                CarveLakeBasin(data, heights, lakeCenterNZ, lakeWaterLevel01Actual, lakeRadiusWorld, lakeMaxDepth01);
                data.SetHeights(0, 0, heights);

                hasLake = true;

                // Create visible lake water surface
                CreateOrUpdateLakeWater(data, lakeCenterNZ, lakeWaterLevel01Actual, lakeRadiusWorld);
            }
            else
            {
                Debug.LogWarning("LakeGen: could not find a suitable grassland location.");
            }
        }
        
        
        ApplyFixedBiomeTextures();
        
        TerrainRoadGenerator.GenerateRandomRoadBetweenTwoPoints(
            terrain: terrain,
            desertMask01: (nx, nz) => DesertMaskFromRegions01(nx, nz),
            mountainMask01: (nx, nz) =>
            {
                float m = MountainMaskFromRegions01(nx, nz);

                // HARD blocked zone for roads: lake + buffer (meters)
                float lakeBlock = LakeMask01(nx, nz, extraBufferWorld: 12f);

                return Mathf.Clamp01(Mathf.Max(m, lakeBlock));
            },
            seed: seed ^ 0xA11CE,
            gridSize: 256,
            desertCutoff: 0.35f,
            maxSlopeDegrees: 35f,
            roadLayerIndex: 3,
            trailLayerIndex: 4,
            grassRoadHalfWidthWorld: 7f,
            desertTrailHalfWidthWorld: 3.5f,
            paintStrength: 0.85f,
            mountainAvoidance: 20f,
            mountainBlockCutoff: 0.5f
        );
        
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
                
                if (LakeMask01(nx, nz, extraBufferWorld: 2.5f) > 0f) return 0f;

                float grass = Mathf.Pow(1f - desert, 3.0f);

                float road = TerrainRoadGenerator.SampleRoadMask01(terrain.terrainData, nx, nz);
                grass *= (1f - road);          // 1.0 means no road, 0.0 means full road

                // make roads “hard” (no grass even at edges):
                if (road > 0.15f) grass = 0f;

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
            clearExistingTrees: false,
            lakeMask01: (nx, nz) => LakeMask01(nx, nz, extraBufferWorld: 6f),
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
    
    private float[,] GenerateHeights(int width, int height, Vector3 terrainSize)
    {
        float[,] heights = new float[height, width];

        // Separate PRNG streams so grass + desert patterns differ but remain deterministic
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
                float nx = (float)x / (width - 1);  // 0..1
                float nz = (float)y / (height - 1); // 0..1

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

// multipliers
// Ensure inspector ranges are sane
                float gMin = grassMin01, gMax = grassMax01;
                float dMin = desertMin01, dMax = desertMax01;
                float mMin = mountainMin01, mMax = mountainMax01;
                FixRange01(ref gMin, ref gMax);
                FixRange01(ref dMin, ref dMax);
                FixRange01(ref mMin, ref mMax);

// Remap each biome's noise into its OWN height band
                float grassBand    = Remap01ToRange(grassH,    gMin, gMax);
                float desertBand   = Remap01ToRange(desertH,   dMin, dMax);
                float mountainBand = Remap01ToRange(mountainH, mMin, mMax);

// weights (your existing region-based blend)
                var w = BiomeWeightsFromRegions(nx, nz);

// 3-way blend (now makes physical sense)
                float finalH =
                    grassBand    * w.grass +
                    desertBand   * w.desert +
                    mountainBand * w.mountain;
                
                if (edgeDropoff)
                {
                    float e = EdgeMask01(nx, nz); // 0 interior -> 1 edge

                    // target height at edge (slightly below sea so no cracks)
                    float edgeTarget = seaLevel01 - edgeBelowSea01;

                    // As we approach the edge, blend terrain height down to edgeTarget
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
    // distance to nearest edge in normalized coords
    float d = Mathf.Min(nx, 1f - nx, nz, 1f - nz); // 0 at edge, 0.5 center

    float w = Mathf.Max(0.0001f, edgeWidth01);
    float t = Mathf.InverseLerp(w, 0f, d); // 0 when d>=w, 1 when d<=0
    t = Mathf.Clamp01(t);

    return edgeFalloffCurve != null ? Mathf.Clamp01(edgeFalloffCurve.Evaluate(t)) : t;
}

public float LakeMask01(float nx, float nz, float extraBufferWorld = 0f)
{
    if (!hasLake || !terrain) return 0f;

    TerrainData data = terrain.terrainData;

    // Convert normalized -> world offset from center
    float dxW = (nx - lakeCenterNZ.x) * data.size.x;
    float dzW = (nz - lakeCenterNZ.y) * data.size.z;

    // Rotate into lake local space (MUST match CarveLakeBasin)
    float cosR = Mathf.Cos(lakeRotationRad);
    float sinR = Mathf.Sin(lakeRotationRad);

    float rx = dxW * cosR - dzW * sinR;
    float rz = dxW * sinR + dzW * cosR;

    // Ellipse axes (world) + buffer
    float baseRadius = lakeRadiusWorld + extraBufferWorld;
    float a = baseRadius * lakeOvalAspect;
    float b = baseRadius / lakeOvalAspect;

    // SAME wobble sampling as CarveLakeBasin (deterministic)
    float wobbleOffX = (seed * 0.00123f) % 1000f;
    float wobbleOffZ = (seed * 0.00456f) % 1000f;

    float pn = Mathf.PerlinNoise(
        (rx / (lakeRadiusWorld * lakeWobbleScale)) + wobbleOffX,
        (rz / (lakeRadiusWorld * lakeWobbleScale)) + wobbleOffZ
    );

    float wobble = (pn * 2f - 1f) * lakeShoreWobble01;

    float ang = Mathf.Atan2(rz, rx);
    float wobbleAng = Mathf.Sin(ang * 3f) * (lakeShoreWobble01 * 0.35f);

    // Effective axes with wobble
    float aa = a * (1f + wobble + wobbleAng);
    float bb = b * (1f + wobble + wobbleAng);

    // Inside test
    float u = rx / Mathf.Max(0.001f, aa);
    float v = rz / Mathf.Max(0.001f, bb);
    float d = Mathf.Sqrt(u * u + v * v); // 1 at shoreline

    // HARD: 1 inside, 0 outside
    return (d <= 1f) ? 1f : 0f;
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

        float n = Mathf.PerlinNoise(sx, sz); // 0..1
        if (ridged) n = 1f - Mathf.Abs(n * 2f - 1f);

        sum += n * amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    float h = sum / maxPossible;
    h = Mathf.Clamp01(curve.Evaluate(h));
    return h;
}

/// <summary>
/// Dune-like height: uses Perlin but pushes it into repeating “ridges/bands”
/// so it reads like dunes rather than lumpy hills.
/// </summary>
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

        float n = Mathf.PerlinNoise(sx, sz); // 0..1

        // Turn it into “dune bands”:
        // 1) center to [-1..1]
        float centered = n * 2f - 1f;

        // 2) absolute value makes repeating ridges (like waves)
        float bands = 1f - Mathf.Abs(centered); // 0..1 (peaks at 0.5)

        // Blend between normal noise and banded dunes
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

// ✅ Add mountain mask
private float MountainMaskFromRegions01(float nx, float nz)
{
    return BiomeWeightsFromRegions(nx, nz).mountain;
}
private BiomeWeights BiomeWeightsFromRegions(float nx, float nz)
{
    // Start from defaults
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
            influence = influence * influence * (3f - 2f * influence); // smoothstep

            // Target weights per biome
            float tg = 0f, td = 0f, tm = 0f;
            switch (r.biome)
            {
                case BiomeType.Grassland: tg = 1f; break;
                case BiomeType.Desert:    td = 1f; break;
                case BiomeType.Mountain:  tm = 1f; break;
            }

            // “Paint” toward target using influence
            grass    = Mathf.Lerp(grass, tg, influence);
            desert   = Mathf.Lerp(desert, td, influence);
            mountain = Mathf.Lerp(mountain, tm, influence);
        }
    }
    
    if (edgeDropoff && edgeBeachStrength > 0f)
    {
        float e = EdgeMask01(nx, nz) * edgeBeachStrength; // 0..1
        if (e > 0f)
        {
            grass    = Mathf.Lerp(grass,    0f, e);
            desert   = Mathf.Lerp(desert,   1f, e);
            mountain = Mathf.Lerp(mountain, 0f, e);
        }
    }

    // Normalize so they sum to 1
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

        // MUST match number of layers (5)
        float[,,] maps = new float[ah, aw, 5];

        for (int y = 0; y < ah; y++)
        for (int x = 0; x < aw; x++)
        {
            float nx = (float)x / (aw - 1);
            float nz = (float)y / (ah - 1);

            var w = BiomeWeightsFromRegions(nx, nz);

// Optional: bias desert like before (but keep normalization)
            float desert = Mathf.Pow(Mathf.SmoothStep(0f, 1f, w.desert), 1.5f);
            float mountain = Mathf.Pow(Mathf.SmoothStep(0f, 1f, w.mountain), 1.2f);
            float grass = Mathf.Max(0f, 1f - desert - mountain);

// renormalize
            float sum = grass + desert + mountain;
            grass /= sum; desert /= sum; mountain /= sum;

            maps[y, x, 0] = grass;
            maps[y, x, 1] = desert;
            maps[y, x, 2] = mountain;
            maps[y, x, 3] = 0f; // road
            maps[y, x, 4] = 0f; // trail
        }

        data.SetAlphamaps(0, 0, maps);
        terrain.Flush();
    }
    
    private bool TryPickLakeCenterNZ(TerrainData data, int tries, out Vector2 centerNZ)
    {
        var rng = new System.Random(seed ^ 0xC0FFEE);

        for (int i = 0; i < tries; i++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            // keep away from edges (so it doesn't collide with your edge dropoff/ocean)
            float dEdge = Mathf.Min(nx, 1f - nx, nz, 1f - nz);
            if (dEdge < lakeEdgeMargin01) continue;

            // only grasslands-ish (based on your regions)
            var w = BiomeWeightsFromRegions(nx, nz);
            if (w.desert > 0.20f) continue;
            if (w.mountain > 0.20f) continue;

            // avoid steep
            float slope = data.GetSteepness(nx, nz);
            if (slope > 20f) continue;

            // avoid places that are already near sea level (lake needs to be above sea)
            float h01 = Mathf.Clamp01(data.GetInterpolatedHeight(nx, nz) / data.size.y);
            if (h01 < seaLevel01 + lakeMinAboveSea01 + 0.02f) continue;

            // optional: avoid roads if they exist already (safe even if roadMask not generated)
            float road = TerrainRoadGenerator.SampleRoadMask01(data, nx, nz);
            if (road > 0.15f) continue;

            centerNZ = new Vector2(nx, nz);
            return true;
        }

        centerNZ = default;
        return false;
    }
    
private void CarveLakeBasin(
    TerrainData data,
    float[,] heights,
    Vector2 centerNZ,
    float waterLevel01,
    float baseRadiusWorld,
    float maxDepth01
)
{
    int hm = data.heightmapResolution;
    float worldPerX = data.size.x / (hm - 1);
    float worldPerZ = data.size.z / (hm - 1);

    // Center in heightmap pixels
    float cx = centerNZ.x * (hm - 1);
    float cz = centerNZ.y * (hm - 1);

    // Overscan a bit because wobble can push shoreline outward
    float maxRadiusWorld = baseRadiusWorld * (1f + lakeShoreWobble01 + 0.25f);
    int rPx = Mathf.CeilToInt(maxRadiusWorld / Mathf.Min(worldPerX, worldPerZ));

    int xmin = Mathf.Clamp((int)cx - rPx, 0, hm - 1);
    int xmax = Mathf.Clamp((int)cx + rPx, 0, hm - 1);
    int zmin = Mathf.Clamp((int)cz - rPx, 0, hm - 1);
    int zmax = Mathf.Clamp((int)cz + rPx, 0, hm - 1);

    // Hard floor to prevent abyss lakes
    float floor01 = Mathf.Max(seaLevel01 - 0.02f, waterLevel01 - lakeMaxDepth01);

    // Ellipse axes (world)
    float a = baseRadiusWorld * lakeOvalAspect; // major axis
    float b = baseRadiusWorld / lakeOvalAspect; // minor axis

    // Precompute rotation
    float cosR = Mathf.Cos(lakeRotationRad);
    float sinR = Mathf.Sin(lakeRotationRad);

    // Deterministic wobble offset so shoreline isn't symmetric
    float wobbleOffX = (seed * 0.00123f) % 1000f;
    float wobbleOffZ = (seed * 0.00456f) % 1000f;

    for (int z = zmin; z <= zmax; z++)
    for (int x = xmin; x <= xmax; x++)
    {
        // Local position in WORLD meters relative to lake center
        float dxW = (x - cx) * worldPerX;
        float dzW = (z - cz) * worldPerZ;

        // Rotate into lake's local ellipse space
        float rx = dxW * cosR - dzW * sinR;
        float rz = dxW * sinR + dzW * cosR;

        // Angle around center (for shoreline wobble)
        float ang = Mathf.Atan2(rz, rx); // -pi..pi

        // Smooth wobble using Perlin (0..1) -> (-1..1)
        // Sampling uses rotated coords so bumps follow the lake orientation.
        float pn = Mathf.PerlinNoise((rx / (baseRadiusWorld * lakeWobbleScale)) + wobbleOffX,
                                     (rz / (baseRadiusWorld * lakeWobbleScale)) + wobbleOffZ);
        float wobble = (pn * 2f - 1f) * lakeShoreWobble01;

        // Slight angular component to break up Perlin "blobs"
        float wobbleAng = Mathf.Sin(ang * 3f) * (lakeShoreWobble01 * 0.35f);

        // Effective ellipse axes with wobble (push shoreline in/out)
        float aa = a * (1f + wobble + wobbleAng);
        float bb = b * (1f + wobble + wobbleAng);

        // Ellipse inside test using normalized radius
        float u = rx / Mathf.Max(0.001f, aa);
        float v = rz / Mathf.Max(0.001f, bb);
        float d = Mathf.Sqrt(u * u + v * v); // 1 at shoreline

        if (d > 1f) continue;

        // t: 0 at shore, 1 at center
        float t = 1f - d;
        t = t * t * (3f - 2f * t);

        float target = waterLevel01 - maxDepth01 * t;
        target = Mathf.Max(target, floor01);

        heights[z, x] = Mathf.Min(heights[z, x], target);
    }
}
    private void CreateOrUpdateLakeWater(
        TerrainData data,
        Vector2 lakeCenterNZ,
        float waterLevel01,
        float radiusWorld
    )
    {
        // Find or create object by name under this TerrainManager
        Transform t = transform.Find(lakeObjectName);
        GameObject go = t ? t.gameObject : new GameObject(lakeObjectName);
        go.transform.SetParent(transform, false);

        MeshFilter mf = go.GetComponent<MeshFilter>();
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (!mf) mf = go.AddComponent<MeshFilter>();
        if (!mr) mr = go.AddComponent<MeshRenderer>();

        // Cylinder mesh -> flat disc
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);

        if (waterMaterial) mr.sharedMaterial = waterMaterial;

        float worldX = lakeCenterNZ.x * data.size.x + terrain.transform.position.x;
        float worldZ = lakeCenterNZ.y * data.size.z + terrain.transform.position.z;
        float worldY = terrain.transform.position.y + waterLevel01 * data.size.y;

        go.transform.position = new Vector3(worldX, worldY, worldZ);

        float a = lakeRadiusWorld * lakeOvalAspect;     // major radius
        float b = lakeRadiusWorld / lakeOvalAspect;     // minor radius

        go.transform.localScale = new Vector3(a * 2f, 0.05f, b * 2f);
        go.transform.rotation = Quaternion.Euler(0f, lakeRotationRad * Mathf.Rad2Deg, 0f);

        // no collider
        var col = go.GetComponent<Collider>();
        if (col) col.enabled = false;
    }
}