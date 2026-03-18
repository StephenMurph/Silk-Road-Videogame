using UnityEngine;

public class RubberCactus : MonoBehaviour
{
    [Header("Launch")]
    [SerializeField] private float launchHorizontalSpeed = 20f;
    [SerializeField] private float launchUpwardSpeed = 8f;
    [SerializeField] private float minImpactSpeed = 0.25f;

    [Header("Extra Kick")]
    [SerializeField] private float extraVelocityChange = 6f;
    [SerializeField] private float spinKick = 10f;

    [Header("Wobble")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private float wobbleStrengthMultiplier = 0.2f;
    [SerializeField] private float maxWobbleStrength = 1.5f;

    private static readonly int WobbleDirId = Shader.PropertyToID("_WobbleDirection");
    private static readonly int WobbleStrengthId = Shader.PropertyToID("_WobbleStrength");
    private static readonly int WobbleTimeId = Shader.PropertyToID("_WobbleStartTime");

    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        mpb = new MaterialPropertyBlock();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.rigidbody;
        if (rb == null)
            return;

        if (!collision.gameObject.CompareTag("Dice"))
            return;

        Vector3 incomingVelocity = rb.linearVelocity;
        float speed = incomingVelocity.magnitude;
        if (speed < minImpactSpeed)
            return;

        Vector3 away = rb.worldCenterOfMass - transform.position;
        away.y = 0f;

        if (away.sqrMagnitude < 0.001f && collision.contactCount > 0)
        {
            away = collision.GetContact(0).normal;
            away.y = 0f;
        }

        if (away.sqrMagnitude < 0.001f)
            away = transform.forward;

        away.Normalize();

        Vector3 launchVelocity =
            away * launchHorizontalSpeed +
            Vector3.up * launchUpwardSpeed;

        rb.linearVelocity = launchVelocity;
        rb.AddForce(away * extraVelocityChange, ForceMode.VelocityChange);
        rb.AddForce(Vector3.up * (extraVelocityChange * 0.35f), ForceMode.VelocityChange);

        rb.angularVelocity = Vector3.zero;
        rb.AddTorque(Random.onUnitSphere * spinKick, ForceMode.VelocityChange);

        TriggerWobble(-away, speed);
    }

    private void TriggerWobble(Vector3 wobbleDirWorld, float impactSpeed)
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            return;

        wobbleDirWorld.y = 0f;
        if (wobbleDirWorld.sqrMagnitude < 0.0001f)
            wobbleDirWorld = transform.forward;

        wobbleDirWorld.Normalize();

        Vector3 localDir = transform.InverseTransformDirection(wobbleDirWorld);

        float wobbleStrength = Mathf.Clamp(
            impactSpeed * wobbleStrengthMultiplier,
            0f,
            maxWobbleStrength
        );

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (!r) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetVector(WobbleDirId, new Vector4(localDir.x, localDir.y, localDir.z, 0f));
            mpb.SetFloat(WobbleStrengthId, wobbleStrength);
            mpb.SetFloat(WobbleTimeId, Time.time);
            r.SetPropertyBlock(mpb);
        }
    }
}