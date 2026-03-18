using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTravelController : MonoBehaviour
{
    private enum TravelState
    {
        Town,
        Dice,
        Recovering,
        Traveling
    }

    [Header("Refs")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TownManager townSystem;
    [SerializeField] private TerrainRoadGeneratorComponent roadGenerator;
    [SerializeField] private WorldMapUI worldMapUI;
    [SerializeField] private CameraFollowPlayer cameraFollow;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    private PlayerMotor player;

    [Header("Dice")]
    [SerializeField] private GameObject dicePrefab;

    [Header("Path / Hops")]
    [SerializeField] private float minDistanceFromTownWorld = 12f;
    [SerializeField] private float hopSpacingWorld = 8f;
    [SerializeField] private float hopDuration = 0.5f;
    [SerializeField] private float hopHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawSpaces = true;
    [SerializeField] private float gizmoSphereRadius = 0.5f;

    [SerializeField] private RunState runState;
    [SerializeField] private PartyHUDController partyHUD;
    [SerializeField] private ResourceHUDController resourceHUD;
    [SerializeField] private InventoryUIController inventoryUI;
    [SerializeField] private TownUIController townUI;
    
    [Header("Companions")]
    [SerializeField] private GameObject companionPrefab;
    [SerializeField] private float companionFollowStartDelay = 0.55f;
    
    [SerializeField] private GrassDisplacementGlobals grassDisplacementGlobals;
    
    private readonly List<PlayerMotor> companions = new();
    private readonly List<int> companionSpaceIndices = new();
    
    private TravelState state = TravelState.Town;

    private DiceController diceA;
    private DiceController diceB;
    
    private bool diceAReady;
    private bool diceBReady;
    private int diceAValue;
    private int diceBValue;
    
    private Coroutine stateRoutine;

    private readonly List<Vector3> activeSpaces = new();

    private int currentTownId;
    private int nextTownId = -1;
    private int currentSpaceIndex;

    private Transform cameraFocus;

    void Awake()
    {
        if (!terrain) terrain = FindFirstObjectByType<Terrain>();
        if (!townSystem) townSystem = FindFirstObjectByType<TownManager>();
        if (!roadGenerator) roadGenerator = FindFirstObjectByType<TerrainRoadGeneratorComponent>();
        if (!worldMapUI) worldMapUI = FindFirstObjectByType<WorldMapUI>();
        if (!cameraFollow) cameraFollow = FindFirstObjectByType<CameraFollowPlayer>();
        if (!runState) runState = FindFirstObjectByType<RunState>();
        if (!partyHUD) partyHUD = FindFirstObjectByType<PartyHUDController>();
        if (!resourceHUD) resourceHUD = FindFirstObjectByType<ResourceHUDController>();
        if (!inventoryUI) inventoryUI = FindFirstObjectByType<InventoryUIController>();
        if (!townUI) townUI = FindFirstObjectByType<TownUIController>();
        
        if (!grassDisplacementGlobals)
            grassDisplacementGlobals = FindFirstObjectByType<GrassDisplacementGlobals>();

        if (!cameraFocus)
        {
            cameraFocus = new GameObject("CameraFocus").transform;
            cameraFocus.position = Vector3.zero;
        }

        if (cameraFollow)
            cameraFollow.SetTarget(cameraFocus, true);
    }

    void Start()
    {
        if (townSystem != null && townSystem.towns.Count == 0)
            townSystem.GenerateTowns();

        if (roadGenerator != null && roadGenerator.edgesNZ.Count == 0)
            roadGenerator.GenerateRoads();

        if (townSystem == null || townSystem.towns.Count == 0)
        {
            Debug.LogError("No towns found.");
            enabled = false;
            return;
        }

        currentTownId = worldMapUI ? Mathf.Clamp(worldMapUI.currentTownId, 0, townSystem.towns.Count - 1) : 0;
        EnterTownMode();
    }
    
    void Update()
    {
        if (Keyboard.current == null || runState == null || runState.party == null)
            return;
        
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            DebugAddPartyMember();
        }
        
        if (state == TravelState.Traveling)
        {
            RefreshMusicForCurrentContext(false);
        }
    }

    void OnEnable()
    {
        if (worldMapUI != null)
        {
            worldMapUI.TownSelected += OnTownSelectedFromMap;
            worldMapUI.CurrentTownChanged += OnCurrentTownChangedFromMap;
        }
    }

    void OnDisable()
    {
        if (worldMapUI != null)
        {
            worldMapUI.TownSelected -= OnTownSelectedFromMap;
            worldMapUI.CurrentTownChanged -= OnCurrentTownChangedFromMap;
        }
    }

    private void OnTownSelectedFromMap(int newTownId)
    {
        if (state != TravelState.Town) return;

        nextTownId = Mathf.Clamp(newTownId, 0, townSystem.towns.Count - 1);
        if (nextTownId == currentTownId) return;

        if (!BuildSpacesForEdge(currentTownId, nextTownId))
        {
            Debug.LogWarning($"No spaces for edge {currentTownId}->{nextTownId}");
            nextTownId = -1;
            return;
        }

        EnsurePlayerExists();

        if (stateRoutine != null)
            StopCoroutine(stateRoutine);

        stateRoutine = StartCoroutine(BeginTravelFromTown());
    }

    private void OnCurrentTownChangedFromMap(int id)
    {
        currentTownId = Mathf.Clamp(id, 0, townSystem.towns.Count - 1);
    }

    private IEnumerator TravelSequence()
    {
        EnterTravelMode();
        
        while (currentSpaceIndex < activeSpaces.Count - 1)
        {
            state = TravelState.Dice;

            player.EnablePhysics();
            SetCompanionPhysics(true);

            var reset = FindFirstObjectByType<PhysicsResetManager>();
            reset?.Capture();

            yield return WaitForDicePair();

            int total = diceAValue + diceBValue;

            CleanupDice();

            player.DisablePhysics();
            SetCompanionPhysics(false);

            Vector3 forward = GetForwardFromCurrentSpace();

            state = TravelState.Recovering;
            yield return player.RecoverToBoardPose(forward);
            yield return RecoverCompanionsToBoardPose();

            EnterTravelMode();

            state = TravelState.Traveling;

            int pendingMoves = 1 + companions.Count;
            int leaderTargetIndex = Mathf.Clamp(currentSpaceIndex + total, 0, activeSpaces.Count - 1);
            
            player.BeginHopPath(
                this,
                activeSpaces,
                currentSpaceIndex,
                total,
                hopDuration,
                hopHeight,
                i =>
                {
                    currentSpaceIndex = i;
                    pendingMoves--;
                },
                0f);

            
            for (int c = 0; c < companions.Count; c++)
            {
                int capturedIndex = c;
                if (companions[capturedIndex] == null)
                {
                    pendingMoves--;
                    continue;
                }

                int oldIndex = companionSpaceIndices[capturedIndex];

                int targetIndex = Mathf.Clamp(
                    leaderTargetIndex - (capturedIndex + 1),
                    0,
                    activeSpaces.Count - 1
                );

                int hopCount = Mathf.Max(0, targetIndex - oldIndex);

                float startDelay = companionFollowStartDelay * (capturedIndex + 1);

                if (capturedIndex > 0 && oldIndex == companionSpaceIndices[capturedIndex - 1])
                {
                    startDelay += hopDuration * 0.6f;
                }

                if (hopCount == 0)
                {
                    companionSpaceIndices[capturedIndex] = oldIndex;
                    pendingMoves--;
                    continue;
                }

                companions[capturedIndex].BeginHopPath(
                    this,
                    activeSpaces,
                    oldIndex,
                    hopCount,
                    hopDuration,
                    hopHeight,
                    i =>
                    {
                        companionSpaceIndices[capturedIndex] = i;
                        pendingMoves--;
                    },
                    startDelay);
            }

            while (pendingMoves > 0)
                yield return null;

            ResolvePostTravelSupplies();

            if (currentSpaceIndex >= activeSpaces.Count - 1)
                break;
        }

        currentTownId = nextTownId;
        nextTownId = -1;

        EnterTownMode();
        stateRoutine = null;
    }

    /*private IEnumerator EnterDiceModeAndRoll()
    {
        state = TravelState.Dice;

        EnterTravelMode();

        player.EnablePhysics();
        SpawnDice();

        yield return new WaitForSeconds(physicsDiceWindow);
    }

    private IEnumerator RollSingleDie(Action<int> onRolled)
    {
        if (!activeDice)
            SpawnDice();

        waitingForDice = true;
        lastDiceRoll = 0;

        activeDice.OnRolled -= OnDiceRolled;
        activeDice.OnRolled += OnDiceRolled;

        while (waitingForDice)
            yield return null;

        onRolled?.Invoke(lastDiceRoll);
    }*/

    private void SpawnDicePair()
    {
        diceA = Instantiate(dicePrefab).GetComponent<DiceController>();
        diceB = Instantiate(dicePrefab).GetComponent<DiceController>();

        if (!diceA || !diceB)
        {
            Debug.LogError("Dice prefab missing DiceController.");
            return;
        }

        diceA.cameraOffset = new Vector3(-4f, -0.25f, 10f);
        diceB.cameraOffset = new Vector3( 4f, -0.25f, 10f);

        diceA.spinAxis = new Vector3(0.35f, 1f, 0.2f);
        diceB.spinAxis = new Vector3(-0.28f, 1f, -0.18f);

        diceA.transform.rotation = UnityEngine.Random.rotation;
        diceB.transform.rotation = UnityEngine.Random.rotation;

        diceA.OnRolled += OnDiceRolledA;
        diceB.OnRolled += OnDiceRolledB;
        
        if (grassDisplacementGlobals != null)
            grassDisplacementGlobals.SetDice(diceA.transform, diceB.transform);
    }

    private void CleanupDice()
    {
        if (diceA) StartCoroutine(ShrinkAndDestroy(diceA.gameObject, 0.12f));
        if (diceB) StartCoroutine(ShrinkAndDestroy(diceB.gameObject, 0.12f));

        diceA = null;
        diceB = null;
        
        if (grassDisplacementGlobals != null)
            grassDisplacementGlobals.ClearDice();
    }

    private void EnsurePlayerExists()
    {
        if (player != null) return;

        GameObject go = Instantiate(playerPrefab);
        player = go.GetComponent<PlayerMotor>();

        if (!player)
            player = go.AddComponent<PlayerMotor>();

        player.Initialize(terrain);
    }

    private IEnumerator SpawnPlayerAtEdgeStart(int fromTownId)
    {
        Vector3 townPos = TownWorld(fromTownId);

        int startIndex = 0;
        float minDist2 = minDistanceFromTownWorld * minDistanceFromTownWorld;

        for (int i = 0; i < activeSpaces.Count; i++)
        {
            Vector3 a = new Vector3(activeSpaces[i].x, 0f, activeSpaces[i].z);
            Vector3 b = new Vector3(townPos.x, 0f, townPos.z);

            if ((a - b).sqrMagnitude >= minDist2)
            {
                startIndex = i;
                break;
            }
        }

        startIndex = Mathf.Clamp(startIndex, 0, activeSpaces.Count - 2);
        currentSpaceIndex = startIndex;

        Vector3 leaderSpawnPos = activeSpaces[startIndex];
        Vector3 forward = activeSpaces[startIndex + 1] - leaderSpawnPos;

        Vector3 flatForward = forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude > 0.0001f)
        {
            cameraFollow.yaw = Quaternion.LookRotation(-flatForward.normalized, Vector3.up).eulerAngles.y;
        }

        EnterTravelMode(false);

        EnsureCompanionsMatchParty();

        Vector3 townToLeader = leaderSpawnPos - townPos;
        townToLeader.y = 0f;

        float leaderDistanceFromTown = townToLeader.magnitude;
        Vector3 baseDir = leaderDistanceFromTown > 0.001f
            ? townToLeader.normalized
            : flatForward.normalized;

        if (baseDir.sqrMagnitude < 0.0001f)
            baseDir = Vector3.forward;

        int pendingSpawns = 1 + companions.Count;
        
        StartCoroutine(SpawnMotorFromTown(player, townPos, leaderSpawnPos, forward, () =>
        {
            pendingSpawns--;
        }));
        
        for (int i = 0; i < companions.Count; i++)
        {
            if (companions[i] == null)
            {
                pendingSpawns--;
                continue;
            }
            
            float angleDeg = 0f;

            switch (i)
            {
                case 0: angleDeg = -25f; break; 
                case 1: angleDeg =  25f; break; 
                case 2: angleDeg = -50f; break;
                default: angleDeg = 10f * (i + 1); break;
            }

            Vector3 rotatedDir = Quaternion.AngleAxis(angleDeg, Vector3.up) * baseDir;
            rotatedDir.y = 0f;
            rotatedDir.Normalize();

            Vector3 companionSpawnPos = townPos + rotatedDir * leaderDistanceFromTown;
            
            Vector3 companionForward = rotatedDir;
            
            companionSpaceIndices[i] = Mathf.Max(0, startIndex - (i + 1));

            StartCoroutine(SpawnMotorFromTown(companions[i], townPos, companionSpawnPos, companionForward, () =>
            {
                pendingSpawns--;
            }));
        }

        while (pendingSpawns > 0)
            yield return null;

        player.EnablePhysics();
        SetCompanionPhysics(true);
    }

    private Vector3 GetForwardFromCurrentSpace()
    {
        if (activeSpaces == null || activeSpaces.Count < 2)
            return Vector3.forward;

        if (currentSpaceIndex < activeSpaces.Count - 1)
            return activeSpaces[currentSpaceIndex + 1] - activeSpaces[currentSpaceIndex];

        if (currentSpaceIndex > 0)
            return activeSpaces[currentSpaceIndex] - activeSpaces[currentSpaceIndex - 1];

        return Vector3.forward;
    }

    private bool BuildSpacesForEdge(int fromTownId, int toTownId)
    {
        activeSpaces.Clear();

        if (fromTownId < 0 || toTownId < 0) return false;
        if (fromTownId >= townSystem.towns.Count || toTownId >= townSystem.towns.Count) return false;

        Vector2 aNZ = townSystem.towns[fromTownId].nz;
        Vector2 bNZ = townSystem.towns[toTownId].nz;

        if (!TerrainRoadGenerator.TryGetRoadPathNZ(aNZ, bNZ, out var pathNZ))
            return false;

        List<Vector3> spaces = RoadSpaces.BuildSpacesWorld(terrain, pathNZ, hopSpacingWorld, 0f);
        if (spaces == null || spaces.Count < 2)
            return false;

        activeSpaces.AddRange(spaces);
        return true;
    }

    private Vector3 TownWorld(int townId)
    {
        var t = townSystem.towns[townId];
        if (t.go) return t.go.transform.position;

        var data = terrain.terrainData;
        float x = t.nz.x * data.size.x + terrain.transform.position.x;
        float z = t.nz.y * data.size.z + terrain.transform.position.z;
        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        return new Vector3(x, y, z);
    }

    private void EnterTownMode()
    {
        state = TravelState.Town;

        if (partyHUD)
            partyHUD.SetVisible(false);

        if (resourceHUD)
            resourceHUD.SetVisible(false);

        if (inventoryUI)
            inventoryUI.SetTravelUIVisible(false);

        CleanupDice();

        if (player != null)
            Destroy(player.gameObject);

        player = null;
        
        DespawnCompanions();

        if (!cameraFocus) return;

        var townGO = townSystem.towns[currentTownId].go;
        if (!townGO) return;

        cameraFocus.SetParent(null);
        cameraFocus.position = townGO.transform.position;

        if (cameraFollow)
            cameraFollow.SetTarget(cameraFocus, true);

        if (worldMapUI != null)
        {
            worldMapUI.SetCurrentTown(currentTownId, true, false);
        }

        if (townUI != null && currentTownId >= 0 && currentTownId < townSystem.towns.Count)
        {
            townUI.ShowTown(townSystem.towns[currentTownId]);
        }
        
        RefreshMusicForCurrentContext(true);

        Debug.Log($"Entered Town Mode at town {currentTownId}. Map reopened.");
    }

    private void EnterTravelMode(bool snap = false)
    {
        if (!player || !cameraFollow) return;

        if (partyHUD)
        {
            partyHUD.SetVisible(true);
            partyHUD.Refresh();
        }

        if (resourceHUD)
        {
            resourceHUD.SetVisible(true);
            resourceHUD.Refresh();
        }

        if (inventoryUI)
            inventoryUI.SetTravelUIVisible(true);

        RefreshMusicForCurrentContext(false);
        
        cameraFocus.SetParent(null);
        cameraFocus.position = player.transform.position;

        cameraFollow.SetTarget(player.transform, snap);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSpaces || activeSpaces == null || activeSpaces.Count == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < activeSpaces.Count; i++)
        {
            Gizmos.DrawSphere(activeSpaces[i], gizmoSphereRadius);
            if (i > 0)
                Gizmos.DrawLine(activeSpaces[i - 1], activeSpaces[i]);
        }

        Gizmos.color = Color.yellow;
        int idx = Mathf.Clamp(currentSpaceIndex, 0, activeSpaces.Count - 1);
        Gizmos.DrawSphere(activeSpaces[idx], gizmoSphereRadius * 1.25f);
    }
    
    private void OnDiceRolledA(int value)
    {
        diceAValue = value;
        diceAReady = true;
    }

    private void OnDiceRolledB(int value)
    {
        diceBValue = value;
        diceBReady = true;
    }
    
    private IEnumerator WaitForDicePair()
    {
        diceAReady = false;
        diceBReady = false;

        SpawnDicePair();

        while (!diceAReady || !diceBReady)
            yield return null;
    }
    
    private IEnumerator ShrinkAndDestroy(GameObject go, float duration)
    {
        if (!go) yield break;

        Vector3 startScale = go.transform.localScale;
        float t = 0f;

        while (t < duration && go)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, u);
            yield return null;
        }

        if (go) Destroy(go);
    }
    
    private IEnumerator BeginTravelFromTown()
    {
        yield return SpawnPlayerAtEdgeStart(currentTownId);
        stateRoutine = StartCoroutine(TravelSequence());
    }
    
    private void ResolvePostTravelSupplies()
    {
        if (!runState)
        {
            Debug.LogError("PlayerTravelController: No RunState assigned/found.");
            return;
        }

        int foodBefore = runState.resources.food;
        int waterBefore = runState.resources.water;
        int goldBefore = runState.resources.gold;

        TravelSupplyResult result = runState.ResolveSuppliesAfterTravelRoll();

        Debug.Log(
            $"[Supplies] After travel roll | " +
            $"Food: {foodBefore}->{runState.resources.food} (req {result.requiredFood}, used {result.consumedFood}) | " +
            $"Water: {waterBefore}->{runState.resources.water} (req {result.requiredWater}, used {result.consumedWater}) | " +
            $"Gold: {goldBefore}->{runState.resources.gold} (req {result.requiredGold}, used {result.consumedGold})"
        );

        if (result.foodShortageTriggered)
            HandleStarvationTriggered(result);

        if (result.waterShortageTriggered)
            HandleDehydrationTriggered(result);

        if (result.goldShortageTriggered)
            HandleGoldShortageTriggered(result);
    }

    private void HandleStarvationTriggered(TravelSupplyResult result)
    {
        int beforeDead = runState.party.CountDeadMembers();
        int totalDamage = runState.ApplyStarvationDamageToAllMembers();
        int afterDead = runState.party.CountDeadMembers();

        Debug.Log($"[Starvation] Food shortage after travel roll. Applied {runState.starvationDamagePerRoll} to each party member. Total damage dealt: {totalDamage}. Dead members: {beforeDead}->{afterDead}");
    }

    private void HandleDehydrationTriggered(TravelSupplyResult result)
    {
        int beforeDead = runState.party.CountDeadMembers();
        int totalDamage = runState.ApplyDehydrationDamageToAllMembers();
        int afterDead = runState.party.CountDeadMembers();

        Debug.Log($"[Dehydration] Water shortage after travel roll. Applied {runState.dehydrationDamagePerRoll} to each party member. Total damage dealt: {totalDamage}. Dead members: {beforeDead}->{afterDead}");
    }
    
    private void HandleGoldShortageTriggered(TravelSupplyResult result)
    {
        Debug.Log($"[Wages] Gold shortage after travel roll. Required {result.requiredGold}, paid {result.consumedGold}. No penalty implemented yet.");
    }
    
    private void DebugAddPartyMember()
    {
        if (runState == null || runState.party == null)
            return;

        int nextNumber = runState.party.MemberCount + 1;
        bool added = runState.party.TryAddMember($"Companion {nextNumber}");

        if (!added)
        {
            Debug.Log("[Party] Could not add member. Party already full.");
            return;
        }

        Debug.Log($"[Party] Added Companion {nextNumber}. Party count is now {runState.party.MemberCount}");

        partyHUD?.Refresh();
        inventoryUI?.Refresh();

        if (state != TravelState.Town && player != null)
        {
            EnsureCompanionsMatchParty();
            SnapCompanionsToFormation();
        }
    }
    
    private void EnsureCompanionsMatchParty()
    {
        int wantedCompanionCount = Mathf.Max(0, runState.party.MemberCount - 1);
        
        while (companions.Count < wantedCompanionCount)
        {
            GameObject prefabToUse = companionPrefab ? companionPrefab : playerPrefab;
            if (!prefabToUse)
            {
                Debug.LogError("PlayerTravelController: No companionPrefab or fallback playerPrefab assigned.");
                return;
            }

            GameObject go = Instantiate(prefabToUse);
            PlayerMotor motor = go.GetComponent<PlayerMotor>();
            if (!motor)
                motor = go.AddComponent<PlayerMotor>();

            motor.Initialize(terrain);

            companions.Add(motor);
            companionSpaceIndices.Add(0);

            var rb = go.GetComponent<Rigidbody>();
            var reset = FindFirstObjectByType<PhysicsResetManager>();
            if (rb && reset)
                reset.Register(rb);
        }
        
        while (companions.Count > wantedCompanionCount)
        {
            int last = companions.Count - 1;

            if (companions[last] != null)
                Destroy(companions[last].gameObject);

            companions.RemoveAt(last);
            companionSpaceIndices.RemoveAt(last);
        }
    }

    private void DespawnCompanions()
    {
        for (int i = companions.Count - 1; i >= 0; i--)
        {
            if (companions[i] != null)
                Destroy(companions[i].gameObject);
        }

        companions.Clear();
        companionSpaceIndices.Clear();
    }

    private void SnapCompanionsToFormation()
    {
        if (companions.Count == 0 || activeSpaces == null || activeSpaces.Count == 0)
            return;

        for (int i = 0; i < companions.Count; i++)
        {
            if (companions[i] == null) continue;

            int desiredIndex = Mathf.Clamp(currentSpaceIndex - (i + 1), 0, activeSpaces.Count - 1);
            companionSpaceIndices[i] = desiredIndex;

            Vector3 pos = activeSpaces[desiredIndex];
            Vector3 forward = GetForwardFromSpaceIndex(desiredIndex);

            companions[i].WarpToGrounded(pos, forward);
            companions[i].EnablePhysics();
        }
    }

    private void SetCompanionPhysics(bool enabled)
    {
        for (int i = 0; i < companions.Count; i++)
        {
            if (companions[i] == null) continue;
            if (!companions[i].gameObject.activeSelf) continue;

            if (enabled) companions[i].EnablePhysics();
            else companions[i].DisablePhysics();
        }
    }

    private IEnumerator RecoverCompanionsToBoardPose()
    {
        for (int i = 0; i < companions.Count; i++)
        {
            if (companions[i] == null) continue;
            if (!companions[i].gameObject.activeSelf) continue;

            Vector3 forward = GetForwardFromSpaceIndex(companionSpaceIndices[i]);
            yield return companions[i].RecoverToBoardPose(forward);
        }
    }

    private Vector3 GetForwardFromSpaceIndex(int index)
    {
        if (activeSpaces == null || activeSpaces.Count < 2)
            return Vector3.forward;

        index = Mathf.Clamp(index, 0, activeSpaces.Count - 1);

        if (index < activeSpaces.Count - 1)
            return activeSpaces[index + 1] - activeSpaces[index];

        if (index > 0)
            return activeSpaces[index] - activeSpaces[index - 1];

        return Vector3.forward;
    }
    
    private IEnumerator SpawnMotorFromTown(PlayerMotor motor, Vector3 fromTownPos, Vector3 toSpawnPos, Vector3 desiredForward, Action onComplete)
    {
        if (motor == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        yield return motor.SpawnHopFromTown(fromTownPos, toSpawnPos, desiredForward);
        onComplete?.Invoke();
    }
    
    public void OpenDepartMapFromTown()
    {
        if (townUI != null)
            townUI.HideTown();

        if (worldMapUI != null)
        {
            worldMapUI.SetCurrentTown(currentTownId, true, false);
            worldMapUI.OpenMap();
        }
    }
    
    private void RefreshMusicForCurrentContext(bool inTown)
    {
        if (GameAudioManager.Instance == null || townSystem == null)
            return;

        if (inTown)
        {
            if (currentTownId < 0 || currentTownId >= townSystem.towns.Count)
                return;

            var town = townSystem.towns[currentTownId];
            bool isDesertTown = town.biomeType == TownManager.TownBiomeType.Desert;

            GameAudioManager.Instance.PlayMusic(
                isDesertTown
                    ? GameAudioManager.MusicState.DesertTown
                    : GameAudioManager.MusicState.GrasslandTown
            );

            return;
        }

        if (terrain == null)
            return;

        Vector3 samplePos;

        if (player != null)
            samplePos = player.transform.position;
        else if (activeSpaces != null && activeSpaces.Count > 0)
            samplePos = activeSpaces[Mathf.Clamp(currentSpaceIndex, 0, activeSpaces.Count - 1)];
        else
            samplePos = TownWorld(currentTownId);

        float nx = (samplePos.x - terrain.transform.position.x) / terrain.terrainData.size.x;
        float nz = (samplePos.z - terrain.transform.position.z) / terrain.terrainData.size.z;

        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        float desert = 0f;
        if (townSystem.terrainManager != null)
            desert = townSystem.terrainManager.SendMessageDesertMask(nx, nz);

        GameAudioManager.Instance.PlayMusic(
            desert >= 0.35f
                ? GameAudioManager.MusicState.DesertTravel
                : GameAudioManager.MusicState.GrasslandTravel
        );
    }
}