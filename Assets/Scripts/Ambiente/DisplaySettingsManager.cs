using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DisplaySettingsManager : MonoBehaviour
{
    public struct ResolutionOption
    {
        public int width;
        public int height;

        public string Label => $"{width}x{height}";
    }

    const string UiScaleKey = "settings.ui_scale";
    const string DisplayDefaultsAppliedKey = "settings.display_defaults_applied";
    const string ResolutionWidthKey = "settings.display_width";
    const string ResolutionHeightKey = "settings.display_height";
    const string FullscreenKey = "settings.display_fullscreen";
    const string FrameRateCapKey = "settings.frame_rate_cap";

    const float DefaultUiScale = 1.15f;
    const float MinUiScale = 0.8f;
    const float MaxUiScale = 1.6f;
    const float RefreshInterval = 0.5f;
    const int DefaultFrameRateCap = 120;
    const int MinFrameRateCap = 30;
    const int MaxFrameRateCap = 240;
    const int DemoMaxRenderWidth = 1920;
    const int DemoMaxRenderHeight = 1080;
    const float DemoRenderScale = 0.75f;

    static DisplaySettingsManager instance;
    static readonly List<ResolutionOption> resolutionOptions = new List<ResolutionOption>();
    static readonly Vector2 BaseReferenceResolution = new Vector2(1920f, 1080f);

    readonly Dictionary<int, Vector2> baseReferenceResolutions = new Dictionary<int, Vector2>();

    float nextRefreshTime;

    public static float CurrentUiScale => Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, DefaultUiScale), MinUiScale, MaxUiScale);
    public static bool IsFullscreen => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
    public static int CurrentFrameRateCap => Mathf.Clamp(PlayerPrefs.GetInt(FrameRateCapKey, DefaultFrameRateCap), MinFrameRateCap, MaxFrameRateCap);

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

        BuildResolutionOptions();
        ApplyDesktopDefaultsIfNeeded();
        ApplySavedDisplaySettings();
        ApplyFrameRateCap();
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
        ApplyFrameRateCap();
        RefreshCanvasScaling();
    }

    void ApplyFrameRateCap()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = CurrentFrameRateCap;
        ApplyDemoPerformanceProfile();
    }

    void ApplyDemoPerformanceProfile()
    {
        QualitySettings.pixelLightCount = 1;
        QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowCascades = 0;
        QualitySettings.shadowDistance = 24f;
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        QualitySettings.lodBias = 1f;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
        QualitySettings.particleRaycastBudget = 64;
        QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset ??
                                                QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
            pipeline.renderScale = Mathf.Min(pipeline.renderScale, DemoRenderScale);
    }

    public static void SetFrameRateCap(int targetFrameRate)
    {
        int clamped = Mathf.Clamp(targetFrameRate, MinFrameRateCap, MaxFrameRateCap);
        PlayerPrefs.SetInt(FrameRateCapKey, clamped);
        PlayerPrefs.Save();
        instance?.ApplyFrameRateCap();
    }

    void ApplyDesktopDefaultsIfNeeded()
    {
        if (PlayerPrefs.GetInt(DisplayDefaultsAppliedKey, 0) == 1)
            return;

        if (Application.isMobilePlatform)
            return;

        Resolution currentResolution = Screen.currentResolution;

        int width = currentResolution.width > 0 ? currentResolution.width : Screen.width;
        int height = currentResolution.height > 0 ? currentResolution.height : Screen.height;

        ClampToDemoResolution(ref width, ref height);

        PlayerPrefs.SetInt(ResolutionWidthKey, width);
        PlayerPrefs.SetInt(ResolutionHeightKey, height);
        PlayerPrefs.SetInt(FullscreenKey, 1);
        PlayerPrefs.SetInt(DisplayDefaultsAppliedKey, 1);
        PlayerPrefs.Save();
    }

    void ApplySavedDisplaySettings()
    {
        if (Application.isMobilePlatform)
            return;

        int width = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
        int height = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);

        ApplyResolution(width, height, IsFullscreen, true);
    }

    static void BuildResolutionOptions()
    {
        resolutionOptions.Clear();

        Resolution[] availableResolutions = Screen.resolutions;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution resolution = availableResolutions[i];

            if (resolution.width < 1280 || resolution.height < 720)
                continue;

            bool exists = false;

            for (int j = 0; j < resolutionOptions.Count; j++)
            {
                if (resolutionOptions[j].width == resolution.width &&
                    resolutionOptions[j].height == resolution.height)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                resolutionOptions.Add(new ResolutionOption
                {
                    width = resolution.width,
                    height = resolution.height
                });
            }
        }

        if (resolutionOptions.Count == 0)
        {
            resolutionOptions.Add(new ResolutionOption { width = 1280, height = 720 });
            resolutionOptions.Add(new ResolutionOption { width = 1920, height = 1080 });
        }

        resolutionOptions.Sort((a, b) =>
        {
            int pixelsA = a.width * a.height;
            int pixelsB = b.width * b.height;

            if (pixelsA != pixelsB)
                return pixelsA.CompareTo(pixelsB);

            return a.width.CompareTo(b.width);
        });
    }

    public static IReadOnlyList<ResolutionOption> GetResolutionOptions()
    {
        if (resolutionOptions.Count == 0)
            BuildResolutionOptions();

        return resolutionOptions;
    }

    public static int GetCurrentResolutionIndex()
    {
        int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
        int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i].width == savedWidth &&
                resolutionOptions[i].height == savedHeight)
                return i;
        }

        return Mathf.Clamp(resolutionOptions.Count - 1, 0, resolutionOptions.Count - 1);
    }

    public static void SetResolutionByIndex(int index)
    {
        if (resolutionOptions.Count == 0)
            BuildResolutionOptions();

        ResolutionOption option = resolutionOptions[Mathf.Clamp(index, 0, resolutionOptions.Count - 1)];

        PlayerPrefs.SetInt(ResolutionWidthKey, option.width);
        PlayerPrefs.SetInt(ResolutionHeightKey, option.height);
        PlayerPrefs.Save();

        instance?.ApplyResolution(option.width, option.height, IsFullscreen, false);
    }

    public static void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();

        int width = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
        int height = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);

        instance?.ApplyResolution(width, height, fullscreen, false);
    }

    void ApplyResolution(int width, int height, bool fullscreen, bool force)
    {
        if (Application.isMobilePlatform)
            return;

        int safeWidth = Mathf.Max(1280, width);
        int safeHeight = Mathf.Max(720, height);
        ClampToDemoResolution(ref safeWidth, ref safeHeight);

        FullScreenMode mode = fullscreen
            ? FullScreenMode.ExclusiveFullScreen // 🔥 melhor pra evitar zoom estranho
            : FullScreenMode.Windowed;

        if (!force &&
            Screen.width == safeWidth &&
            Screen.height == safeHeight &&
            Screen.fullScreenMode == mode)
            return;

        Screen.SetResolution(safeWidth, safeHeight, mode);
    }

    static void ClampToDemoResolution(ref int width, ref int height)
    {
        width = Mathf.Max(1280, width);
        height = Mathf.Max(720, height);

        if (width <= DemoMaxRenderWidth && height <= DemoMaxRenderHeight)
            return;

        float scale = Mathf.Min(
            DemoMaxRenderWidth / (float)width,
            DemoMaxRenderHeight / (float)height
        );

        width = Mathf.Max(1280, Mathf.RoundToInt(width * scale));
        height = Mathf.Max(720, Mathf.RoundToInt(height * scale));
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

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GetReferenceResolutionForScale(CurrentUiScale);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    void RefreshCanvasScaling()
    {
        CanvasScaler[] scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();

        foreach (var scaler in scalers)
        {
            if (!IsRuntimeCanvasScaler(scaler))
                continue;

            ApplyToCanvasScaler(scaler);
        }
    }

    void ApplyToCanvasScaler(CanvasScaler scaler)
    {
        float uiScale = CurrentUiScale;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // ✅ CORREÇÃO PRINCIPAL (sem zoom bug)
        scaler.referenceResolution = GetReferenceResolutionForScale(uiScale);

        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static bool IsRuntimeCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return false;

        if (scaler.GetComponent<CraftingBenchUI>() != null)
            return false;

        var scene = scaler.gameObject.scene;

        return scene.IsValid() && scene.isLoaded;
    }

    static Vector2 GetReferenceResolutionForScale(float uiScale)
    {
        return BaseReferenceResolution / Mathf.Max(0.1f, uiScale);
    }
}
