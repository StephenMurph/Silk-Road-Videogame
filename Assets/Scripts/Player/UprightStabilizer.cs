using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class UprightStabilizer : MonoBehaviour
{
    public Rigidbody rb;

    [Header("Upright Spring")]
    public float uprightStrength = 80f;
    public float uprightDamping  = 12f;

    [Header("Dice Tag")]
    public string diceTag = "Dice";

    [Header("Collision Disable")]
    [Tooltip("Disable this long after a meaningful dice hit.")]
    public float diceDisableSeconds = 3f;

    [Tooltip("Ignore tiny contacts; only count as a 'hit' if impulse >= this.")]
    public float minDiceImpulse = 0.5f;

    [Header("Proximity Disable (pre-impact + during tumbling)")]
    [Tooltip("If a dice is within this distance AND moving fast enough, stabilizer is forced off.")]
    public float proximityRadius = 2.0f;

    [Tooltip("Dice must be moving at least this fast to count as 'active'.")]
    public float minDiceSpeed = 1.2f;

    [Tooltip("After dice slows/leaves radius, keep stabilizer off for this extra time (prevents flicker).")]
    public float proximityGraceSeconds = 0.35f;

    [Header("Fade Back In")]
    public float fadeInDuration = 0.6f;

    [Header("Assist When Upside Down")]
    public float maxAssistAngle = 80f;

    [Header("Debug")]
    [Range(0f, 1f)] public float stabilizerWeight = 1f;

    float disabledUntil = -999f;
    float fadeStartTime = -999f;
    float proximityOffUntil = -999f;

    Rigidbody[] diceRBs;

    void Reset() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        RefreshDiceCache();
    }
    
    float nextRefresh;
    void RefreshDiceCache()
    {
        var diceGOs = GameObject.FindGameObjectsWithTag(diceTag);
        diceRBs = new Rigidbody[diceGOs.Length];
        for (int i = 0; i < diceGOs.Length; i++)
            diceRBs[i] = diceGOs[i].GetComponentInChildren<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!rb) return;
        
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + 0.5f;
            RefreshDiceCache();
        }
        
        if (IsAnyActiveDiceNearby())
        {
            proximityOffUntil = Time.time + proximityGraceSeconds;
        }

        UpdateWeight();

        if (stabilizerWeight <= 0f) return;
        
        Vector3 currentUp = transform.up;
        Vector3 targetUp  = Vector3.up;

        Vector3 axis = Vector3.Cross(currentUp, targetUp);
        float axisMag = axis.magnitude;
        if (axisMag < 1e-5f) return;

        axis /= axisMag;

        float angle = Mathf.Asin(Mathf.Clamp(axisMag, -1f, 1f)) * Mathf.Rad2Deg;

        float assist = 1f;
        if (angle > maxAssistAngle)
            assist = Mathf.Lerp(1f, 2.5f, Mathf.InverseLerp(maxAssistAngle, 90f, angle));

        Vector3 springTorque = axis * (angle * Mathf.Deg2Rad) * uprightStrength * assist;
        Vector3 dampTorque   = -rb.angularVelocity * uprightDamping;

        rb.AddTorque((springTorque + dampTorque) * stabilizerWeight, ForceMode.Acceleration);
    }

    bool IsAnyActiveDiceNearby()
    {
        if (diceRBs == null || diceRBs.Length == 0) return false;

        float r2 = proximityRadius * proximityRadius;
        Vector3 p = rb.worldCenterOfMass;

        for (int i = 0; i < diceRBs.Length; i++)
        {
            var d = diceRBs[i];
            if (!d) continue;
            
            Vector3 dp = d.worldCenterOfMass - p;
            if (dp.sqrMagnitude > r2) continue;
            
            if (d.linearVelocity.magnitude >= minDiceSpeed)
                return true;
        }

        return false;
    }

    void UpdateWeight()
    {
        float t = Time.time;
        
        if (t < proximityOffUntil)
        {
            stabilizerWeight = 0f;
            return;
        }
        
        if (t < disabledUntil)
        {
            stabilizerWeight = 0f;
            return;
        }
        
        if (fadeInDuration <= 0.0001f)
        {
            stabilizerWeight = 1f;
            return;
        }

        float u = Mathf.Clamp01((t - fadeStartTime) / fadeInDuration);
        stabilizerWeight = u;
    }

    void OnCollisionEnter(Collision collision) => HandleCollision(collision);
    void OnCollisionStay(Collision collision)  => HandleCollision(collision);

    void HandleCollision(Collision collision)
    {
        Transform other = collision.collider.transform;
        bool isDice = other.CompareTag(diceTag) || (other.root != null && other.root.CompareTag(diceTag));
        if (!isDice) return;

        float impulseMag = collision.impulse.magnitude;
        if (impulseMag < minDiceImpulse) return;

        disabledUntil = Time.time + diceDisableSeconds;
        fadeStartTime = disabledUntil;
        
        stabilizerWeight = 0f;
    }
}