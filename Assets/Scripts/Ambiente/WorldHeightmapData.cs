using UnityEngine;

[CreateAssetMenu(fileName = "HeightmapData", menuName = "World/Heightmap Data")]
public class WorldHeightmapData : ScriptableObject
{
    public Texture2D heightmap;
    public Vector2 worldOrigin = new Vector2(-400f, -400f);
    public Vector2 worldSize = new Vector2(800f, 800f);
    public float minHeight = 0f;
    public float maxHeight = 14f;
    public bool clampOutsideBounds = true;
    public bool applyRiverCarving = true;
    [Range(0f, 1.5f)] public float riverCarvingStrength = 1f;
    [Range(0.1f, 2f)] public float riverWidthScale = 0.5f;
    [Range(0.1f, 2f)] public float riverWaterInsetScale = 0.52f;
    public bool applyRoadFlattening;
    [Min(0.25f)] public float heightResponse = 1.8f;
    [Min(0f)] public float smoothSampleRadius = 0.006f;
    [Range(0, 4)] public int smoothSampleKernel = 2;

    public float SampleHeight01(Vector2 worldPoint)
    {
        if (heightmap == null)
            return 0.5f;

        Vector2 uv = WorldToUv(worldPoint);
        return SampleSmoothedHeight(uv);
    }

    Vector2 WorldToUv(Vector2 worldPoint)
    {
        float width = Mathf.Max(0.001f, worldSize.x);
        float height = Mathf.Max(0.001f, worldSize.y);
        float u = (worldPoint.x - worldOrigin.x) / width;
        float v = (worldPoint.y - worldOrigin.y) / height;

        if (clampOutsideBounds)
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));

        return new Vector2(u, v);
    }

    float SampleSmoothedHeight(Vector2 uv)
    {
        int kernel = Mathf.Max(0, smoothSampleKernel);
        if (kernel == 0 || smoothSampleRadius <= 0f)
            return ApplyResponse(heightmap.GetPixelBilinear(uv.x, uv.y).grayscale);

        float accum = 0f;
        float weightSum = 0f;
        for (int y = -kernel; y <= kernel; y++)
        {
            for (int x = -kernel; x <= kernel; x++)
            {
                Vector2 offset = new Vector2(x, y) * smoothSampleRadius;
                float distance = Mathf.Sqrt(x * x + y * y);
                float weight = 1f / (1f + distance);
                float sample = heightmap.GetPixelBilinear(uv.x + offset.x, uv.y + offset.y).grayscale;
                accum += ApplyResponse(sample) * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0f ? accum / weightSum : 0.5f;
    }

    float ApplyResponse(float sample)
    {
        float exponent = 1f / Mathf.Max(0.001f, heightResponse);
        return Mathf.Pow(Mathf.Clamp01(sample), exponent);
    }

    public float SampleWorldHeight(Vector2 worldPoint)
    {
        float normalized = SampleHeight01(worldPoint);
        return Mathf.Lerp(minHeight, maxHeight, normalized);
    }
}
