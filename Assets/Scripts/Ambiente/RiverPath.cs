using System.Collections.Generic;
using UnityEngine;

public class RiverPath : MonoBehaviour
{
    public int priority = 0;
    [Min(2f)] public float riverWidth = 8f;
    [Min(0.5f)] public float bankBlend = 5f;
    [Min(0.5f)] public float depth = 3.2f;
    [Range(0.2f, 1f)] public float waterInsetMultiplier = 0.72f;
    [Min(0.5f)] public float sampleSpacing = 3f;
    [Range(0f, 1f)] public float curveStrength = 1f;
    public bool useChildrenAsWaypoints = true;
    public Transform[] waypoints;

    int version;

    public int Version => version;

    void OnEnable()
    {
        RiverSystem.RegisterPath(this);
    }

    void OnDisable()
    {
        RiverSystem.UnregisterPath(this);
    }

    void OnValidate()
    {
        version++;
        RiverSystem.NotifyPathsChanged();
    }

    void OnTransformChildrenChanged()
    {
        version++;
        RiverSystem.NotifyPathsChanged();
    }

    public bool TryBuildCenterlineSamples(List<Vector3> centers)
    {
        centers.Clear();

        if (!TryGetWaypointCount(out int count) || count < 2)
            return false;

        float step = Mathf.Max(0.5f, sampleSpacing);
        List<Vector3> points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            if (TryGetWaypoint(i, out Vector3 waypoint))
                points.Add(waypoint);
        }

        if (points.Count < 2)
            return false;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            Vector3 p0 = i > 0 ? points[i - 1] : a;
            Vector3 p3 = i + 2 < points.Count ? points[i + 2] : b;

            float segmentLength = Vector3.Distance(a, b);
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(segmentLength / step));

            for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
            {
                if (i > 0 && sampleIndex == 0)
                    continue;

                float t = sampleCount <= 0 ? 0f : sampleIndex / (float)sampleCount;
                Vector3 linear = Vector3.Lerp(a, b, t);
                Vector3 curved = EvaluateCatmullRom(p0, a, b, p3, t);
                centers.Add(Vector3.Lerp(linear, curved, curveStrength));
            }
        }

        return centers.Count >= 2;
    }

    static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    bool TryGetWaypointCount(out int count)
    {
        if (useChildrenAsWaypoints)
        {
            count = transform.childCount;
            return count > 0;
        }

        count = waypoints != null ? waypoints.Length : 0;
        return count > 0;
    }

    bool TryGetWaypoint(int index, out Vector3 position)
    {
        if (useChildrenAsWaypoints)
        {
            if (index < 0 || index >= transform.childCount)
            {
                position = default;
                return false;
            }

            position = transform.GetChild(index).position;
            return true;
        }

        if (waypoints == null || index < 0 || index >= waypoints.Length || waypoints[index] == null)
        {
            position = default;
            return false;
        }

        position = waypoints[index].position;
        return true;
    }

    void OnDrawGizmos()
    {
        if (!TryGetWaypointCount(out int count) || count < 2)
            return;

        Gizmos.color = new Color(0.18f, 0.6f, 0.95f, 0.95f);

        for (int i = 0; i < count - 1; i++)
        {
            if (!TryGetWaypoint(i, out Vector3 a) || !TryGetWaypoint(i + 1, out Vector3 b))
                continue;

            Gizmos.DrawLine(a, b);

            Vector3 midpoint = (a + b) * 0.5f;
            Vector3 direction = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized * (riverWidth * 0.5f);

            Gizmos.DrawLine(midpoint - right, midpoint + right);
        }
    }
}
