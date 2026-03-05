using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsResetManager : MonoBehaviour
{
    [Header("What to record")]
    public List<Rigidbody> targets = new();
    public bool autoFindIfTargetsEmpty = true;

    private struct Snapshot
    {
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 vel;
        public Vector3 angVel;
        public bool wasKinematic;
        public bool useGravity;
        public RigidbodyConstraints constraints;
    }

    private readonly Dictionary<Rigidbody, Snapshot> snap = new();
    private bool hasSnapshot;

    private Coroutine restoreRoutine;

    public void Capture()
    {
        snap.Clear();

        List<Rigidbody> list;
        if (targets != null && targets.Count > 0) list = targets;
        else if (autoFindIfTargetsEmpty) list = new List<Rigidbody>(FindObjectsByType<Rigidbody>(FindObjectsSortMode.None));
        else list = new List<Rigidbody>();

        for (int i = 0; i < list.Count; i++)
        {
            var rb = list[i];
            if (!rb) continue;

            snap[rb] = new Snapshot
            {
                pos = rb.transform.position,
                rot = rb.transform.rotation,
                vel = rb.linearVelocity,
                angVel = rb.angularVelocity,
                wasKinematic = rb.isKinematic,
                useGravity = rb.useGravity,
                constraints = rb.constraints
            };
        }

        hasSnapshot = true;
        Debug.Log($"[PhysicsResetManager] Captured {snap.Count} rigidbodies.");
    }

    // --- Instant snap restore (keep this)
    public void RestoreInstant()
    {
        if (!hasSnapshot)
        {
            Debug.LogWarning("[PhysicsResetManager] No snapshot to restore.");
            return;
        }

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        foreach (var kv in snap)
        {
            Rigidbody rb = kv.Key;
            if (!rb) continue;

            Snapshot s = kv.Value;

            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.transform.position = s.pos;
            rb.transform.rotation = s.rot;

            rb.useGravity = s.useGravity;
            rb.constraints = s.constraints;
            rb.isKinematic = s.wasKinematic;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = s.vel;
                rb.angularVelocity = s.angVel;
            }

            rb.Sleep();
        }

        Debug.Log($"[PhysicsResetManager] Restored INSTANT {snap.Count} rigidbodies.");
    }

    // --- Animated board-game restore
    public void RestoreAnimated(float duration = 0.25f, AnimationCurve ease = null)
    {
        if (!hasSnapshot)
        {
            Debug.LogWarning("[PhysicsResetManager] No snapshot to restore.");
            return;
        }

        if (restoreRoutine != null)
            StopCoroutine(restoreRoutine);

        restoreRoutine = StartCoroutine(RestoreAnimatedRoutine(duration, ease));
    }

    private IEnumerator RestoreAnimatedRoutine(float duration, AnimationCurve ease)
    {
        duration = Mathf.Max(0.01f, duration);
        if (ease == null) ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Cache start state + force kinematic during the animation
        var starts = new Dictionary<Rigidbody, (Vector3 pos, Quaternion rot)>(snap.Count);

        foreach (var kv in snap)
        {
            var rb = kv.Key;
            if (!rb) continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            starts[rb] = (rb.transform.position, rb.transform.rotation);

            // freeze physics while we animate back
            rb.isKinematic = true;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float e = Mathf.Clamp01(ease.Evaluate(u));

            foreach (var kv in snap)
            {
                var rb = kv.Key;
                if (!rb) continue;

                var s = kv.Value;
                var start = starts[rb];

                rb.transform.position = Vector3.Lerp(start.pos, s.pos, e);
                rb.transform.rotation = Quaternion.Slerp(start.rot, s.rot, e);
            }

            yield return null;
        }

        // Finalize + restore original flags
        foreach (var kv in snap)
        {
            var rb = kv.Key;
            if (!rb) continue;

            var s = kv.Value;

            rb.transform.position = s.pos;
            rb.transform.rotation = s.rot;

            rb.useGravity = s.useGravity;
            rb.constraints = s.constraints;
            rb.isKinematic = s.wasKinematic;

            // Usually for a board reset, you want everything settled:
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        Debug.Log($"[PhysicsResetManager] Restored ANIMATED {snap.Count} rigidbodies.");
        restoreRoutine = null;
    }

    public void Register(Rigidbody rb)
    {
        if (!rb) return;
        if (!targets.Contains(rb))
            targets.Add(rb);
    }
}