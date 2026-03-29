using UnityEngine;

public class GameSceneBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var terrainManager = FindFirstObjectByType<TerrainManager>();
        var saveManager = FindFirstObjectByType<GameSaveManager>();
        var worldMapUI = FindFirstObjectByType<WorldMapUI>();

        if (terrainManager == null)
        {
            Debug.LogError("GameSceneBootstrap: No TerrainManager found.");
            return;
        }

        // LOAD GAME
        if (GameLaunchState.LoadFromSave && GameSaveSystem.HasSave())
        {
            GameSaveData data = GameSaveSystem.Load();

            if (data == null)
            {
                Debug.LogError("GameSceneBootstrap: Save file exists but failed to load.");
                return;
            }

            terrainManager.seed = data.worldSeed;
            terrainManager.offset = data.worldOffset;

            if (worldMapUI != null)
            {
                worldMapUI.startAtRandomTown = false;
                worldMapUI.currentTownId = data.currentTownId;
            }

            terrainManager.Regenerate();

            if (saveManager != null)
                saveManager.ApplyLoadedSave(data);

            GameLaunchState.SuppressTownArrivalEffects = true;
        }
        else
        {
            // NEW GAME
            terrainManager.seed = Random.Range(int.MinValue, int.MaxValue);
            terrainManager.offset = new Vector2(
                Random.Range(-100000f, 100000f),
                Random.Range(-100000f, 100000f)
            );

            if (worldMapUI != null)
            {
                worldMapUI.startAtRandomTown = true;
            }

            terrainManager.Regenerate();
        }
    }

    private void Start()
    {
        LoadingOverlay.HideIfPresent();
    }
}