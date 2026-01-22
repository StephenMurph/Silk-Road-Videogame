using UnityEngine;

[ExecuteAlways]
public class TerrainHeightGenerator : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Terrain terrain;
    
    

    [Header("Heightmap Settings")]
    [Min(33)] public int heightmapResolution = 513; // 2^n + 1
    [Min(1f)] public float terrainHeight = 80f;     // world units

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

    [Header("Objects")] 
    public GameObject grassPrefab;
    public int grassInstanceCount = 50000;

    public enum BiomeType { Grassland, Desert }

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

        TerrainData data = terrain.terrainData;

        data.heightmapResolution = heightmapResolution;
        data.size = new Vector3(data.size.x, terrainHeight, data.size.z);

        float[,] heights = GenerateHeights(heightmapResolution, heightmapResolution, data.size);
        data.SetHeights(0, 0, heights);
        
        ApplyFixedBiomeTextures();
        
        TerrainGrassSpawner.SpawnGrassAsTrees(
            terrain: terrain,
            grassPrefab: grassPrefab,
            instanceCount: grassInstanceCount,
            seed: seed,
            grassDensity01: (nx, nz) =>
            {
                float desert = DesertMaskFromRegions01(nx, nz); // 0..1

                // Hard kill grass once desert influence passes a threshold
                if (desert > 0.2f)
                    return 0f;

                float grass = 1f - desert;

                // Sharpen the falloff near the border
                grass = Mathf.Pow(grass, 3.0f);

                return grass;
            },
            clearExisting: true,
            maxSlopeDegrees: 35f,
            baseScaleRange: new Vector2(0.7f, 1.3f),
            yRotationRandom: 360f
        );
    }

    private float[,] GenerateHeights(int width, int height, Vector3 terrainSize)
    {
        float[,] heights = new float[height, width];

        // Separate PRNG streams so grass + desert patterns differ but remain deterministic
        System.Random prngGrass = new System.Random(seed);
        System.Random prngDesert = new System.Random(seed * 73856093 ^ 19349663);

        Vector2[] grassOffsets = MakeOctaveOffsets(prngGrass, octaves, offset);
        Vector2[] desertOffsets = MakeOctaveOffsets(prngDesert, desertOctaves, offset + new Vector2(777, 333));

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

                // Biome blend mask: 0 grassland -> 1 desert
                float desertMask = DesertMaskFromRegions01(nx, nz);

                // Blend heights smoothly between the two biome shapes
                float finalH = Mathf.Lerp(grassH, desertH, desertMask);

                heights[y, x] = finalH;
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
    
    private float DesertMaskFromRegions01(float nx, float nz)
    {
        // Start from default biome
        float desert = Mathf.Clamp01(defaultDesert);

        if (regions == null) return desert;

        // Later regions override earlier ones (so you can "paint" order)
        for (int i = 0; i < regions.Length; i++)
        {
            var r = regions[i];
            Rect rect = r.normalizedRect;

            // Distance from point to rect (0 inside)
            float dx = 0f;
            if (nx < rect.xMin) dx = rect.xMin - nx;
            else if (nx > rect.xMax) dx = nx - rect.xMax;

            float dz = 0f;
            if (nz < rect.yMin) dz = rect.yMin - nz;
            else if (nz > rect.yMax) dz = nz - rect.yMax;

            float dist = Mathf.Sqrt(dx * dx + dz * dz); // normalized distance

            // Influence is 1 inside, fades to 0 outside over blend width
            float b = Mathf.Max(0.0001f, r.blend);
            float influence = 1f - Mathf.Clamp01(dist / b);

            // Smooth the fade
            influence = influence * influence * (3f - 2f * influence);

            float targetDesert = (r.biome == BiomeType.Desert) ? 1f : 0f;

            // Override with blending
            desert = Mathf.Lerp(desert, targetDesert, influence);
        }

        return desert;
    }
    [ContextMenu("Apply Fixed Biome Textures")]
    public void ApplyFixedBiomeTextures()
    {
        if (!terrain) terrain = GetComponent<Terrain>();
        if (!terrain) { Debug.LogError("No Terrain found."); return; }

        TerrainData data = terrain.terrainData;

        if (!grassLayer || !desertLayer)
        {
            Debug.LogError("Assign grassLayer and desertLayer (TerrainLayer assets).");
            return;
        }

        data.terrainLayers = new[] { grassLayer, desertLayer };

        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;

        float[,,] maps = new float[ah, aw, 2];

        for (int y = 0; y < ah; y++)
        for (int x = 0; x < aw; x++)
        {
            float nx = (float)x / (aw - 1);
            float nz = (float)y / (ah - 1);

            float desert = DesertMaskFromRegions01(nx, nz);
            maps[y, x, 0] = 1f - desert; // grass
            maps[y, x, 1] = desert;      // desert
        }

        data.SetAlphamaps(0, 0, maps);
        terrain.Flush();
    }
}