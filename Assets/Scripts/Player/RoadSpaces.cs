using System.Collections.Generic;
using UnityEngine;

public static class RoadSpaces
{
    private static Vector3 NZToWorldOnTerrain(Terrain terrain, Vector2 nz, float yOffset)
    {
        var data = terrain.terrainData;
        var tp = terrain.transform.position;

        float x = tp.x + nz.x * data.size.x;
        float z = tp.z + nz.y * data.size.z;

        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + tp.y + yOffset;
        return new Vector3(x, y, z);
    }

public static List<Vector3> BuildSpacesWorld(Terrain terrain, List<Vector2> pathNZ, float spacingWorld, float yOffset)
{
    var spaces = new List<Vector3>();
    if (!terrain || pathNZ == null || pathNZ.Count < 2) return spaces;

    spacingWorld = Mathf.Max(0.5f, spacingWorld);

    Vector3 prev = NZToWorldOnTerrain(terrain, pathNZ[0], yOffset);
    spaces.Add(prev);

    float distSinceLast = 0f;

    for (int i = 1; i < pathNZ.Count; i++)
    {
        Vector3 cur = NZToWorldOnTerrain(terrain, pathNZ[i], yOffset);

        Vector3 a = prev; a.y = 0f;
        Vector3 b = cur;  b.y = 0f;

        Vector3 ab = b - a;
        float segLen = ab.magnitude;
        if (segLen < 0.0001f)
        {
            prev = cur;
            continue;
        }

        Vector3 dir = ab / segLen;

        float remaining = segLen;

        while (distSinceLast + remaining >= spacingWorld)
        {
            float need = spacingWorld - distSinceLast;      // how far into this segment to place next point
            float t = need / remaining;                     // fraction along the remaining part

            // advance 'a' forward by 'need'
            a += dir * need;

            // ground it
            Vector3 p = new Vector3(a.x, 0f, a.z);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y + yOffset;

            spaces.Add(p);

            // we placed a point exactly spacingWorld away
            distSinceLast = 0f;

            // shorten what’s left of this segment
            remaining -= need;
        }

        // whatever segment length is left contributes to the next spacing
        distSinceLast += remaining;

        prev = cur;
    }

    return spaces;
}
}