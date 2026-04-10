using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RiverBlocker : MonoBehaviour
{
    static readonly List<RiverBlocker> activeBlockers = new List<RiverBlocker>();

    public bool useAttachedColliderBounds = true;
    [Range(0f, 1f)] public float cutoffHeightNormalized = 0.3f;
    public float xzPadding = 0.35f;
    public float waterSurfaceClearance = 0.02f;
    public Vector3 center = new Vector3(0f, 1f, 0f);
    public Vector3 size = new Vector3(8f, 4f, 8f);
    public bool allowWaterBelowCutoff = true;
    public float blockWaterAboveLocalY = 0.6f;

    public static bool Contains(Vector3 worldPoint)
    {
        for (int i = activeBlockers.Count - 1; i >= 0; i--)
        {
            RiverBlocker blocker = activeBlockers[i];
            if (blocker == null)
            {
                activeBlockers.RemoveAt(i);
                continue;
            }

            if (blocker.enabled && blocker.gameObject.activeInHierarchy && blocker.ContainsPoint(worldPoint))
                return true;
        }

        return false;
    }

    public static bool IsBlockedCollider(Collider collider)
    {
        if (collider == null)
            return false;

        return collider.GetComponentInParent<RiverBlocker>() != null;
    }

    public static bool TryFilterWaterSurface(Vector3 worldPoint, ref float waterSurfaceY)
    {
        bool shouldHideSurface = false;

        for (int i = activeBlockers.Count - 1; i >= 0; i--)
        {
            RiverBlocker blocker = activeBlockers[i];
            if (blocker == null)
            {
                activeBlockers.RemoveAt(i);
                continue;
            }

            if (!blocker.enabled || !blocker.gameObject.activeInHierarchy)
                continue;

            blocker.ApplyWaterConstraint(worldPoint, ref waterSurfaceY, ref shouldHideSurface);
        }

        return shouldHideSurface;
    }

    public static void ApplyOverheadColliderConstraint(Vector3 worldPoint, ref float waterSurfaceY)
    {
        Vector3 origin = new Vector3(worldPoint.x, waterSurfaceY - 0.05f, worldPoint.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.up, 25f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            RiverBlocker blocker = hit.collider != null ? hit.collider.GetComponentInParent<RiverBlocker>() : null;
            if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy)
                continue;

            waterSurfaceY = Mathf.Min(waterSurfaceY, hit.point.y - Mathf.Abs(blocker.waterSurfaceClearance));
            return;
        }
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        if (TryContainsUsingColliderBounds(worldPoint, out bool isBlocked))
            return isBlocked;

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint) - center;
        Vector3 halfExtents = size * 0.5f;

        if (Mathf.Abs(localPoint.x) > halfExtents.x || Mathf.Abs(localPoint.z) > halfExtents.z)
            return false;

        if (allowWaterBelowCutoff && localPoint.y < blockWaterAboveLocalY)
            return false;

        return Mathf.Abs(localPoint.y) <= halfExtents.y || (allowWaterBelowCutoff && localPoint.y >= blockWaterAboveLocalY);
    }

    bool TryContainsUsingColliderBounds(Vector3 worldPoint, out bool isBlocked)
    {
        isBlocked = false;

        if (!useAttachedColliderBounds)
            return false;

        if (!TryGetExpandedColliderBounds(out Bounds bounds))
            return false;

        if (worldPoint.x < bounds.min.x || worldPoint.x > bounds.max.x ||
            worldPoint.z < bounds.min.z || worldPoint.z > bounds.max.z)
        {
            isBlocked = false;
            return true;
        }

        float cutoffWorldY = Mathf.Lerp(bounds.min.y, bounds.max.y, cutoffHeightNormalized);
        if (allowWaterBelowCutoff && worldPoint.y < cutoffWorldY)
        {
            isBlocked = false;
            return true;
        }

        isBlocked = true;
        return true;
    }

    void ApplyWaterConstraint(Vector3 worldPoint, ref float waterSurfaceY, ref bool shouldHideSurface)
    {
        if (useAttachedColliderBounds && TryGetExpandedColliderBounds(out Bounds colliderBounds))
        {
            if (worldPoint.x < colliderBounds.min.x || worldPoint.x > colliderBounds.max.x ||
                worldPoint.z < colliderBounds.min.z || worldPoint.z > colliderBounds.max.z)
                return;

            float cutoffWorldY = Mathf.Lerp(colliderBounds.min.y, colliderBounds.max.y, cutoffHeightNormalized);

            if (allowWaterBelowCutoff)
            {
                waterSurfaceY = Mathf.Min(waterSurfaceY, cutoffWorldY - Mathf.Abs(waterSurfaceClearance));
                return;
            }

            if (waterSurfaceY >= cutoffWorldY)
                shouldHideSurface = true;

            return;
        }

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint) - center;
        Vector3 halfExtents = size * 0.5f;

        if (Mathf.Abs(localPoint.x) > halfExtents.x || Mathf.Abs(localPoint.z) > halfExtents.z)
            return;

        if (allowWaterBelowCutoff)
        {
            waterSurfaceY = Mathf.Min(waterSurfaceY, transform.TransformPoint(center + Vector3.up * blockWaterAboveLocalY).y - Mathf.Abs(waterSurfaceClearance));
            return;
        }

        if (localPoint.y >= blockWaterAboveLocalY)
            shouldHideSurface = true;
    }

    void OnEnable()
    {
        if (!activeBlockers.Contains(this))
            activeBlockers.Add(this);
    }

    void OnDisable()
    {
        activeBlockers.Remove(this);
    }

    void OnDrawGizmosSelected()
    {
        if (useAttachedColliderBounds && TryGetExpandedColliderBounds(out Bounds colliderBounds))
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawCube(colliderBounds.center, colliderBounds.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(colliderBounds.center, colliderBounds.size);

            if (allowWaterBelowCutoff)
            {
                float cutoffWorldY = Mathf.Lerp(colliderBounds.min.y, colliderBounds.max.y, cutoffHeightNormalized);
                Vector3 planeCenter = new Vector3(colliderBounds.center.x, cutoffWorldY, colliderBounds.center.z);
                Vector3 planeSize = new Vector3(colliderBounds.size.x, 0.02f, colliderBounds.size.z);
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                Gizmos.DrawWireCube(planeCenter, planeSize);
            }

            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(center, size);

        if (allowWaterBelowCutoff)
        {
            Vector3 planeCenter = center + new Vector3(0f, blockWaterAboveLocalY, 0f);
            Vector3 planeSize = new Vector3(size.x, 0.02f, size.z);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(planeCenter, planeSize);
        }

        Gizmos.matrix = previous;
    }

    bool TryGetExpandedColliderBounds(out Bounds bounds)
    {
        bounds = default;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
            return false;

        bounds.Expand(new Vector3(xzPadding * 2f, 0f, xzPadding * 2f));
        return true;
    }
}
