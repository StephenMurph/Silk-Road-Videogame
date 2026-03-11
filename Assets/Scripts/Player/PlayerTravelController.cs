using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private TerrainTownRoadSystem townSystem;
    [SerializeField] private WorldMapUI worldMapUI;
    [SerializeField] private CameraFollowPlayer cameraFollow;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    private PlayerMotor player;

    [Header("Dice")]
    [SerializeField] private GameObject dicePrefab;
    [SerializeField] private float delayAfterEachRoll = 0.15f;
    [SerializeField] private float delayBeforeTravel = 0.25f;

    [Header("Path / Hops")]
    [SerializeField] private float minDistanceFromTownWorld = 12f;
    [SerializeField] private float hopSpacingWorld = 8f;
    [SerializeField] private float hopDuration = 0.5f;
    [SerializeField] private float hopHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawSpaces = true;
    [SerializeField] private float gizmoSphereRadius = 0.5f;

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
        if (!townSystem) townSystem = FindFirstObjectByType<TerrainTownRoadSystem>();
        if (!worldMapUI) worldMapUI = FindFirstObjectByType<WorldMapUI>();
        if (!cameraFollow) cameraFollow = FindFirstObjectByType<CameraFollowPlayer>();

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
            townSystem.GenerateTownsAndRoads();

        if (townSystem == null || townSystem.towns.Count == 0)
        {
            Debug.LogError("No towns found.");
            enabled = false;
            return;
        }

        currentTownId = worldMapUI ? Mathf.Clamp(worldMapUI.currentTownId, 0, townSystem.towns.Count - 1) : 0;
        EnterTownMode();
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
            
            var reset = FindFirstObjectByType<PhysicsResetManager>();
            reset?.Capture();

            yield return WaitForDicePair();

            int total = diceAValue + diceBValue;

            CleanupDice();
            
            player.DisablePhysics();

            Vector3 forward = GetForwardFromCurrentSpace();

            state = TravelState.Recovering;
            yield return player.RecoverToBoardPose(forward);
            
            EnterTravelMode();

            bool done = false;

            state = TravelState.Traveling;

            player.BeginHopPath(this, activeSpaces, currentSpaceIndex, total, hopDuration, hopHeight, i =>
            {
                currentSpaceIndex = i;
                done = true;
            });

            while (!done)
                yield return null;

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
    }

    private void CleanupDice()
    {
        if (diceA) StartCoroutine(ShrinkAndDestroy(diceA.gameObject, 0.12f));
        if (diceB) StartCoroutine(ShrinkAndDestroy(diceB.gameObject, 0.12f));

        diceA = null;
        diceB = null;
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

        Vector3 spawnPos = activeSpaces[startIndex];
        Vector3 forward = activeSpaces[startIndex + 1] - spawnPos;
        
        Vector3 flatForward = forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude > 0.0001f)
        {
            cameraFollow.yaw = Quaternion.LookRotation(-flatForward.normalized, Vector3.up).eulerAngles.y;
        }
        
        EnterTravelMode(false);

        yield return player.SpawnHopFromTown(townPos, spawnPos, forward);

        player.EnablePhysics();
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

        CleanupDice();

        if (player != null)
            Destroy(player.gameObject);
        
        if (worldMapUI != null)
        {
            worldMapUI.SetCurrentTown(currentTownId, true, false);
            worldMapUI.OpenMap();
        }

        player = null;

        if (!cameraFocus) return;

        var townGO = townSystem.towns[currentTownId].go;
        if (!townGO) return;

        cameraFocus.SetParent(null);
        cameraFocus.position = townGO.transform.position;

        if (cameraFollow)
            cameraFollow.SetTarget(cameraFocus, true);
        
        if (worldMapUI != null)
        {
            worldMapUI.SetCurrentTown(currentTownId, true);
            worldMapUI.OpenMap();
        }

        Debug.Log($"Entered Town Mode at town {currentTownId}. Map reopened.");
    }

    private void EnterTravelMode(bool snap = false)
    {
        if (!player || !cameraFollow) return;

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
}