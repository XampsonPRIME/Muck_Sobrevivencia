using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FpsHUD : MonoBehaviour
{
    public float refreshInterval = 0.25f;

    TextMeshProUGUI fpsText;
    float timer;
    int frameCount;
    float accumulatedUnscaledTime;

    void Awake()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
        if (fpsText != null && string.IsNullOrWhiteSpace(fpsText.text))
            fpsText.text = "FPS: --";
    }

    void OnEnable()
    {
        ResetCounter();
    }

    void Update()
    {
        if (fpsText == null)
            return;

        timer += Time.unscaledDeltaTime;
        accumulatedUnscaledTime += Time.unscaledDeltaTime;
        frameCount++;

        if (timer < Mathf.Max(0.1f, refreshInterval))
            return;

        float averageDelta = accumulatedUnscaledTime / Mathf.Max(1, frameCount);
        float fps = averageDelta > 0f ? 1f / averageDelta : 0f;
        float milliseconds = averageDelta * 1000f;

        fpsText.text = $"FPS: {Mathf.RoundToInt(fps)} ({milliseconds:0.0} ms)";
        fpsText.color = GetFpsColor(fps);

        ResetCounter();
    }

    Color GetFpsColor(float fps)
    {
        if (fps >= 55f)
            return new Color(0.62f, 0.95f, 0.62f, 1f);

        if (fps >= 30f)
            return new Color(1f, 0.88f, 0.45f, 1f);

        return new Color(1f, 0.45f, 0.45f, 1f);
    }

    void ResetCounter()
    {
        timer = 0f;
        frameCount = 0;
        accumulatedUnscaledTime = 0f;
    }
}
