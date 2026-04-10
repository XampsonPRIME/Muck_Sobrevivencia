using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadPath : MonoBehaviour
{
    [Min(2f)] public float roadWidth = 8f;
    [Min(0f)] public float spawnClearMargin = 2f;
    [Min(1f)] public float flattenBlendWidth = 6f;
    [Min(0.5f)] public float visualSampleSpacing = 2.5f;
    [Min(0f)] public float roadSurfaceOffset = 0.08f;
    public bool autoRefreshVisualInPlayMode = true;
    [Min(0.2f)] public float refreshInterval = 1f;
    public bool useChildrenAsWaypoints = true;
    public Transform[] waypoints;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh roadMesh;
    float nextRefreshTime;
    static Material sharedRoadMaterial;

    public bool ContainsPoint(Vector3 worldPoint, float extraMargin = 0f)
    {
        if (!TryGetWaypointCount(out int count) || count < 2)
            return false;

        float maxDistance = Mathf.Max(0.5f, roadWidth * 0.5f + spawnClearMargin + extraMargin);
        Vector2 pointXZ = new Vector2(worldPoint.x, worldPoint.z);
        float maxDistanceSqr = maxDistance * maxDistance;

        for (int i = 0; i < count - 1; i++)
        {
            if (!TryGetWaypoint(i, out Vector3 a) || !TryGetWaypoint(i + 1, out Vector3 b))
                continue;

            Vector2 aXZ = new Vector2(a.x, a.z);
            Vector2 bXZ = new Vector2(b.x, b.z);

            if ((DistanceToSegmentSquared(pointXZ, aXZ, bXZ)) <= maxDistanceSqr)
                return true;
        }

        return false;
    }

    public bool TryGetFlattenedHeight(Vector3 worldPoint, float currentHeight, out float flattenedHeight, out float distance)
    {
        flattenedHeight = currentHeight;
        distance = float.MaxValue;

        if (!TryGetWaypointCount(out int count) || count < 2)
            return false;

        float hardRadius = Mathf.Max(0.5f, roadWidth * 0.5f);
        float maxRadius = hardRadius + Mathf.Max(0f, flattenBlendWidth);
        Vector2 pointXZ = new Vector2(worldPoint.x, worldPoint.z);

        bool found = false;

        for (int i = 0; i < count - 1; i++)
        {
            if (!TryGetWaypoint(i, out Vector3 a) || !TryGetWaypoint(i + 1, out Vector3 b))
                continue;

            Vector2 aXZ = new Vector2(a.x, a.z);
            Vector2 bXZ = new Vector2(b.x, b.z);
            Vector2 closest = ClosestPointOnSegmentXZ(pointXZ, aXZ, bXZ, out float t);
            float segmentDistance = Vector2.Distance(pointXZ, closest);

            if (segmentDistance > maxRadius || segmentDistance >= distance)
                continue;

            float targetHeight = SampleFlattenHeight(closest);
            float influence = segmentDistance <= hardRadius
                ? 1f
                : 1f - Mathf.InverseLerp(hardRadius, maxRadius, segmentDistance);

            flattenedHeight = Mathf.Lerp(currentHeight, targetHeight, influence);
            distance = segmentDistance;
            found = true;
        }

        return found;
    }

    float SampleFlattenHeight(Vector2 pointXZ)
    {
        float terrainScale = 40f;
        float heightMultiplier = 12f;

        float height = Mathf.PerlinNoise(pointXZ.x / terrainScale, pointXZ.y / terrainScale) * heightMultiplier;
        height += Mathf.PerlinNoise(pointXZ.x * 0.05f, pointXZ.y * 0.05f) * 2f;

        if (RiverSystem.Instance != null && RiverSystem.Instance.TryGetBlend(pointXZ, true, out float riverBlend))
        {
            float riverDepth = RiverSystem.Instance.RiverDepth;
            height = Mathf.Min(height, height - riverDepth * riverBlend);
        }

        return height;
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

    static float DistanceToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denominator = ab.sqrMagnitude;

        if (denominator <= 0.0001f)
            return (point - a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
        Vector2 closest = a + ab * t;
        return (point - closest).sqrMagnitude;
    }

    static Vector2 ClosestPointOnSegmentXZ(Vector2 point, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        float denominator = ab.sqrMagnitude;

        if (denominator <= 0.0001f)
        {
            t = 0f;
            return a;
        }

        t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
        return a + ab * t;
    }

    void Awake()
    {
        EnsureComponents();
    }

    void OnEnable()
    {
        EnsureComponents();
        RoadSystem.Register(this);
        RebuildVisual();
    }

    void OnDisable()
    {
        RoadSystem.Unregister(this);
    }

    void OnDrawGizmos()
    {
        if (!TryGetWaypointCount(out int count) || count < 2)
            return;

        Gizmos.color = new Color(0.68f, 0.52f, 0.24f, 0.9f);

        for (int i = 0; i < count - 1; i++)
        {
            if (!TryGetWaypoint(i, out Vector3 a) || !TryGetWaypoint(i + 1, out Vector3 b))
                continue;

            Gizmos.DrawLine(a, b);

            Vector3 midpoint = (a + b) * 0.5f;
            Vector3 direction = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized * (roadWidth * 0.5f);

            Gizmos.DrawLine(midpoint - right, midpoint + right);
        }
    }

    void Start()
    {
        RebuildVisual();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || !autoRefreshVisualInPlayMode)
            return;

        if (Time.time < nextRefreshTime)
            return;

        nextRefreshTime = Time.time + refreshInterval;
        RebuildVisual();
    }

    void OnValidate()
    {
        EnsureComponents();

        if (!Application.isPlaying)
            RebuildVisual();
    }

    void EnsureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshFilter.sharedMesh == null)
        {
            roadMesh = new Mesh
            {
                name = "RoadMesh"
            };
            meshFilter.sharedMesh = roadMesh;
        }
        else
        {
            roadMesh = meshFilter.sharedMesh;
        }

        meshRenderer.sharedMaterial = GetSharedRoadMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void RebuildVisual()
    {
        EnsureComponents();

        if (!TryBuildRoadSamples(out List<Vector3> centers))
        {
            roadMesh.Clear();
            return;
        }

        Vector3[] vertices = new Vector3[centers.Count * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(centers.Count - 1) * 6];

        float accumulatedLength = 0f;

        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 tangent = GetSampleTangent(centers, i);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized * (roadWidth * 0.5f);

            vertices[i * 2] = transform.InverseTransformPoint(centers[i] - right);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(centers[i] + right);

            if (i > 0)
                accumulatedLength += Vector3.Distance(centers[i - 1], centers[i]);

            uvs[i * 2] = new Vector2(0f, accumulatedLength * 0.2f);
            uvs[i * 2 + 1] = new Vector2(1f, accumulatedLength * 0.2f);

            if (i >= centers.Count - 1)
                continue;

            int triangleIndex = i * 6;
            int vertexIndex = i * 2;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 1;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        roadMesh.Clear();
        roadMesh.vertices = vertices;
        roadMesh.uv = uvs;
        roadMesh.triangles = triangles;
        roadMesh.RecalculateNormals();
        roadMesh.RecalculateBounds();
    }

    bool TryBuildRoadSamples(out List<Vector3> centers)
    {
        centers = new List<Vector3>();

        if (!TryGetWaypointCount(out int count) || count < 2)
            return false;

        for (int i = 0; i < count - 1; i++)
        {
            if (!TryGetWaypoint(i, out Vector3 a) || !TryGetWaypoint(i + 1, out Vector3 b))
                continue;

            float segmentLength = Vector3.Distance(a, b);
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(segmentLength / Mathf.Max(0.5f, visualSampleSpacing)));

            for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
            {
                if (i > 0 && sampleIndex == 0)
                    continue;

                float t = sampleCount <= 0 ? 0f : sampleIndex / (float)sampleCount;
                Vector3 sample = Vector3.Lerp(a, b, t);
                float sampleHeight = SampleRoadHeight(sample);
                centers.Add(new Vector3(sample.x, sampleHeight + roadSurfaceOffset, sample.z));
            }
        }

        return centers.Count >= 2;
    }

    float SampleRoadHeight(Vector3 sample)
    {
        Vector3 origin = new Vector3(sample.x, sample.y + 200f, sample.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return sample.y;
    }

    Vector3 GetSampleTangent(List<Vector3> centers, int index)
    {
        Vector3 tangent;

        if (index <= 0)
            tangent = centers[1] - centers[0];
        else if (index >= centers.Count - 1)
            tangent = centers[index] - centers[index - 1];
        else
            tangent = centers[index + 1] - centers[index - 1];

        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.forward;

        return tangent.normalized;
    }

    static Material GetSharedRoadMaterial()
    {
        if (sharedRoadMaterial != null)
            return sharedRoadMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        sharedRoadMaterial = new Material(shader);
        Color roadColor = new Color(0.42f, 0.31f, 0.18f, 1f);

        if (sharedRoadMaterial.HasProperty("_BaseColor"))
            sharedRoadMaterial.SetColor("_BaseColor", roadColor);

        sharedRoadMaterial.color = roadColor;

        if (sharedRoadMaterial.HasProperty("_Smoothness"))
            sharedRoadMaterial.SetFloat("_Smoothness", 0.08f);

        return sharedRoadMaterial;
    }
}
