using UnityEngine;

[CreateAssetMenu(fileName = "RoadMaskData", menuName = "Muck/Road Mask Data")]
public class RoadMaskData : ScriptableObject
{
    public Texture2D roadMask;
    public Vector2 worldOrigin = new Vector2(-400f, -400f);
    public Vector2 worldSize = new Vector2(800f, 800f);
    public bool clampOutsideBounds = true;
    [Range(0f, 1f)] public float roadThreshold = 0.35f;
    [Range(0f, 0.02f)] public float smoothSampleRadius = 0.003f;
    [Range(1, 3)] public int smoothSampleKernel = 1;

    public bool CanSample()
    {
        return roadMask != null && worldSize.x > 0.001f && worldSize.y > 0.001f;
    }

    public bool TrySampleBlend(Vector3 worldPoint, out float blend)
    {
        blend = 0f;

        if (!CanSample())
            return false;

        float u = Mathf.InverseLerp(worldOrigin.x, worldOrigin.x + worldSize.x, worldPoint.x);
        float v = Mathf.InverseLerp(worldOrigin.y, worldOrigin.y + worldSize.y, worldPoint.z);

        bool outsideBounds = u < 0f || u > 1f || v < 0f || v > 1f;
        if (outsideBounds && !clampOutsideBounds)
            return false;

        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        blend = SampleSmoothedMask(u, v);
        return true;
    }

    public bool IsRoad(Vector3 worldPoint, float extraWorldMargin = 0f)
    {
        if (!TrySampleBlend(worldPoint, out float blend))
            return false;

        if (extraWorldMargin <= 0.001f)
            return blend >= roadThreshold;

        float stepU = extraWorldMargin / Mathf.Max(1f, worldSize.x);
        float stepV = extraWorldMargin / Mathf.Max(1f, worldSize.y);

        float u = Mathf.InverseLerp(worldOrigin.x, worldOrigin.x + worldSize.x, worldPoint.x);
        float v = Mathf.InverseLerp(worldOrigin.y, worldOrigin.y + worldSize.y, worldPoint.z);

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float sample = SampleSmoothedMask(Mathf.Clamp01(u + x * stepU), Mathf.Clamp01(v + y * stepV));
                if (sample >= roadThreshold)
                    return true;
            }
        }

        return false;
    }

    float SampleSmoothedMask(float u, float v)
    {
        int kernel = Mathf.Max(1, smoothSampleKernel);
        float radius = Mathf.Max(0f, smoothSampleRadius);

        if (kernel == 1 || radius <= 0.00001f)
            return roadMask.GetPixelBilinear(u, v).grayscale;

        float sum = 0f;
        float weightSum = 0f;

        for (int y = -kernel; y <= kernel; y++)
        {
            for (int x = -kernel; x <= kernel; x++)
            {
                float offsetU = Mathf.Clamp01(u + x * radius);
                float offsetV = Mathf.Clamp01(v + y * radius);
                float distance = Mathf.Sqrt(x * x + y * y);
                float weight = 1f / (1f + distance);

                sum += roadMask.GetPixelBilinear(offsetU, offsetV).grayscale * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0f ? sum / weightSum : roadMask.GetPixelBilinear(u, v).grayscale;
    }
}
