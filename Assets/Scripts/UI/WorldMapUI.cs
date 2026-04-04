using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class WorldMapUI : MonoBehaviour
{
    [Header("Refs")]
    public TerrainManager terrainManager;
    public TownManager townSystem;
    public TerrainRoadGeneratorComponent roadGenerator;

    [Header("Map")]
    public RawImage mapImage;          
    public RectTransform mapRect;      
    public int mapResolution = 512;

    [Header("Towns")]
    public TownDotUI townDotPrefab;
    public RectTransform townDotRoot;

    [Header("Selection")]
    public bool startAtRandomTown = true;
    public int currentTownId = 0;

    [Header("Close Map")]
    public GameObject mapRootToClose;

    [Header("Map Style")]
    public Color grassTint = new Color(0.50f, 0.61f, 0.43f);
    public Color desertTint = new Color(0.76f, 0.67f, 0.45f);
    public Color mountainTint = new Color(0.55f, 0.53f, 0.50f);
    public Color roadColor = new Color(0.22f, 0.16f, 0.10f);
    [Range(1, 12)] public int roadWidthPixels = 3;
    
    [Header("Path Style")]
    public Color pathDotColor = new Color(0.18f, 0.12f, 0.08f);
    [Range(2, 20)] public int pathDotSpacingPixels = 12;
    [Range(1, 8)] public int pathDotRadiusPixels = 2;
    [Range(1, 12)] public int pathBigDotEvery = 5;
    [Range(1, 12)] public int pathBigDotRadiusPixels = 3;

    public event Action<int> CurrentTownChanged;
    public event Action<int> TownSelected;

    private readonly List<TownDotUI> dots = new();

    void Start()
    {
        BuildMap();
    }

    public void BuildMap()
    {
        if (!terrainManager || !townSystem || !mapImage || !mapRect || !townDotPrefab || !townDotRoot)
        {
            Debug.LogError("WorldMapUI: Missing references.");
            return;
        }
        
        if (!roadGenerator) roadGenerator = FindFirstObjectByType<TerrainRoadGeneratorComponent>();

        if (townSystem.towns.Count == 0)
        {
            Debug.Log("WorldMapUI: No towns yet — generating now...");
            townSystem.GenerateTowns();
            var roadGenerator = FindFirstObjectByType<TerrainRoadGeneratorComponent>();
            if (roadGenerator != null)
                roadGenerator.GenerateRoads();
            Debug.Log($"WorldMapUI: towns after generate = {townSystem.towns.Count}");
        }

        var tex = BuildStyledMapTexture(mapResolution);
        mapImage.texture = tex;

        ClearDots();

        if (townSystem.towns.Count > 0 && dots.Count == 0)
        {
            if (startAtRandomTown)
                currentTownId = Random.Range(0, townSystem.towns.Count);
            else
                currentTownId = Mathf.Clamp(currentTownId, 0, townSystem.towns.Count - 1);
        }
        else
        {
            currentTownId = Mathf.Clamp(currentTownId, 0, townSystem.towns.Count - 1);
        }

        CurrentTownChanged?.Invoke(currentTownId);

        SpawnTownDots();
        RefreshSelectableDots();
    }

    private Texture2D BuildStyledMapTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)(size - 1);
                float nz = y / (float)(size - 1);

                float desert = terrainManager.SendMessageDesertMask(nx, nz);
                float mountain = terrainManager.SendMessageMountainMask(nx, nz);
                float grass = Mathf.Max(0f, 1f - desert - mountain);

                Color biomeColor;

                if (mountain >= desert && mountain >= grass)
                    biomeColor = mountainTint;
                else if (desert >= grass)
                    biomeColor = desertTint;
                else
                    biomeColor = grassTint;

                float edgeX = Mathf.Min(nx, 1f - nx);
                float edgeY = Mathf.Min(nz, 1f - nz);
                float edge = Mathf.Min(edgeX, edgeY);
                float vignette = Mathf.InverseLerp(0f, 0.08f, edge);

                biomeColor *= Mathf.Lerp(0.82f, 1f, vignette);

                tex.SetPixel(x, y, biomeColor);
            }
        }

        DrawRoads(tex);
        tex.Apply();

        return tex;
    }

    private void DrawRoads(Texture2D tex)
    {
        if (townSystem == null || roadGenerator.edgesNZ == null) return;

        foreach (var edge in roadGenerator.edgesNZ)
        {
            if (!TerrainRoadGenerator.TryGetRoadPathNZ(edge.aNZ, edge.bNZ, out var pathNZ))
                continue;

            if (pathNZ == null || pathNZ.Count < 2)
                continue;

            List<Vector2> simplified = SimplifyPathByAngle(pathNZ, 8f);
            List<Vector2> smooth = SmoothPathChaikin(simplified, 2);

            DrawDottedPath(tex, smooth, pathDotColor, pathDotSpacingPixels, pathDotRadiusPixels);
        }
    }

    private Vector2Int NZToPixel(Vector2 nz, int width, int height)
    {
        int x = Mathf.RoundToInt(nz.x * (width - 1));
        int y = Mathf.RoundToInt(nz.y * (height - 1));
        return new Vector2Int(x, y);
    }

    private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color, int width)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawBrush(tex, x0, y0, width, color);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void DrawBrush(Texture2D tex, int cx, int cy, int radius, Color color)
    {
        int r = Mathf.Max(1, radius);

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r)
                    continue;

                int px = cx + x;
                int py = cy + y;

                if (px < 0 || px >= tex.width || py < 0 || py >= tex.height)
                    continue;

                Color baseColor = tex.GetPixel(px, py);
                tex.SetPixel(px, py, Color.Lerp(baseColor, color, 0.9f));
            }
        }
    }

    private void ClearDots()
    {
        for (int i = townDotRoot.childCount - 1; i >= 0; i--)
            Destroy(townDotRoot.GetChild(i).gameObject);

        dots.Clear();
    }

    private void SpawnTownDots()
    {
        var towns = townSystem.towns;
        for (int i = 0; i < towns.Count; i++)
        {
            var dot = Instantiate(townDotPrefab, townDotRoot);
            dot.Setup(i, towns[i].nz, this);
            dot.Rect.anchoredPosition = ToMapPosition(towns[i].nz);
            dots.Add(dot);
        }
    }

    private void RefreshSelectableDots()
    {
        var neighbors = (roadGenerator.adjacency != null && roadGenerator.adjacency.TryGetValue(currentTownId, out var list))
            ? list
            : null;

        for (int i = 0; i < dots.Count; i++)
        {
            bool selectable = neighbors != null && neighbors.Contains(i) && i != currentTownId;
            dots[i].SetSelectable(selectable, i == currentTownId);

            dots[i].Rect.sizeDelta = (i == currentTownId)
                ? new Vector2(48, 48)
                : new Vector2(32, 32);
        }
    }

    public void OnTownClicked(int townId)
    {
        if (!roadGenerator.adjacency.TryGetValue(currentTownId, out var neigh))
        {
            Debug.LogError("No adjacency list for current town!");
            return;
        }

        if (!neigh.Contains(townId) || townId == currentTownId)
        {
            Debug.Log($"Rejected click {townId}. Current={currentTownId}. Neigh={string.Join(",", neigh)}");
            return;
        }

        Debug.Log($"Accepted click {townId}. Closing map.");
        TownSelected?.Invoke(townId);

        if (mapRootToClose) mapRootToClose.SetActive(false);
        else transform.root.gameObject.SetActive(false);
    }

    public Vector2 ToMapPosition(Vector2 nz)
    {
        Rect r = mapRect.rect;
        float x = Mathf.Lerp(r.xMin, r.xMax, nz.x);
        float y = Mathf.Lerp(r.yMin, r.yMax, nz.y);
        return new Vector2(x, y);
    }

    public void SetCurrentTown(int id, bool refreshUI = true, bool notify = true)
    {
        currentTownId = Mathf.Clamp(id, 0, townSystem.towns.Count - 1);

        if (notify)
            CurrentTownChanged?.Invoke(currentTownId);

        if (refreshUI)
            RefreshSelectableDots();
    }

    public void OpenMap()
    {
        if (mapRootToClose) mapRootToClose.SetActive(true);
        else transform.root.gameObject.SetActive(true);

        RefreshSelectableDots();
    }
    
    private void DrawDottedPath(
        Texture2D tex,
        List<Vector2> pathNZ,
        Color color,
        int spacingPixels,
        int dotRadius)
    {
        if (pathNZ == null || pathNZ.Count < 2) return;

        List<Vector2Int> pixels = new List<Vector2Int>(pathNZ.Count);
        for (int i = 0; i < pathNZ.Count; i++)
            pixels.Add(NZToPixel(pathNZ[i], tex.width, tex.height));

        float spacing = Mathf.Max(1f, spacingPixels);
        float carry = 0f;
        int dotIndex = 0;

        for (int i = 0; i < pixels.Count - 1; i++)
        {
            Vector2 a = pixels[i];
            Vector2 b = pixels[i + 1];
            Vector2 ab = b - a;
            float len = ab.magnitude;

            if (len < 0.001f)
                continue;

            Vector2 dir = ab / len;

            float dist = carry;
            while (dist <= len)
            {
                Vector2 p = a + dir * dist;

                int radius = dotRadius;
                if (pathBigDotEvery > 0 && dotIndex % pathBigDotEvery == 0)
                    radius = Mathf.Max(radius, pathBigDotRadiusPixels);

                DrawFilledCircle(tex, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), radius, color);

                dotIndex++;
                dist += spacing;
            }

            carry = dist - len;
            if (carry >= spacing) carry = 0f;
        }
    }
    
    private void DrawFilledCircle(Texture2D tex, int cx, int cy, int radius, Color color)
    {
        int r = Mathf.Max(1, radius);
        int rr = r * r;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > rr)
                    continue;

                int px = cx + x;
                int py = cy + y;

                if (px < 0 || px >= tex.width || py < 0 || py >= tex.height)
                    continue;

                Color baseColor = tex.GetPixel(px, py);
                tex.SetPixel(px, py, Color.Lerp(baseColor, color, 0.95f));
            }
        }
    }
    
    private List<Vector2> SimplifyPathByAngle(List<Vector2> path, float angleThresholdDeg)
    {
        List<Vector2> result = new List<Vector2>();
        if (path == null || path.Count < 2) return result;

        result.Add(path[0]);

        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector2 prev = (path[i] - path[i - 1]).normalized;
            Vector2 next = (path[i + 1] - path[i]).normalized;

            if (prev.sqrMagnitude < 0.0001f || next.sqrMagnitude < 0.0001f)
                continue;

            float angle = Vector2.Angle(prev, next);

            if (angle >= angleThresholdDeg)
                result.Add(path[i]);
        }

        result.Add(path[path.Count - 1]);
        return result;
    }
    
    private List<Vector2> SmoothPathChaikin(List<Vector2> points, int iterations)
    {
        if (points == null || points.Count < 2)
            return points == null ? new List<Vector2>() : new List<Vector2>(points);

        List<Vector2> current = new List<Vector2>(points);

        for (int it = 0; it < iterations; it++)
        {
            if (current.Count < 2)
                break;

            List<Vector2> next = new List<Vector2>();
            next.Add(current[0]);

            for (int i = 0; i < current.Count - 1; i++)
            {
                Vector2 p0 = current[i];
                Vector2 p1 = current[i + 1];

                Vector2 q = Vector2.Lerp(p0, p1, 0.25f);
                Vector2 r = Vector2.Lerp(p0, p1, 0.75f);

                next.Add(q);
                next.Add(r);
            }

            next.Add(current[current.Count - 1]);
            current = next;
        }

        return current;
    }
}