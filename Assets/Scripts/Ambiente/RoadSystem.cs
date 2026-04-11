using System.Collections.Generic;
using UnityEngine;

public static class RoadSystem
{
    static readonly List<RoadPath> activeRoads = new List<RoadPath>();
    static bool defaultRoadInitialized;

    public static void Register(RoadPath road)
    {
        if (road == null || activeRoads.Contains(road))
            return;

        activeRoads.Add(road);
    }

    public static void Unregister(RoadPath road)
    {
        if (road == null)
            return;

        activeRoads.Remove(road);
    }

    public static bool IsReserved(Vector3 worldPoint, float extraMargin = 0f)
    {
        RoadMaskData roadMask = GetActiveRoadMask();
        if (roadMask != null && roadMask.IsRoad(worldPoint, extraMargin))
            return true;

        for (int i = activeRoads.Count - 1; i >= 0; i--)
        {
            RoadPath road = activeRoads[i];
            if (road == null)
            {
                activeRoads.RemoveAt(i);
                continue;
            }

            if (road.isActiveAndEnabled && road.ContainsPoint(worldPoint, extraMargin))
                return true;
        }

        return false;
    }

    public static float GetRoadBlend(Vector3 worldPoint)
    {
        float blend = 0f;

        RoadMaskData roadMask = GetActiveRoadMask();
        if (roadMask != null && roadMask.TrySampleBlend(worldPoint, out float maskBlend))
            blend = Mathf.Max(blend, maskBlend);

        for (int i = activeRoads.Count - 1; i >= 0; i--)
        {
            RoadPath road = activeRoads[i];
            if (road == null)
            {
                activeRoads.RemoveAt(i);
                continue;
            }

            if (road.isActiveAndEnabled && road.ContainsPoint(worldPoint))
                blend = 1f;
        }

        return Mathf.Clamp01(blend);
    }

    public static float ApplyFlatten(Vector3 worldPoint, float currentHeight)
    {
        float bestHeight = currentHeight;
        float bestDistance = float.MaxValue;

        for (int i = activeRoads.Count - 1; i >= 0; i--)
        {
            RoadPath road = activeRoads[i];
            if (road == null)
            {
                activeRoads.RemoveAt(i);
                continue;
            }

            if (!road.isActiveAndEnabled)
                continue;

            if (!road.TryGetFlattenedHeight(worldPoint, currentHeight, out float roadHeight, out float distance))
                continue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestHeight = roadHeight;
        }

        return bestHeight;
    }

    public static void EnsureDefaultRoadExists()
    {
        if (defaultRoadInitialized)
            return;

        defaultRoadInitialized = true;

        if (activeRoads.Count > 0 || Object.FindFirstObjectByType<RoadPath>() != null)
            return;

        GameObject root = new GameObject("Roads");
        GameObject roadObject = new GameObject("DefaultRoad_Main");
        roadObject.transform.SetParent(root.transform, false);

        RoadPath road = roadObject.AddComponent<RoadPath>();
        road.roadWidth = 10f;
        road.spawnClearMargin = 3f;
        road.flattenBlendWidth = 10f;
        road.visualSampleSpacing = 2.5f;
        road.useChildrenAsWaypoints = true;

        CreateWaypoint(roadObject.transform, "P0", new Vector3(-40f, 0f, 18f));
        CreateWaypoint(roadObject.transform, "P1", new Vector3(20f, 0f, 18f));
        CreateWaypoint(roadObject.transform, "P2", new Vector3(85f, 0f, 26f));
        CreateWaypoint(roadObject.transform, "P3", new Vector3(150f, 0f, 34f));
    }

    static void CreateWaypoint(Transform parent, string name, Vector3 position)
    {
        GameObject waypoint = new GameObject(name);
        waypoint.transform.SetParent(parent, false);
        waypoint.transform.position = position;
    }

    static RoadMaskData GetActiveRoadMask()
    {
        return SceneTerrainContext.GetActiveRoadMask();
    }
}
