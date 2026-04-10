using UnityEngine;

[CreateAssetMenu(fileName = "WorldHeightmapData", menuName = "Muck/World Heightmap Data")]
public class WorldHeightmapData : ScriptableObject
{
    public Texture2D heightmap;
    public Vector2 worldOrigin = new Vector2(-400f, -400f);
    public Vector2 worldSize = new Vector2(800f, 800f);
    public float minHeight = 0f;
    public float maxHeight = 24f;
    public bool clampOutsideBounds = true;
    public bool applyRiverCarving = false;
    [Range(0f, 1f)] public float riverCarvingStrength = 0.45f;
    [Range(0.2f, 1f)] public float riverWidthScale = 0.55f;
    [Range(0.2f, 1f)] public float riverWaterInsetScale = 0.58f;
    public bool applyRoadFlattening = false;
    [Range(0.5f, 4f)] public float heightResponse = 1.8f;
    [Range(0f, 0.02f)] public float smoothSampleRadius = 0.006f;
    [Range(1, 3)] public int smoothSampleKernel = 2;

    public bool CanSample()
    {
        return heightmap != null && worldSize.x > 0.001f && worldSize.y > 0.001f;
    }

    public bool TrySampleHeight(Vector2 worldPoint, out float height)
    {
        height = 0f;

        if (!CanSample())
            return false;

        float u = Mathf.InverseLerp(worldOrigin.x, worldOrigin.x + worldSize.x, worldPoint.x);
        float v = Mathf.InverseLerp(worldOrigin.y, worldOrigin.y + worldSize.y, worldPoint.y);

        bool outsideBounds = u < 0f || u > 1f || v < 0f || v > 1f;
        if (outsideBounds && !clampOutsideBounds)
            return false;

        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        float grayscale = SampleSmoothedGrayscale(u, v);
        grayscale = Mathf.Pow(grayscale, heightResponse);
        height = Mathf.Lerp(minHeight, maxHeight, grayscale);
        return true;
    }

    float SampleSmoothedGrayscale(float u, float v)
    {
        int kernel = Mathf.Max(1, smoothSampleKernel);
        float radius = Mathf.Max(0f, smoothSampleRadius);

        if (kernel == 1 || radius <= 0.00001f)
            return heightmap.GetPixelBilinear(u, v).grayscale;

        float sum = 0f;
        float weightSum = 0f;

        for (int y = -kernel; y <= kernel; y++)
        {
            for (int x = -kernel; x <= kernel; x++)
            {
                float offsetU = u + x * radius;
                float offsetV = v + y * radius;
                float distance = Mathf.Sqrt(x * x + y * y);
                float weight = 1f / (1f + distance);

                sum += heightmap.GetPixelBilinear(Mathf.Clamp01(offsetU), Mathf.Clamp01(offsetV)).grayscale * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0f ? sum / weightSum : heightmap.GetPixelBilinear(u, v).grayscale;
    }
}
