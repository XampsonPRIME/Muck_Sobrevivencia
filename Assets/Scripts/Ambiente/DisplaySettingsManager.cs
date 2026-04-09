using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DisplaySettingsManager : MonoBehaviour
{
    const string UiScaleKey = "settings.ui_scale";
    const string DisplayDefaultsAppliedKey = "settings.display_defaults_applied";
    const float DefaultUiScale = 1f;
    const float MinUiScale = 0.8f;
    const float MaxUiScale = 1.45f;
    const float RefreshInterval = 0.5f;

    static DisplaySettingsManager instance;

    readonly Dictionary<int, Vector2> baseReferenceResolutions = new Dictionary<int, Vector2>();

    float nextRefreshTime;

    public static float CurrentUiScale => Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, DefaultUiScale), MinUiScale, MaxUiScale);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<DisplaySettingsManager>() != null)
            return;

        GameObject managerObject = new GameObject("DisplaySettingsManager");
        managerObject.AddComponent<DisplaySettingsManager>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyDesktopDefaultsIfNeeded();
        RefreshCanvasScaling();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        RefreshCanvasScaling();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshCanvasScaling();
    }

    void ApplyDesktopDefaultsIfNeeded()
    {
        if (PlayerPrefs.GetInt(DisplayDefaultsAppliedKey, 0) == 1)
            return;

        if (Application.isMobilePlatform)
            return;

        Resolution currentResolution = Screen.currentResolution;
        if (currentResolution.width > 0 && currentResolution.height > 0)
            Screen.SetResolution(currentResolution.width, currentResolution.height, FullScreenMode.FullScreenWindow);

        PlayerPrefs.SetInt(DisplayDefaultsAppliedKey, 1);
        PlayerPrefs.Save();
    }

    public static void SetUiScale(float value)
    {
        float clamped = Mathf.Clamp(value, MinUiScale, MaxUiScale);
        PlayerPrefs.SetFloat(UiScaleKey, clamped);
        PlayerPrefs.Save();
        instance?.RefreshCanvasScaling();
    }

    public static void ConfigureCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return;

        if (instance == null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = scaler.referenceResolution == Vector2.zero ? new Vector2(1920f, 1080f) : scaler.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return;
        }

        instance.RegisterCanvasScaler(scaler);
        instance.ApplyToCanvasScaler(scaler);
    }

    void RefreshCanvasScaling()
    {
        CanvasScaler[] scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (!IsRuntimeCanvasScaler(scaler))
                continue;

            RegisterCanvasScaler(scaler);
            ApplyToCanvasScaler(scaler);
        }
    }

    void RegisterCanvasScaler(CanvasScaler scaler)
    {
        int id = scaler.GetInstanceID();
        if (baseReferenceResolutions.ContainsKey(id))
            return;

        Vector2 referenceResolution = scaler.referenceResolution;
        if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            referenceResolution = new Vector2(1920f, 1080f);

        baseReferenceResolutions[id] = referenceResolution;
    }

    void ApplyToCanvasScaler(CanvasScaler scaler)
    {
        int id = scaler.GetInstanceID();
        if (!baseReferenceResolutions.TryGetValue(id, out Vector2 baseResolution))
            baseResolution = new Vector2(1920f, 1080f);

        float uiScale = CurrentUiScale;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = baseResolution / uiScale;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static bool IsRuntimeCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return false;

        GameObject owner = scaler.gameObject;
        if (owner == null)
            return false;

        Scene scene = owner.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        if ((owner.hideFlags & HideFlags.HideAndDontSave) != 0)
            return false;

        return true;
    }
}
