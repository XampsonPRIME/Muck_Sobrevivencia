using UnityEngine;

[DisallowMultipleComponent]
public class BreathingScale : MonoBehaviour
{
    [Header("Ritmo")]
    public float speed = 1.4f;
    public float amplitude = 0.08f;
    public bool randomizePhase = true;

    [Header("Forma")]
    public bool affectX = true;
    public bool affectY = true;
    public bool affectZ = true;
    public float verticalBias = 1.15f;

    [Header("Variacao")]
    public bool randomizeAmplitude = true;
    public Vector2 amplitudeMultiplierRange = new Vector2(0.85f, 1.15f);
    public bool unscaledTime;

    Vector3 baseScale;
    float phaseOffset;
    float amplitudeMultiplier = 1f;

    void Awake()
    {
        CaptureBaseScale();
        InitializeVariation();
    }

    void OnEnable()
    {
        CaptureBaseScale();
        InitializeVariation();
        ApplyBreathing(0f);
    }

    void OnDisable()
    {
        transform.localScale = baseScale;
    }

    void Update()
    {
        float time = unscaledTime ? Time.unscaledTime : Time.time;
        ApplyBreathing(time);
    }

    void CaptureBaseScale()
    {
        baseScale = transform.localScale;
    }

    void InitializeVariation()
    {
        phaseOffset = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        if (randomizeAmplitude)
        {
            float min = Mathf.Min(amplitudeMultiplierRange.x, amplitudeMultiplierRange.y);
            float max = Mathf.Max(amplitudeMultiplierRange.x, amplitudeMultiplierRange.y);
            amplitudeMultiplier = Random.Range(min, max);
        }
        else
        {
            amplitudeMultiplier = 1f;
        }
    }

    void ApplyBreathing(float time)
    {
        float pulse = Mathf.Sin(time * Mathf.Max(0.01f, speed) + phaseOffset);
        float scaleOffset = 1f + (pulse * amplitude * amplitudeMultiplier);

        Vector3 scale = baseScale;

        if (affectX)
            scale.x = baseScale.x * scaleOffset;

        if (affectY)
            scale.y = baseScale.y * Mathf.Lerp(1f, scaleOffset, verticalBias);

        if (affectZ)
            scale.z = baseScale.z * scaleOffset;

        transform.localScale = scale;
    }
}
