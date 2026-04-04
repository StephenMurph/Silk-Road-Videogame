using UnityEngine;

public class RainController : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float fixedHeight = 20f;
    [SerializeField] private ParticleSystem rainParticles;

    private void Awake()
    {
        if (!rainParticles)
            rainParticles = GetComponentInChildren<ParticleSystem>(true);

        if (rainParticles != null)
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    public void StartRain()
    {
        if (rainParticles != null && !rainParticles.isPlaying)
            rainParticles.Play();
    }

    public void StopRain()
    {
        if (rainParticles != null)
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}