using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TravelEventManager : MonoBehaviour
{
    public static TravelEventManager Instance { get; private set; }

    public enum TravelEventType
    {
        None,
        Rain,
        Sandstorm,
        Bandits,
        AbandonedCaravanFood,
        AbandonedCaravanWater
    }

    private enum WeatherType
    {
        None,
        Rain,
        Sandstorm
    }
    
    [Header("Popup")]
    [SerializeField] private EventPopupUI eventPopupUI;
    [SerializeField] private Sprite rainSprite;
    [SerializeField] private Sprite sandstormSprite;
    [SerializeField] private Sprite banditSprite;
    [SerializeField] private Sprite moneySprite;
    [SerializeField] private Sprite foodSprite;
    [SerializeField] private Sprite waterSprite;
    
    [Header("Random Events")]
    [SerializeField, Range(0f, 1f)] private float eventChancePerMove = 0.15f;
    [SerializeField] private int eventSeed = 12345;

    [Header("Weather")]
    [SerializeField] private int rainMovementPenaltyTurns = 3;
    [SerializeField] private int sandstormMovementPenaltyTurns = 3;
    
    [Header("Rain Visuals")]
    [SerializeField] private Material rainSkybox;
    [SerializeField] private Light mainDirectionalLight;
    [SerializeField] private float rainLightIntensity = 1f;
    [SerializeField] private RainController rainController;
    
    [Header("Sandstorm Visuals")]
    [SerializeField] private ParticleSystem sandstormParticles;
    [SerializeField] private float sandstormLightIntensity = 1f;

    [Header("Bandits")]
    [SerializeField] private GameObject banditPrefab;
    [SerializeField] private float banditSpawnDistanceAhead = 6f;
    [SerializeField] private float banditSideOffset = 0f;
    [SerializeField] private float banditMinDistanceFromTown = 20f;
    [SerializeField] private EnemyFightController enemyFightController;
    
    [Header("Bandit Music")]
    [SerializeField] private GameAudioManager.MusicState banditMusicState = GameAudioManager.MusicState.Battle;
    
    private WeatherType activeWeather = WeatherType.None;
    
    private TravelEventType lastTriggeredEvent = TravelEventType.None;

    public bool IsWeatherActive => WeatherTurnsRemaining > 0;
    public bool RainPenaltyActive => activeWeather == WeatherType.Rain && WeatherTurnsRemaining > 0;
    public bool SandstormPenaltyActive => activeWeather == WeatherType.Sandstorm && WeatherTurnsRemaining > 0;

    public int WeatherTurnsRemaining { get; private set; }

    private GameAudioManager.MusicState previousMusicState;
    private bool hasStoredMusic;
    public bool EventPopupShowing => eventPopupUI != null && eventPopupUI.IsShowing;

    private System.Random rng;

    private Material originalSkybox;
    private float originalLightIntensity;
    private bool visualsCached;

    private GameObject activeBandit;
    
    private bool isEventBlockingTravel;
    public bool IsEventBlockingTravel => isEventBlockingTravel;

    private void Awake()
    {
        if (!enemyFightController)
            enemyFightController = FindFirstObjectByType<EnemyFightController>();
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!eventPopupUI)
            eventPopupUI = FindFirstObjectByType<EventPopupUI>();

        if (!rainController)
            rainController = FindFirstObjectByType<RainController>();

        var terrainManager = FindFirstObjectByType<TerrainManager>();
        int seedBase = terrainManager != null ? terrainManager.seed : eventSeed;
        rng = new System.Random(seedBase ^ eventSeed);
    }

    public bool TryTriggerRandomEventAtPosition(
        Terrain terrain,
        TerrainManager terrainManager,
        Vector3 worldPos,
        PlayerTravelController travelController)
    {
        if (EventPopupShowing)
            return false;

        if (terrain == null || terrainManager == null || travelController == null)
            return false;

        float roll = (float)rng.NextDouble();
        if (roll > eventChancePerMove)
            return false;

        TravelEventType chosen = ChooseRandomEventForPosition(terrain, terrainManager, worldPos);
        if (chosen == TravelEventType.None)
            return false;

        TriggerEvent(chosen, travelController);
        return true;
    }

    private TravelEventType ChooseRandomEventForPosition(Terrain terrain, TerrainManager terrainManager, Vector3 worldPos)
    {
        List<TravelEventType> validEvents = new List<TravelEventType>();

        float nx = (worldPos.x - terrain.transform.position.x) / terrain.terrainData.size.x;
        float nz = (worldPos.z - terrain.transform.position.z) / terrain.terrainData.size.z;

        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        float desert = terrainManager.SendMessageDesertMask(nx, nz);
        bool isGrassland = desert < 0.35f;
        bool isDesert = desert >= 0.35f;

        List<TravelEventType> weightedEvents = new List<TravelEventType>();

        if (isGrassland && !IsWeatherActive)
        {
            AddWeightedEvent(weightedEvents, TravelEventType.Rain, 3);
        }

        if (isDesert && !IsWeatherActive)
        {
            AddWeightedEvent(weightedEvents, TravelEventType.Sandstorm, 3);
        }
        
        bool nearTown = IsNearAnyTown(worldPos, banditMinDistanceFromTown, terrain);

        if (!nearTown)
        {
            AddWeightedEvent(weightedEvents, TravelEventType.Bandits, 3);
        }

        AddWeightedEvent(weightedEvents, TravelEventType.AbandonedCaravanFood, 2);
        AddWeightedEvent(weightedEvents, TravelEventType.AbandonedCaravanWater, 2);

        if (weightedEvents.Count == 0)
            return TravelEventType.None;

        return weightedEvents[rng.Next(weightedEvents.Count)];
    }

    private void TriggerEvent(TravelEventType eventType, PlayerTravelController travelController)
    {
        lastTriggeredEvent = eventType;

        switch (eventType)
        {
            case TravelEventType.Rain:
                TriggerRainEvent();
                break;

            case TravelEventType.Sandstorm:
                TriggerSandstormEvent();
                break;

            case TravelEventType.Bandits:
                TriggerBanditEvent(travelController);
                break;

            case TravelEventType.AbandonedCaravanFood:
                TriggerAbandonedCaravanFoodEvent();
                break;

            case TravelEventType.AbandonedCaravanWater:
                TriggerAbandonedCaravanWaterEvent();
                break;
        }
    }

    public void TriggerRainEvent()
    {
        isEventBlockingTravel = true;

        if (!eventPopupUI)
        {
            Debug.LogError("TravelEventManager: No EventPopupUI assigned/found.");
            return;
        }

        eventPopupUI.ShowSimpleEvent(
            "Rainfall",
            "It has started to rain.\nMovement penalty for 3 turns.",
            rainSprite,
            "OK",
            ApplyRainPenalty
        );
    }

    private void ApplyRainPenalty()
    {
        activeWeather = WeatherType.Rain;
        WeatherTurnsRemaining = rainMovementPenaltyTurns;

        CacheOriginalVisuals();
        ApplyRainVisuals();

        isEventBlockingTravel = false;

        Debug.Log($"Rainfall applied. Single-die movement for {WeatherTurnsRemaining} turns.");
    }

    public void ConsumeTravelRoll()
    {
        if (WeatherTurnsRemaining <= 0)
            return;

        WeatherTurnsRemaining--;

        if (WeatherTurnsRemaining == 0)
            ShowWeatherEndedPopup();
    }

    private void ShowWeatherEndedPopup()
    {
        WeatherType endedWeather = activeWeather;

        RestoreVisuals();
        activeWeather = WeatherType.None;

        isEventBlockingTravel = true;

        if (!eventPopupUI)
            return;

        string title = endedWeather == WeatherType.Sandstorm ? "Sandstorm" : "Rainfall";
        string body = endedWeather == WeatherType.Sandstorm
            ? "The sandstorm has passed."
            : "The rain has stopped.";

        Sprite sprite = endedWeather == WeatherType.Sandstorm ? sandstormSprite : rainSprite;

        eventPopupUI.ShowSimpleEvent(
            title,
            body,
            sprite,
            "OK",
            () =>
            {
                isEventBlockingTravel = false;
            }
        );
    }

    private void CacheOriginalVisuals()
    {
        if (visualsCached)
            return;

        originalSkybox = RenderSettings.skybox;

        if (mainDirectionalLight != null)
            originalLightIntensity = mainDirectionalLight.intensity;

        visualsCached = true;
    }

    private void ApplyRainVisuals()
    {
        if (rainSkybox != null)
        {
            RenderSettings.skybox = rainSkybox;
            DynamicGI.UpdateEnvironment();
        }

        if (mainDirectionalLight != null)
            mainDirectionalLight.intensity = rainLightIntensity;

        if (rainController != null)
            rainController.StartRain();
    }

    private void RestoreVisuals()
    {
        if (!visualsCached)
            return;

        if (originalSkybox != null)
        {
            RenderSettings.skybox = originalSkybox;
            DynamicGI.UpdateEnvironment();
        }

        if (mainDirectionalLight != null)
            mainDirectionalLight.intensity = originalLightIntensity;

        if (rainController != null)
            rainController.StopRain();

        if (sandstormParticles != null)
            sandstormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void TriggerBanditEvent(PlayerTravelController travelController)
    {
        if (!eventPopupUI)
        {
            Debug.LogError("TravelEventManager: No EventPopupUI assigned/found for bandit event.");
            return;
        }

        if (!travelController)
        {
            Debug.LogError("TravelEventManager: No PlayerTravelController passed for bandit event.");
            return;
        }

        if (!banditPrefab)
        {
            Debug.LogError("TravelEventManager: No banditPrefab assigned.");
            return;
        }

        isEventBlockingTravel = true;
        StartCoroutine(SpawnBanditAndShowPopup(travelController));
    }

    private IEnumerator SpawnBanditAndShowPopup(PlayerTravelController travelController)
    {
        Vector3 spawnPos = travelController.GetBanditSpawnPoint(banditSpawnDistanceAhead, banditSideOffset);

        if (activeBandit != null)
            Destroy(activeBandit);

        activeBandit = Instantiate(banditPrefab);
        activeBandit.name = "BanditEventActor";

        Vector3 lookDir = travelController.GetForwardForEvent();
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            activeBandit.transform.rotation = Quaternion.LookRotation(-lookDir.normalized, Vector3.up);

        SetBanditPhysicsEnabled(activeBandit, false);

        BanditEventActor actor = activeBandit.GetComponent<BanditEventActor>();
        if (actor == null)
            actor = activeBandit.AddComponent<BanditEventActor>();

        StartBanditMusic();

        yield return actor.DropFromSky(spawnPos);
        travelController.CacheCompanionPositions();
        
        if (travelController != null)
        {
            yield return travelController.EnterCombatFormation(activeBandit.transform);
        }
        
        SetBanditPhysicsEnabled(activeBandit, true);

        yield return new WaitForSeconds(0.5f);

        eventPopupUI.ShowChoiceEvent(
            "Bandits",
            "Bandits have stopped you.",
            banditSprite,
            "Give up your gold",
            HandleBanditGiveGold,
            "Fight",
            HandleBanditFight
        );

        RunState runState = FindFirstObjectByType<RunState>();
        bool hasGold = runState != null && runState.resources != null && runState.resources.gold > 0;

        eventPopupUI.SetOption1Interactable(hasGold);
        eventPopupUI.SetOption2Interactable(true);
        
    }

    private void SetBanditPhysicsEnabled(GameObject bandit, bool enabled)
    {
        if (!bandit)
            return;

        Rigidbody rb = bandit.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = !enabled;
            rb.useGravity = enabled;
        }

        Collider[] cols = bandit.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = enabled;

        UprightStabilizer stabilizer = bandit.GetComponent<UprightStabilizer>();
        if (stabilizer)
        {
            stabilizer.enabled = enabled;
            stabilizer.stabilizerWeight = enabled ? 1f : 0f;
        }
    }

    private void HandleBanditGiveGold()
    {
        int stolenGold = rng.Next(8, 26);

        RunState runState = FindFirstObjectByType<RunState>();
        int actualStolen = 0;

        if (runState != null && runState.resources != null)
            actualStolen = runState.resources.ConsumeGold(stolenGold);

        var resourceHUD = FindFirstObjectByType<ResourceHUDController>();
        if (resourceHUD != null)
            resourceHUD.Refresh();

        eventPopupUI.ShowSimpleEvent(
            "Bandits",
            $"The bandits took {actualStolen} gold.",
            banditSprite,
            "OK",
            () =>
            {
                RestoreMusic();
                ClearBandit();
                isEventBlockingTravel = false;
            }
        );

        Debug.Log($"Bandits event: give up gold selected. Stolen gold = {actualStolen}");
    }

    private void HandleBanditFight()
    {
        Debug.Log("Bandits event: Fight selected.");

        if (activeBandit == null || enemyFightController == null)
        {
            Debug.LogError("Enemy fight could not start.");
            isEventBlockingTravel = false;
            RestoreMusic();
            return;
        }

        isEventBlockingTravel = true;
        enemyFightController.StartFight(activeBandit, "Bandit");
    }

    private void ClearBandit()
    {
        if (activeBandit != null)
            Destroy(activeBandit);

        activeBandit = null;
    }
    
    private void StartBanditMusic()
    {
        if (GameAudioManager.Instance == null)
            return;

        if (!hasStoredMusic)
        {
            previousMusicState = GameAudioManager.Instance.CurrentState;
            hasStoredMusic = true;
        }

        GameAudioManager.Instance.PlayMusic(banditMusicState);
    }

    private void RestoreMusic()
    {
        if (GameAudioManager.Instance == null || !hasStoredMusic)
            return;

        GameAudioManager.Instance.PlayMusic(previousMusicState);
        hasStoredMusic = false;
    }
    
    private void AddWeightedEvent(List<TravelEventType> list, TravelEventType eventType, int baseWeight)
    {
        int weight = baseWeight;
        
        if (eventType == lastTriggeredEvent)
            weight = Mathf.Max(1, baseWeight - 2);

        for (int i = 0; i < weight; i++)
            list.Add(eventType);
    }
    
    private bool IsNearAnyTown(Vector3 worldPos, float minDistance, Terrain terrain)
    {
        var townSystem = FindFirstObjectByType<TownManager>();

        if (townSystem == null || terrain == null)
            return false;

        float minDistSq = minDistance * minDistance;

        foreach (var town in townSystem.towns)
        {
            Vector3 townPos;

            if (town.go != null)
            {
                townPos = town.go.transform.position;
            }
            else
            {
                var data = terrain.terrainData;
                float x = town.nz.x * data.size.x + terrain.transform.position.x;
                float z = town.nz.y * data.size.z + terrain.transform.position.z;
                float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                townPos = new Vector3(x, y, z);
            }

            Vector3 a = new Vector3(worldPos.x, 0f, worldPos.z);
            Vector3 b = new Vector3(townPos.x, 0f, townPos.z);

            if ((a - b).sqrMagnitude <= minDistSq)
                return true;
        }

        return false;
    }
    
    public void SetEventBlocking(bool value)
    {
        isEventBlockingTravel = value;
    }
    
    public void FinishActiveEnemyEvent(bool destroyEnemy = true)
    {
        RestoreMusic();

        if (destroyEnemy)
            ClearBandit();

        SetEventBlocking(false);
    }
    
    public void ShowBanditVictoryPopup(int goldReward, System.Action onClosed = null)
    {
        if (!eventPopupUI)
        {
            onClosed?.Invoke();
            return;
        }

        isEventBlockingTravel = true;

        eventPopupUI.ShowSimpleEvent(
            "You won",
            $"You received {goldReward} gold.",
            moneySprite,
            "OK",
            () =>
            {
                isEventBlockingTravel = false;
                onClosed?.Invoke();
            }
        );
    }
    
    private void TriggerAbandonedCaravanFoodEvent()
    {
        isEventBlockingTravel = true;

        if (!eventPopupUI)
        {
            Debug.LogError("TravelEventManager: No EventPopupUI assigned/found for abandoned caravan food event.");
            return;
        }

        int foundFood = rng.Next(50, 101);

        eventPopupUI.ShowSimpleEvent(
            "Abandoned Caravan",
            $"You found an abandoned caravan.\nYou recovered {foundFood} food.",
            foodSprite,
            "OK",
            () =>
            {
                RunState runState = FindFirstObjectByType<RunState>();
                if (runState != null && runState.resources != null)
                    runState.resources.AddFood(foundFood);

                var resourceHUD = FindFirstObjectByType<ResourceHUDController>();
                if (resourceHUD != null)
                    resourceHUD.Refresh();

                isEventBlockingTravel = false;
            }
        );
    }

    private void TriggerAbandonedCaravanWaterEvent()
    {
        isEventBlockingTravel = true;

        if (!eventPopupUI)
        {
            Debug.LogError("TravelEventManager: No EventPopupUI assigned/found for abandoned caravan water event.");
            return;
        }

        int foundWater = rng.Next(50, 101);

        eventPopupUI.ShowSimpleEvent(
            "Abandoned Caravan",
            $"You found an abandoned caravan.\nYou recovered {foundWater} water.",
            waterSprite,
            "OK",
            () =>
            {
                RunState runState = FindFirstObjectByType<RunState>();
                if (runState != null && runState.resources != null)
                    runState.resources.AddWater(foundWater);

                var resourceHUD = FindFirstObjectByType<ResourceHUDController>();
                if (resourceHUD != null)
                    resourceHUD.Refresh();

                isEventBlockingTravel = false;
            }
        );
    }
    
    public void TriggerSandstormEvent()
    {
        isEventBlockingTravel = true;

        if (!eventPopupUI)
        {
            Debug.LogError("TravelEventManager: No EventPopupUI assigned/found.");
            return;
        }

        eventPopupUI.ShowSimpleEvent(
            "Sandstorm",
            "A sandstorm sweeps across the road.\nMovement penalty for 3 turns.",
            sandstormSprite,
            "OK",
            ApplySandstormPenalty
        );
    }

    private void ApplySandstormPenalty()
    {
        activeWeather = WeatherType.Sandstorm;
        WeatherTurnsRemaining = sandstormMovementPenaltyTurns;

        CacheOriginalVisuals();
        ApplySandstormVisuals();

        isEventBlockingTravel = false;

        Debug.Log($"Sandstorm applied. Single-die movement for {WeatherTurnsRemaining} turns.");
    }
    
    private void ApplySandstormVisuals()
    {
        if (mainDirectionalLight != null)
            mainDirectionalLight.intensity = sandstormLightIntensity;

        if (sandstormParticles != null && !sandstormParticles.isPlaying)
            sandstormParticles.Play();
        
        if (rainSkybox != null)
        {
            RenderSettings.skybox = rainSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }
}