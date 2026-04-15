using UnityEngine;

[CreateAssetMenu(fileName = "RoadMaskData", menuName = "World/Road Mask Data")]
public class RoadMaskData : ScriptableObject
{
    public Texture2D roadMask;
    public Vector2 worldOrigin = new Vector2(-400f, -400f);
    public Vector2 worldSize = new Vector2(800f, 800f);
    public bool clampOutsideBounds = true;
    [Range(0f, 1f)] public float roadThreshold = 0.6f;
    [Min(0f)] public float smoothSampleRadius = 0.003f;
    [Range(0, 4)] public int smoothSampleKernel = 1;

    public float SampleMask01(Vector2 worldPoint)
    {
        if (roadMask == null)
            return 0f;

        Vector2 uv = WorldToUv(worldPoint);
        int kernel = Mathf.Max(0, smoothSampleKernel);
        if (kernel == 0 || smoothSampleRadius <= 0f)
            return roadMask.GetPixelBilinear(uv.x, uv.y).grayscale;

        float accum = 0f;
        float weightSum = 0f;
        for (int y = -kernel; y <= kernel; y++)
        {
            for (int x = -kernel; x <= kernel; x++)
            {
                Vector2 offset = new Vector2(x, y) * smoothSampleRadius;
                float distance = Mathf.Sqrt(x * x + y * y);
                float weight = 1f / (1f + distance);
                accum += roadMask.GetPixelBilinear(uv.x + offset.x, uv.y + offset.y).grayscale * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0f ? accum / weightSum : 0f;
    }

    public bool IsRoad(Vector2 worldPoint)
    {
        return SampleMask01(worldPoint) >= roadThreshold;
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
}
