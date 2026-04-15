using UnityEngine;

<<<<<<< HEAD
public class PortalSpawnPoint : MonoBehaviour
{
    public string spawnPointId = "Default";
    public Color gizmoColor = new Color(0.35f, 1f, 0.9f, 0.85f);
    public float gizmoRadius = 1.2f;

    public static PortalSpawnPoint FindById(string spawnPointId)
    {
        PortalSpawnPoint[] points = FindObjectsByType<PortalSpawnPoint>(FindObjectsSortMode.None);
        PortalSpawnPoint fallback = null;

        for (int i = 0; i < points.Length; i++)
        {
            PortalSpawnPoint point = points[i];
            if (point == null)
                continue;

            if (fallback == null)
                fallback = point;

            if (string.Equals(point.spawnPointId, spawnPointId, System.StringComparison.OrdinalIgnoreCase))
                return point;
        }

        return fallback;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
=======
[DisallowMultipleComponent]
public class PortalSpawnPoint : MonoBehaviour
{
    public string spawnPointId = "Default";

    public bool Matches(string requestedId)
    {
        if (string.IsNullOrWhiteSpace(requestedId))
            return string.IsNullOrWhiteSpace(spawnPointId) || spawnPointId == "Default";

        return string.Equals(spawnPointId, requestedId, System.StringComparison.OrdinalIgnoreCase);
>>>>>>> develop
    }
}
