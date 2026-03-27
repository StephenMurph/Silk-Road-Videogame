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
        Bandits
    }

    [Header("Popup")]
    [SerializeField] private EventPopupUI eventPopupUI;
    [SerializeField] private Sprite rainSprite;
    [SerializeField] private Sprite banditSprite;
    [SerializeField] private Sprite moneySprite;

    [Header("Random Events")]
    [SerializeField, Range(0f, 1f)] private float eventChancePerMove = 0.15f;
    [SerializeField] private int eventSeed = 12345;

    [Header("Rain")]
    [SerializeField] private int rainMovementPenaltyTurns = 3;

    [Header("Rain Visuals")]
    [SerializeField] private Material rainSkybox;
    [SerializeField] private Light mainDirectionalLight;
    [SerializeField] private float rainLightIntensity = 1f;
    [SerializeField] private RainController rainController;

    [Header("Bandits")]
    [SerializeField] private GameObject banditPrefab;
    [SerializeField] private float banditSpawnDistanceAhead = 6f;
    [SerializeField] private float banditSideOffset = 0f;
    [SerializeField] private float banditMinDistanceFromTown = 20f;
    [SerializeField] private EnemyFightController enemyFightController;
    
    [Header("Bandit Music")]
    [SerializeField] private GameAudioManager.MusicState banditMusicState = GameAudioManager.MusicState.Battle;
    
    private TravelEventType lastTriggeredEvent = TravelEventType.None;

    public bool IsWeatherActive =>
        RainTurnsRemaining > 0;

    private GameAudioManager.MusicState previousMusicState;
    private bool hasStoredMusic;

    public int RainTurnsRemaining { get; private set; }
    public bool RainPenaltyActive => RainTurnsRemaining > 0;
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

        rng = new System.Random(eventSeed);
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

        List<TravelEventType> weightedEvents = new List<TravelEventType>();

        if (isGrassland && !IsWeatherActive)
        {
            AddWeightedEvent(weightedEvents, TravelEventType.Rain, 3);
        }

        if (!IsWeatherActive)
        {
            // later:
            // if (isDesert)
            //     AddWeightedEvent(weightedEvents, TravelEventType.Sandstorm, 3);
        }

        bool nearTown = IsNearAnyTown(worldPos, banditMinDistanceFromTown, terrain);

        if (!nearTown)
        {
            AddWeightedEvent(weightedEvents, TravelEventType.Bandits, 3);
        }

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

            case TravelEventType.Bandits:
                TriggerBanditEvent(travelController);
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
        RainTurnsRemaining = rainMovementPenaltyTurns;

        CacheOriginalVisuals();
        ApplyRainVisuals();

        isEventBlockingTravel = false;

        Debug.Log($"Rainfall applied. Single-die movement for {RainTurnsRemaining} turns.");
    }

    public void ConsumeTravelRoll()
    {
        if (RainTurnsRemaining <= 0)
            return;

        RainTurnsRemaining--;

        if (RainTurnsRemaining == 0)
            ShowRainEndedPopup();
    }

    private void ShowRainEndedPopup()
    {
        RestoreVisuals();
        isEventBlockingTravel = true;

        if (!eventPopupUI)
            return;

        eventPopupUI.ShowSimpleEvent(
            "Rainfall",
            "The rain has stopped.",
            rainSprite,
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

        eventPopupUI.ShowSimpleEvent(
            "Bandits",
            $"The bandits took {stolenGold} gold.",
            banditSprite,
            "OK",
            () =>
            {
                RestoreMusic();
                ClearBandit();
                isEventBlockingTravel = false;
            }
        );

        Debug.Log($"Bandits event: give up gold selected. Stolen gold = {stolenGold}");
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
}