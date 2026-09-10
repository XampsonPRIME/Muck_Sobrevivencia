using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FpsHUD : MonoBehaviour
{
    const string FpsVisibleKey = "demo.show_fps";

    public float refreshInterval = 0.25f;

    TextMeshProUGUI fpsText;
    bool isVisible;
    float timer;
    int frameCount;
    float accumulatedUnscaledTime;

    void Awake()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
        if (fpsText != null && string.IsNullOrWhiteSpace(fpsText.text))
            fpsText.text = "FPS: --";

        bool defaultVisible = Application.isEditor || Debug.isDebugBuild;
        isVisible = PlayerPrefs.GetInt(FpsVisibleKey, defaultVisible ? 1 : 0) == 1;
        ApplyVisibility();
    }

    void OnEnable()
    {
        ResetCounter();
    }

    void Update()
    {
        ApplyVisibility();
        if (GameState.IsInLobby)
            return;
        HandleToggle();

        if (fpsText == null)
            return;

        if (!isVisible)
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

    void HandleToggle()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f2Key.wasPressedThisFrame)
            return;

        isVisible = !isVisible;
        PlayerPrefs.SetInt(FpsVisibleKey, isVisible ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVisibility();
        ResetCounter();
    }

    void ApplyVisibility()
    {
        if (fpsText != null)
            fpsText.enabled = isVisible && !GameState.IsInLobby;
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
