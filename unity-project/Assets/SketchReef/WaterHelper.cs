using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class WaterQuery
{
    static WaterSearchParameters search;
    static WaterSearchResult result;

    public static bool TryGetWaterHeight(WaterSurface surface, Vector3 position, out float height)
    {
        height = 0f;
        if (surface == null) return false;

        search.startPositionWS = position;
        search.targetPositionWS = position;

        if (surface.ProjectPointOnWaterSurface(search, out result))
        {
            height = ExtractHeight(result);
            return true;
        }

        return false;
    }

    static float ExtractHeight(WaterSearchResult r)
    {
        var t = r.GetType();

        var h = t.GetProperty("height");
        if (h != null) return (float)h.GetValue(r);

        var p = t.GetProperty("projectedPositionWS");
        if (p != null) return ((Vector3)p.GetValue(r)).y;

        return 0f;
    }
}