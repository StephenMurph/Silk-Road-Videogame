using System.Collections;
using UnityEngine;

public class BanditEventActor : MonoBehaviour
{
    [SerializeField] private float fallHeight = 50f;
    [SerializeField] private float gravity = 40f;
    [SerializeField] private float groundClearance = 0.3f;
    [SerializeField] private float landPause = 0.1f;

    public IEnumerator DropFromSky(Vector3 landPosition)
    {
        float velocity = 0f;

        float bottomOffset = GetBottomOffsetFromPivot();
        Vector3 targetPos = landPosition + Vector3.up * (bottomOffset + groundClearance);
        Vector3 pos = targetPos + Vector3.up * fallHeight;

        while (pos.y > targetPos.y)
        {
            velocity += gravity * Time.deltaTime;
            pos.y -= velocity * Time.deltaTime;

            if (pos.y < targetPos.y)
                pos.y = targetPos.y;

            transform.position = pos;
            yield return null;
        }

        transform.position = targetPos;
        yield return new WaitForSeconds(landPause);
    }

    private float GetBottomOffsetFromPivot()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return 0.5f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        float bottomY = combined.min.y;
        return transform.position.y - bottomY;
    }
}