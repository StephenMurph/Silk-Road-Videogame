using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Random = UnityEngine.Random;

public class WorldMapUI : MonoBehaviour
{
    [Header("Refs")]
    public TerrainManager terrainManager;
    public TerrainTownRoadSystem townSystem;

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
    
    public event Action<int> CurrentTownChanged;

    private readonly List<TownDotUI> dots = new();
    public event Action<int> TownSelected;

    void Start()
    {
        BuildMap();
    }

    public void BuildMap()
    {
        var tex = terrainManager.BuildBiomeMapTexture(mapResolution);
        mapImage.texture = tex;

        ClearDots();

        // Ensure towns exist
        if (townSystem.towns.Count == 0)
        {
            Debug.Log("WorldMapUI: No towns yet — generating now...");
            townSystem.GenerateTownsAndRoads();
            Debug.Log($"WorldMapUI: towns after generate = {townSystem.towns.Count}");
        }

        // Decide starting town EVERY time BuildMap runs
        if (townSystem.towns.Count > 0)
        {
            if (startAtRandomTown)
                currentTownId = Random.Range(0, townSystem.towns.Count);
            else
                currentTownId = Mathf.Clamp(currentTownId, 0, townSystem.towns.Count - 1);
        }

        // ALWAYS notify listeners of current town
        CurrentTownChanged?.Invoke(currentTownId);

        SpawnTownDots();
        RefreshSelectableDots();
    }

    void ClearDots()
    {
        for (int i = townDotRoot.childCount - 1; i >= 0; i--)
            Destroy(townDotRoot.GetChild(i).gameObject);

        dots.Clear();
    }

    void SpawnTownDots()
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
    
    void RefreshSelectableDots()
    {
        var neighbors = (townSystem.adjacency != null && townSystem.adjacency.TryGetValue(currentTownId, out var list))
            ? list
            : null;

        for (int i = 0; i < dots.Count; i++)
        {
            bool selectable = neighbors != null && neighbors.Contains(i) && i != currentTownId;
            dots[i].SetSelectable(selectable);

            dots[i].Rect.sizeDelta = (i == currentTownId) ? new Vector2(48, 48) : new Vector2(32, 32);
        }
    }
    
    public void OnTownClicked(int townId)
    {
        if (!townSystem.adjacency.TryGetValue(currentTownId, out var neigh))
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
}