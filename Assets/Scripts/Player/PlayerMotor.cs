using System.Collections;
using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider col;

    [Header("Grounding")]
    [SerializeField] private float groundOffset = 0.12f;

    [Header("Rotation")]
    [SerializeField] private float facingSmooth = 14f;
    [SerializeField] private float slopeAlignSmooth = 12f;

    [Header("Recovery")]
    [SerializeField] private float settleDuration = 0.35f;
    
    [Header("Spawn Hop")]
    [SerializeField] private float spawnHopDuration = 1f;
    [SerializeField] private float spawnHopHeight = 15f;
    [SerializeField] private float spawnStartScale = 0.08f;

    public bool IsPhysicsDriven => rb != null && !rb.isKinematic;

    private Coroutine moveRoutine;

    public void Initialize(Terrain t)
    {
        terrain = t;

        if (!rb) rb = GetComponent<Rigidbody>();
        if (!col) col = GetComponent<Collider>();
    }

    public void EnablePhysics()
    {
        if (!rb) return;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void DisablePhysics()
    {
        if (!rb) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void StopMotion()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void WarpToGrounded(Vector3 worldPos, Vector3 desiredForward)
    {
        Vector3 grounded = GetGroundedPosition(worldPos);
        Quaternion rot = GetTargetRotation(grounded, desiredForward, true);

        if (rb && rb.isKinematic)
        {
            rb.position = grounded;
            rb.rotation = rot;
        }
        else
        {
            transform.position = grounded;
            transform.rotation = rot;
        }
    }

    public IEnumerator RecoverToBoardPose(Vector3 desiredForward)
    {
        DisablePhysics();

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 targetPos = GetGroundedPosition(startPos);
        Quaternion targetRot = GetTargetRotation(targetPos, desiredForward, true);

        float t = 0f;
        float dur = Mathf.Max(0.01f, settleDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = u * u * (3f - 2f * u);

            Vector3 pos = Vector3.Lerp(startPos, targetPos, s);
            Quaternion rot = Quaternion.Slerp(startRot, targetRot, s);

            if (rb)
            {
                rb.MovePosition(pos);
                rb.MoveRotation(rot);
            }
            else
            {
                transform.SetPositionAndRotation(pos, rot);
            }

            yield return null;
        }

        if (rb)
        {
            rb.position = targetPos;
            rb.rotation = targetRot;
        }
        else
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
        }
    }

    public void BeginHopPath(
        MonoBehaviour owner,
        System.Collections.Generic.List<Vector3> spaces,
        int startIndex,
        int hopCount,
        float hopDuration,
        float hopHeight,
        System.Action<int> onComplete,
        float startDelay = 0f)
    {
        StopMotion();
        moveRoutine = owner.StartCoroutine(
            HopPathRoutine(spaces, startIndex, hopCount, hopDuration, hopHeight, onComplete, startDelay));
    }

    private IEnumerator HopPathRoutine(
        System.Collections.Generic.List<Vector3> spaces,
        int startIndex,
        int hopCount,
        float hopDuration,
        float hopHeight,
        System.Action<int> onComplete,
        float startDelay = 0f)
    {
        DisablePhysics();
        
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (spaces == null || spaces.Count < 2)
        {
            onComplete?.Invoke(startIndex);
            moveRoutine = null;
            yield break;
        }

        int currentIndex = Mathf.Clamp(startIndex, 0, spaces.Count - 1);
        int targetIndex = Mathf.Clamp(currentIndex + hopCount, 0, spaces.Count - 1);

        for (int nextIndex = currentIndex + 1; nextIndex <= targetIndex; nextIndex++)
        {
            Vector3 start = transform.position;
            Vector3 endSpace = spaces[nextIndex];
            
            Vector3 lookTarget =
                nextIndex < spaces.Count - 1
                ? spaces[nextIndex + 1]
                : endSpace;

            float t = 0f;
            float dur = Mathf.Max(0.05f, hopDuration);

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float s = u * u * (3f - 2f * u);

                Vector3 xz = Vector3.Lerp(
                    new Vector3(start.x, 0f, start.z),
                    new Vector3(endSpace.x, 0f, endSpace.z),
                    s
                );

                Vector3 groundPos = GetGroundedPosition(new Vector3(xz.x, 0f, xz.z));
                float arc = Mathf.Sin(u * Mathf.PI) * hopHeight;
                Vector3 finalPos = groundPos + Vector3.up * arc;
                
                Vector3 desiredDir = lookTarget - finalPos;
                Quaternion targetRot = GetTargetRotation(finalPos, desiredDir, false);

                float rotT = 1f - Mathf.Exp(-Mathf.Max(facingSmooth, slopeAlignSmooth) * Time.deltaTime);
                Quaternion smoothRot = Quaternion.Slerp(transform.rotation, targetRot, rotT);

                if (rb)
                {
                    rb.MovePosition(finalPos);
                    rb.MoveRotation(smoothRot);
                }
                else
                {
                    transform.SetPositionAndRotation(finalPos, smoothRot);
                }

                yield return null;
            }
            
            Vector3 landed = GetGroundedPosition(endSpace);

            if (rb)
            {
                rb.position = landed;
            }
            else
            {
                transform.position = landed;
            }

            currentIndex = nextIndex;
        }

        moveRoutine = null;
        onComplete?.Invoke(currentIndex);
    }

    private Vector3 GetGroundedPosition(Vector3 worldPos)
    {
        if (!terrain) return worldPos;

        float y = terrain.SampleHeight(worldPos) + terrain.transform.position.y + groundOffset;
        return new Vector3(worldPos.x, y, worldPos.z);
    }

    private Quaternion GetTargetRotation(Vector3 worldPos, Vector3 desiredForward, bool forcePlanarFallback)
    {
        Vector3 normal = SampleTerrainNormal(worldPos);

        Vector3 forward = desiredForward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        forward = Vector3.ProjectOnPlane(forward, normal);

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = forcePlanarFallback
                ? Vector3.ProjectOnPlane(transform.forward, normal)
                : transform.forward;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        return Quaternion.LookRotation(forward, normal);
    }

    private Vector3 SampleTerrainNormal(Vector3 worldPos)
    {
        if (!terrain) return Vector3.up;

        TerrainData td = terrain.terrainData;
        Vector3 tp = terrain.transform.position;

        float nx = Mathf.InverseLerp(tp.x, tp.x + td.size.x, worldPos.x);
        float nz = Mathf.InverseLerp(tp.z, tp.z + td.size.z, worldPos.z);

        return td.GetInterpolatedNormal(nx, nz).normalized;
    }
    
    public IEnumerator SpawnHopFromTown(Vector3 fromTownPos, Vector3 toSpawnPos, Vector3 desiredForward)
    {
        DisablePhysics();

        Vector3 start = GetGroundedPosition(fromTownPos);
        Vector3 end = GetGroundedPosition(toSpawnPos);

        Quaternion startRot = GetTargetRotation(start, desiredForward, true);
        Quaternion endRot = GetTargetRotation(end, desiredForward, true);

        Vector3 tinyScale = Vector3.one * Mathf.Max(0.001f, spawnStartScale);
        Vector3 fullScale = Vector3.one;

        transform.position = start;
        transform.rotation = startRot;
        transform.localScale = tinyScale;

        float t = 0f;
        float dur = Mathf.Max(0.05f, spawnHopDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = u * u * (3f - 2f * u);

            Vector3 xz = Vector3.Lerp(
                new Vector3(start.x, 0f, start.z),
                new Vector3(end.x, 0f, end.z),
                s
            );

            Vector3 grounded = GetGroundedPosition(new Vector3(xz.x, 0f, xz.z));
            float arc = Mathf.Sin(u * Mathf.PI) * spawnHopHeight;
            Vector3 pos = grounded + Vector3.up * arc;

            Quaternion rot = Quaternion.Slerp(startRot, endRot, s);
            Vector3 scale = Vector3.Lerp(tinyScale, fullScale, s);

            if (rb)
            {
                rb.MovePosition(pos);
                rb.MoveRotation(rot);
            }
            else
            {
                transform.SetPositionAndRotation(pos, rot);
            }

            transform.localScale = scale;

            yield return null;
        }

        if (rb)
        {
            rb.position = end;
            rb.rotation = endRot;
        }
        else
        {
            transform.SetPositionAndRotation(end, endRot);
        }

        transform.localScale = fullScale;
    }
    
    public void BeginHopToPosition(
        PlayerTravelController controller,
        Vector3 targetPos,
        Vector3 forward,
        float duration,
        float height,
        System.Action onComplete)
    {
        StartCoroutine(HopToPositionRoutine(controller, targetPos, forward, duration, height, onComplete));
    }

    private IEnumerator HopToPositionRoutine(
        PlayerTravelController controller,
        Vector3 targetPos,
        Vector3 forward,
        float duration,
        float height,
        System.Action onComplete)
    {
        Vector3 start = transform.position;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float s = u * u * (3f - 2f * u);

            Vector3 basePos = Vector3.Lerp(start, targetPos, s);
            float arc = Mathf.Sin(u * Mathf.PI) * height;

            transform.position = basePos + Vector3.up * arc;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, s);

            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        onComplete?.Invoke();
    }
}