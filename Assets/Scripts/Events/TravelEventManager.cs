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

    public int RainTurnsRemaining { get; private set; }
    public bool RainPenaltyActive => RainTurnsRemaining > 0;
    public bool EventPopupShowing => eventPopupUI != null && eventPopupUI.IsShowing;

    private System.Random rng;

    private Material originalSkybox;
    private float originalLightIntensity;
    private bool visualsCached;

    private GameObject activeBandit;

    private void Awake()
    {
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

        if (isGrassland)
        {
            validEvents.Add(TravelEventType.Rain);
            validEvents.Add(TravelEventType.Bandits);
        }

        if (validEvents.Count == 0)
            return TravelEventType.None;

        return validEvents[rng.Next(validEvents.Count)];
    }

    private void TriggerEvent(TravelEventType eventType, PlayerTravelController travelController)
    {
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

        if (!eventPopupUI)
            return;

        eventPopupUI.ShowSimpleEvent(
            "Rainfall",
            "The rain has stopped.",
            rainSprite,
            "OK"
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

        yield return actor.DropFromSky(spawnPos);

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
        }

        Collider[] cols = bandit.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = enabled;

        UprightStabilizer stabilizer = bandit.GetComponent<UprightStabilizer>();
        if (stabilizer)
            stabilizer.enabled = enabled;
    }

    private void HandleBanditGiveGold()
    {
        int stolenGold = rng.Next(8, 26);

        eventPopupUI.ShowSimpleEvent(
            "Bandits",
            $"The bandits took {stolenGold} gold.",
            banditSprite,
            "OK",
            ClearBandit
        );

        Debug.Log($"Bandits event: give up gold selected. Stolen gold = {stolenGold}");
    }

    private void HandleBanditFight()
    {
        Debug.Log("Bandits event: Fight selected.");
        // Later: enable combat mode here
        // SetBanditPhysicsEnabled(activeBandit, true);
        // Start fight minigame
    }

    private void ClearBandit()
    {
        if (activeBandit != null)
            Destroy(activeBandit);

        activeBandit = null;
    }
}