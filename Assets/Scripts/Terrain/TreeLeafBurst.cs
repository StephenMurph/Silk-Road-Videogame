using UnityEngine;

public class TreeLeafBurst : MonoBehaviour
{
    [Header("Particles")]
    [SerializeField] private ParticleSystem leafBurstPrefab;
    [SerializeField] private Transform leafBurstPoint;

    [Header("Impact")]
    [SerializeField] private float minImpactSpeed = 2f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Dice"))
            return;

        if (collision.relativeVelocity.magnitude < minImpactSpeed)
            return;

        if (!leafBurstPrefab)
            return;

        Vector3 spawnPos = leafBurstPoint ? leafBurstPoint.position : transform.position + Vector3.up * 3f;

        ParticleSystem ps = Instantiate(leafBurstPrefab, spawnPos, Quaternion.identity);
        ps.Play();
        Destroy(ps.gameObject, 4f);
    }
}