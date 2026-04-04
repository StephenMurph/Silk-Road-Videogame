using System.Collections;
using UnityEngine;

public class BanditEventActor : MonoBehaviour
{
    [SerializeField] private float fallHeight = 12f;
    [SerializeField] private float fallDuration = 0.5f;
    [SerializeField] private float landYOffset = 0f;

    public IEnumerator DropFromSky(Vector3 landPosition)
    {
        Vector3 start = landPosition + Vector3.up * fallHeight;
        Vector3 end = landPosition + Vector3.up * landYOffset;

        transform.position = start;

        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fallDuration);
            float eased = 1f - Mathf.Pow(1f - u, 3f);

            transform.position = Vector3.Lerp(start, end, eased);
            yield return null;
        }

        transform.position = end;
    }
}