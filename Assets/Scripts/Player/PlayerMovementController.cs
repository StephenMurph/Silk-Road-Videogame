using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    [Header("Refs")]
    public Terrain terrain;
    public TerrainTownRoadSystem townSystem;
    public WorldMapUI worldMapUI;

    [Header("Pawn")]
    public GameObject pawnPrefab;
    public Transform pawnInstance;

    [Header("Spawn Tuning")]
    public float minDistanceFromTownWorld = 12f;
    public float maxSearchDistanceWorld = 200f;
    public float searchStepWorld = 2f;
    [Range(0f, 1f)] public float roadThreshold01 = 0.20f;
    public float pawnYOffset = 0.2f;

    [Header("Camera")]
    public CameraFollowPlayer cameraFollow;

    [Header("Dice")]
    public GameObject dicePrefab;
    private DiceController activeDice;

    [Header("Hop Spaces")]
    [Tooltip("Distance between hop spaces along the road (world meters).")]
    public float hopSpacingWorld = 8f;

    [Tooltip("Seconds per hop (board-game snappy).")]
    public float hopDuration = 0.18f;

    [Tooltip("How high the hop arc goes (meters).")]
    public float hopHeight = 1.2f;

    [Header("Debug")]
    public bool drawSpaces = true;
    public float gizmoSphereRadius = 0.6f;

    private Rigidbody pawnRb;
    private Collider pawnCol;

    public int currentTownId;
    public int nextTownId = -1;

    // current active road spaces for the chosen edge
    private List<Vector3> activeSpaces = new();
    private int currentSpaceIndex = 0;
    private Coroutine hopRoutine;
    
    private Transform cameraFocus;

    void Awake()
    {
        if (!terrain) terrain = FindFirstObjectByType<Terrain>();
        if (!townSystem) townSystem = FindFirstObjectByType<TerrainTownRoadSystem>();
        if (!worldMapUI) worldMapUI = FindFirstObjectByType<WorldMapUI>();
        if (!cameraFollow) cameraFollow = FindFirstObjectByType<CameraFollowPlayer>();

        if (cameraFocus == null)
        {
            cameraFocus = new GameObject("CameraFocus").transform;
            cameraFocus.position = Vector3.zero;
        }

        if (cameraFollow)
            cameraFollow.SetTarget(cameraFocus, snap: true);
    }

    void Start()
    {
        if (townSystem != null && townSystem.towns.Count == 0)
            townSystem.GenerateTownsAndRoads();

        if (townSystem == null || townSystem.towns.Count == 0)
        {
            Debug.LogError("PlayerMovementController: No towns found.");
            return;
        }

        if (worldMapUI != null)
            currentTownId = Mathf.Clamp(worldMapUI.currentTownId, 0, townSystem.towns.Count - 1);
        else
            currentTownId = 0;

        EnterTownMode();
    }

    private void OnTownSelectedFromMap(int newTownId)
    {
        nextTownId = Mathf.Clamp(newTownId, 0, townSystem.towns.Count - 1);

        // 1) Build hop spaces FIRST (must succeed)
        if (!BuildSpacesForEdge(currentTownId, nextTownId))
        {
            Debug.LogWarning($"No hop spaces for edge {currentTownId}->{nextTownId}. Did roads generate this session?");
            return;
        }

        // 2) Spawn pawn exactly on the first “after town” space
        SpawnOrMovePawnOnEdgeStart(currentTownId, nextTownId);

        // 3) Camera follow
        EnterTravelMode();

        // 4) Dice
        SpawnDice();
    }

    private void SpawnDice()
    {
        if (!dicePrefab)
        {
            Debug.LogError("PlayerMovementController: dicePrefab not assigned.");
            return;
        }

        if (activeDice != null)
            Destroy(activeDice.gameObject);

        var go = Instantiate(dicePrefab, Vector3.zero, Quaternion.identity);

        activeDice = go.GetComponent<DiceController>();
        if (!activeDice)
        {
            Debug.LogError("dicePrefab missing DiceController.");
            Destroy(go);
            return;
        }

        // Hook result callback
        activeDice.OnRolled += OnDiceRolled;
    }

    private void OnDiceRolled(int result)
    {
        // Stop any current hop
        if (hopRoutine != null)
            StopCoroutine(hopRoutine);

        hopRoutine = StartCoroutine(HopSpaces(result));
    }

    private IEnumerator HopSpaces(int hops)
    {
        if (pawnInstance == null || activeSpaces == null || activeSpaces.Count < 2)
        {
            Debug.LogWarning("HopSpaces: No spaces to hop on.");
            yield break;
        }

        hops = Mathf.Max(0, hops);

        int targetIndex = Mathf.Clamp(currentSpaceIndex + hops, 0, activeSpaces.Count - 1);

        for (int i = currentSpaceIndex + 1; i <= targetIndex; i++)
        {
            Vector3 from = pawnInstance.position;
            Vector3 to = activeSpaces[i];

            // Face direction of travel (flat)
            Vector3 dir = (to - from);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                pawnInstance.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // Animate hop
            float t = 0f;
            float dur = Mathf.Max(0.05f, hopDuration);

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                // smoothstep
                float s = u * u * (3f - 2f * u);

                Vector3 p = Vector3.Lerp(from, to, s);

                // arc
                float arc = Mathf.Sin(u * Mathf.PI) * hopHeight;
                p.y += arc;

                // If you have a Rigidbody, use MovePosition for nicer physics sync
                if (pawnRb != null && !pawnRb.isKinematic)
                    pawnRb.MovePosition(p);
                else
                    pawnInstance.position = p;

                yield return null;
            }

            // snap exactly to the space (grounded)
            if (pawnRb != null && !pawnRb.isKinematic)
            {
                pawnRb.position = to;
                pawnRb.linearVelocity = Vector3.zero;
                pawnRb.angularVelocity = Vector3.zero;
            }
            else
            {
                pawnInstance.position = to;
            }

            currentSpaceIndex = i;
        }

        // If we reached the end, we can consider ourselves arrived:
        if (currentSpaceIndex >= activeSpaces.Count - 1)
        {
            currentTownId = nextTownId;
            Debug.Log($"Arrived at town {currentTownId}");
        }

        hopRoutine = null;
    }

    private bool BuildSpacesForEdge(int fromTownId, int toTownId)
    {
        activeSpaces.Clear();
        currentSpaceIndex = 0;

        if (fromTownId < 0 || toTownId < 0) return false;
        if (fromTownId >= townSystem.towns.Count || toTownId >= townSystem.towns.Count) return false;

        Vector2 aNZ = townSystem.towns[fromTownId].nz;
        Vector2 bNZ = townSystem.towns[toTownId].nz;

        if (!TerrainRoadGenerator.TryGetRoadPathNZ(aNZ, bNZ, out var pathNZ))
        {
            Debug.LogWarning("No cached road path found for this edge. (Make sure roads were generated this session.)");
            return false;
        }

        activeSpaces = RoadSpaces.BuildSpacesWorld(terrain, pathNZ, hopSpacingWorld, pawnYOffset);

        if (activeSpaces == null || activeSpaces.Count < 2)
            return false;

        // IMPORTANT: we do NOT pick "nearest to pawn" here anymore.
        // The pawn will be placed based on the town end of the edge.
        currentSpaceIndex = 0;

        Debug.Log($"Built {activeSpaces.Count} hop spaces for edge {fromTownId}->{toTownId}.");
        return true;
    }

    private int PickDefaultNextTown(int fromTown)
    {
        if (townSystem.adjacency != null &&
            townSystem.adjacency.TryGetValue(fromTown, out List<int> neigh) &&
            neigh != null && neigh.Count > 0)
            return neigh[0];

        return -1;
    }

    /*// --- Your existing spawn logic (kept) ---
    public void SpawnOrMovePawnInFrontOfTown(int fromTownId, int toTownId)
    {
        if (!terrain || !townSystem) return;

        if (pawnInstance == null)
        {
            if (!pawnPrefab)
            {
                Debug.LogError("PlayerMovementController: Assign pawnPrefab.");
                return;
            }

            var go = Instantiate(pawnPrefab);
            pawnInstance = go.transform;

            pawnRb = pawnInstance.GetComponent<Rigidbody>();
            pawnCol = pawnInstance.GetComponent<Collider>();

            var reset = FindFirstObjectByType<PhysicsResetManager>();
            if (reset && pawnRb)
                reset.Register(pawnRb);
        }

        if (TryFindRoadSpawn(fromTownId, toTownId, out var spawnPos, out var forwardDir))
        {
            pawnInstance.position = spawnPos;
            PlacePawnOnTerrainPhysics(spawnPos);
            FaceDirectionFlat(forwardDir);
        }
    }*/

    private bool TryFindRoadSpawn(int fromTownId, int toTownId, out Vector3 spawnWorld, out Vector3 forwardDir)
    {
        spawnWorld = default;
        forwardDir = Vector3.forward;

        if (fromTownId < 0 || fromTownId >= townSystem.towns.Count) return false;

        Vector3 fromWorld = TownWorld(fromTownId);

        Vector3 dir = (toTownId >= 0 && toTownId < townSystem.towns.Count)
            ? (TownWorld(toTownId) - fromWorld)
            : Vector3.forward;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();
        forwardDir = dir;

        float startD = Mathf.Max(0f, minDistanceFromTownWorld);
        float endD = Mathf.Max(startD + 1f, maxSearchDistanceWorld);

        for (float d = startD; d <= endD; d += Mathf.Max(0.1f, searchStepWorld))
        {
            Vector3 probe = fromWorld + dir * d;
            Vector2 nz = WorldToNZ(probe);

            float road = TerrainRoadGenerator.SampleRoadMask01(terrain.terrainData, nz.x, nz.y);
            if (road >= roadThreshold01)
            {
                float y = terrain.SampleHeight(probe) + terrain.transform.position.y + pawnYOffset;
                spawnWorld = new Vector3(probe.x, y, probe.z);
                forwardDir = dir;
                return true;
            }
        }

        // fallback
        Vector3 fb = fromWorld + dir * minDistanceFromTownWorld;
        float fy = terrain.SampleHeight(fb) + terrain.transform.position.y + pawnYOffset;
        spawnWorld = new Vector3(fb.x, fy, fb.z);
        forwardDir = dir;
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

    private Vector2 WorldToNZ(Vector3 world)
    {
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;

        float nx = (world.x - tp.x) / data.size.x;
        float nz = (world.z - tp.z) / data.size.z;

        return new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(nz));
    }

    private void PlacePawnOnTerrainPhysics(Vector3 desiredXZWorld)
    {
        if (!pawnInstance || !terrain) return;

        float groundY = terrain.SampleHeight(desiredXZWorld) + terrain.transform.position.y;

        float lift = 1.0f;
        if (pawnCol != null)
            lift = Mathf.Max(0.2f, pawnCol.bounds.extents.y + 0.25f);

        Vector3 startPos = new Vector3(desiredXZWorld.x, groundY + lift, desiredXZWorld.z);

        if (pawnRb != null && !pawnRb.isKinematic)
        {
            pawnRb.linearVelocity = Vector3.zero;
            pawnRb.angularVelocity = Vector3.zero;
            pawnRb.position = startPos;
            pawnRb.Sleep();
            pawnRb.WakeUp();
        }
        else
        {
            pawnInstance.position = startPos;
        }
    }

    private void FaceDirectionFlat(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (pawnRb != null && !pawnRb.isKinematic)
            pawnRb.MoveRotation(rot);
        else
            pawnInstance.rotation = rot;
    }

    // --- Gizmos to see the hop spaces ---
    void OnDrawGizmosSelected()
    {
        if (!drawSpaces || activeSpaces == null || activeSpaces.Count == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < activeSpaces.Count; i++)
        {
            Gizmos.DrawSphere(activeSpaces[i], gizmoSphereRadius);
            if (i > 0)
                Gizmos.DrawLine(activeSpaces[i - 1], activeSpaces[i]);
        }

        // Current index marker
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(activeSpaces[Mathf.Clamp(currentSpaceIndex, 0, activeSpaces.Count - 1)], gizmoSphereRadius * 1.25f);
    }
    
    private void DespawnPawnIfExists()
    {
        if (pawnInstance)
        {
            Destroy(pawnInstance.gameObject);
            pawnInstance = null;
            pawnRb = null;
            pawnCol = null;
        }
    }

    private void FocusCameraOnTown(int townId, bool snap)
    {
        if (!cameraFollow) return;
        if (townSystem == null) return;
        if (townId < 0 || townId >= townSystem.towns.Count) return;

        var go = townSystem.towns[townId].go;
        if (!go) return;

        cameraFollow.SetTarget(go.transform, snap);
    }
    
    public void EnterTownMode()
    {
        // pawn should not exist in town mode (as you want)
        DespawnPawnIfExists();

        if (!cameraFocus || !townSystem) return;
        if (currentTownId < 0 || currentTownId >= townSystem.towns.Count) return;

        var townGO = townSystem.towns[currentTownId].go;
        if (!townGO) return;

        // unparent and snap focus to town
        cameraFocus.SetParent(null);
        cameraFocus.position = townGO.transform.position;

        if (cameraFollow)
            cameraFollow.SetTarget(cameraFocus, snap: true);

        Debug.Log($"[Camera] TownMode focus townId={currentTownId} pos={cameraFocus.position}");
    }

    public void EnterTravelMode()
    {
        if (!pawnInstance || !cameraFocus) return;

        // parent focus to pawn so it follows perfectly
        cameraFocus.SetParent(pawnInstance);
        cameraFocus.localPosition = Vector3.zero;

        if (cameraFollow)
            cameraFollow.SetTarget(cameraFocus, snap: true);

        Debug.Log($"[Camera] TravelMode focus pawn pos={pawnInstance.position}");
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
    
    private void OnCurrentTownChangedFromMap(int id)
    {
        currentTownId = Mathf.Clamp(id, 0, townSystem.towns.Count - 1);
        EnterTownMode();
    }
    
    private void SpawnOrMovePawnOnEdgeStart(int fromTownId, int toTownId)
    {
        if (!terrain || !townSystem) return;
        if (activeSpaces == null || activeSpaces.Count < 2)
        {
            Debug.LogWarning("SpawnOrMovePawnOnEdgeStart: activeSpaces missing.");
            return;
        }

        // Ensure pawn exists
        if (pawnInstance == null)
        {
            if (!pawnPrefab)
            {
                Debug.LogError("PlayerMovementController: Assign pawnPrefab.");
                return;
            }

            var go = Instantiate(pawnPrefab);
            pawnInstance = go.transform;

            pawnRb = pawnInstance.GetComponent<Rigidbody>();
            pawnCol = pawnInstance.GetComponent<Collider>();

            var reset = FindFirstObjectByType<PhysicsResetManager>();
            if (reset && pawnRb)
                reset.Register(pawnRb);
        }

        // Pick a start space: "first space after town"
        // Option A (recommended): pick the first space at least minDistanceFromTownWorld away from the town
        Vector3 townPos = TownWorld(fromTownId);

        int startIndex = 0;
        float minDist2 = minDistanceFromTownWorld * minDistanceFromTownWorld;

        // start from 0/1 depending on whether you want to allow space 0 to be "inside town"
        for (int i = 0; i < activeSpaces.Count; i++)
        {
            Vector3 p = activeSpaces[i];
            Vector3 flatTown = new Vector3(townPos.x, 0f, townPos.z);
            Vector3 flatP = new Vector3(p.x, 0f, p.z);

            if ((flatP - flatTown).sqrMagnitude >= minDist2)
            {
                startIndex = i;
                break;
            }
        }

        // Safety: never allow startIndex to be the very last node
        startIndex = Mathf.Clamp(startIndex, 0, activeSpaces.Count - 2);

        Vector3 spawnPos = activeSpaces[startIndex];

        // Place pawn (physics-safe)
        if (pawnRb != null && !pawnRb.isKinematic)
        {
            pawnRb.linearVelocity = Vector3.zero;
            pawnRb.angularVelocity = Vector3.zero;
            pawnRb.position = spawnPos;
            pawnRb.Sleep();
            pawnRb.WakeUp();
        }
        else
        {
            pawnInstance.position = spawnPos;
        }

        // Set board index to match exactly
        currentSpaceIndex = startIndex;

        // Face along the road (toward next space)
        Vector3 lookTo = activeSpaces[startIndex + 1];
        Vector3 dir = lookTo - spawnPos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            FaceDirectionFlat(dir);

        Debug.Log($"Spawned pawn on edge startIndex={startIndex} for {fromTownId}->{toTownId} pos={spawnPos}");
    }
}