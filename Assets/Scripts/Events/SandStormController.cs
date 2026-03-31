using UnityEngine;

public class SandstormController : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float fixedHeight = 8f;
    [SerializeField] private ParticleSystem sandstormParticles;

    private void Awake()
    {
        if (!sandstormParticles)
            sandstormParticles = GetComponentInChildren<ParticleSystem>(true);

        if (sandstormParticles != null)
            sandstormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LateUpdate()
    {
        if (!followTarget)
            return;

        Vector3 p = followTarget.position;
        transform.position = new Vector3(p.x, fixedHeight, p.z);
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    public void StartSandstorm()
    {
        if (sandstormParticles != null && !sandstormParticles.isPlaying)
            sandstormParticles.Play();
    }

    public void StopSandstorm()
    {
        if (sandstormParticles != null)
            sandstormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}