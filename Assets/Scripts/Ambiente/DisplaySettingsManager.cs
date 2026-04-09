using System.Collections.Generic;
using UnityEngine;
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
    const float DefaultUiScale = 1f;
    const float MinUiScale = 0.8f;
    const float MaxUiScale = 1.45f;
    const float RefreshInterval = 0.5f;

    static DisplaySettingsManager instance;
    static readonly List<ResolutionOption> resolutionOptions = new List<ResolutionOption>();

    readonly Dictionary<int, Vector2> baseReferenceResolutions = new Dictionary<int, Vector2>();

    float nextRefreshTime;

    public static float CurrentUiScale => Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, DefaultUiScale), MinUiScale, MaxUiScale);
    public static bool IsFullscreen => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;

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
        int width = currentResolution.width > 0 ? currentResolution.width : Screen.width;
        int height = currentResolution.height > 0 ? currentResolution.height : Screen.height;

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

        int width = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width > 0 ? Screen.currentResolution.width : Screen.width);
        int height = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height > 0 ? Screen.currentResolution.height : Screen.height);
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
            for (int optionIndex = 0; optionIndex < resolutionOptions.Count; optionIndex++)
            {
                ResolutionOption option = resolutionOptions[optionIndex];
                if (option.width == resolution.width && option.height == resolution.height)
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

        resolutionOptions.Sort((left, right) =>
        {
            int leftPixels = left.width * left.height;
            int rightPixels = right.width * right.height;
            if (leftPixels != rightPixels)
                return leftPixels.CompareTo(rightPixels);

            return left.width.CompareTo(right.width);
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
        if (resolutionOptions.Count == 0)
            BuildResolutionOptions();

        int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
        int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            ResolutionOption option = resolutionOptions[i];
            if (option.width == savedWidth && option.height == savedHeight)
                return i;
        }

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            ResolutionOption option = resolutionOptions[i];
            if (option.width == Screen.width && option.height == Screen.height)
                return i;
        }

        return Mathf.Clamp(resolutionOptions.Count - 1, 0, resolutionOptions.Count - 1);
    }

    public static void SetResolutionByIndex(int index)
    {
        if (resolutionOptions.Count == 0)
            BuildResolutionOptions();

        if (resolutionOptions.Count == 0)
            return;

        int clampedIndex = Mathf.Clamp(index, 0, resolutionOptions.Count - 1);
        ResolutionOption option = resolutionOptions[clampedIndex];

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
        FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (!force && Screen.width == safeWidth && Screen.height == safeHeight && Screen.fullScreenMode == mode)
            return;

        Screen.SetResolution(safeWidth, safeHeight, mode);
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
