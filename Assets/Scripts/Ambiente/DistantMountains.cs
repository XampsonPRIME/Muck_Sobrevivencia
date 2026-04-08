using System.Collections.Generic;
using UnityEngine;

public class DistantMountains : MonoBehaviour
{
    public Transform followTarget;

    [Header("Layout")]
    public int mountainCount = 18;
    public float ringRadius = 270f;
    public float angleJitter = 5f;

    [Header("Mountain Size")]
    public float mountainWidthMin = 140f;
    public float mountainWidthMax = 240f;
    public float mountainHeightMin = 190f;
    public float mountainHeightMax = 320f;
    public float baseY = -85f;
    public float horizonSink = 40f;

    readonly List<Transform> mountainRoots = new List<Transform>();

    static Material sharedMaterial;

    public void Initialize(Transform target)
    {
        followTarget = target;
        EnsureMountains();
        UpdateFollowPosition();
    }

    void Awake()
    {
        if (mountainRoots.Count == 0 && transform.childCount > 0)
        {
            for (int i = 0; i < transform.childCount; i++)
                mountainRoots.Add(transform.GetChild(i));
        }
    }

    void LateUpdate()
    {
        UpdateFollowPosition();
    }

    void EnsureMountains()
    {
        ClearChildren();
        mountainRoots.Clear();

        int count = Mathf.Max(8, mountainCount);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float angleDeg = t * 360f + Mathf.Lerp(-angleJitter, angleJitter, Mathf.PerlinNoise(19.1f, i * 0.37f));
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float width = Mathf.Lerp(mountainWidthMin, mountainWidthMax, Mathf.PerlinNoise(43.7f, i * 0.29f));
            float height = Mathf.Lerp(mountainHeightMin, mountainHeightMax, Mathf.PerlinNoise(71.3f, i * 0.23f));

            Transform root = new GameObject($"Mountain_{i}").transform;
            root.SetParent(transform, false);

            Vector3 direction = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
            root.localPosition = direction * ringRadius;
            root.localRotation = Quaternion.LookRotation(-direction, Vector3.up);

            MeshFilter meshFilter = root.gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = root.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = GetMountainMaterial();

            meshFilter.sharedMesh = BuildMountainMesh(i, width, height);
            mountainRoots.Add(root);
        }
    }

    Mesh BuildMountainMesh(int seed, float width, float height)
    {
        int ridgePoints = 10;
        Vector3[] vertices = new Vector3[ridgePoints * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(ridgePoints - 1) * 6];

        float halfWidth = width * 0.5f;

        for (int i = 0; i < ridgePoints; i++)
        {
            float t = i / (float)(ridgePoints - 1);
            float x = Mathf.Lerp(-halfWidth, halfWidth, t);
            float topNoise = Mathf.PerlinNoise(seed * 0.17f + 5.1f, i * 0.31f);
            float topHeight = Mathf.Lerp(height * 0.45f, height, topNoise);
            float shoulderNoise = Mathf.PerlinNoise(seed * 0.13f + 17.3f, i * 0.41f);
            float peakShape = Mathf.Sin(t * Mathf.PI);
            topHeight *= Mathf.Lerp(0.75f, 1.25f, shoulderNoise) * Mathf.Lerp(0.55f, 1f, peakShape);

            if (i == 0 || i == ridgePoints - 1)
                topHeight *= 0.18f;

            vertices[i * 2] = new Vector3(x, baseY, 0f);
            vertices[i * 2 + 1] = new Vector3(x, baseY + topHeight - horizonSink, 0f);

            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);
        }

        int ti = 0;
        for (int i = 0; i < ridgePoints - 1; i++)
        {
            int vi = i * 2;
            triangles[ti++] = vi;
            triangles[ti++] = vi + 3;
            triangles[ti++] = vi + 1;
            triangles[ti++] = vi;
            triangles[ti++] = vi + 2;
            triangles[ti++] = vi + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"DistantMountain_{seed}";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void UpdateFollowPosition()
    {
        if (followTarget == null)
            return;

        Vector3 targetPos = followTarget.position;
        transform.position = new Vector3(targetPos.x, 0f, targetPos.z);
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    Material GetMountainMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        sharedMaterial = new Material(shader);
        Color baseColor = new Color(0.34f, 0.33f, 0.25f, 1f);

        if (sharedMaterial.HasProperty("_BaseColor"))
            sharedMaterial.SetColor("_BaseColor", baseColor);

        sharedMaterial.color = baseColor;

        if (sharedMaterial.HasProperty("_Smoothness"))
            sharedMaterial.SetFloat("_Smoothness", 0.02f);

        return sharedMaterial;
    }
}
