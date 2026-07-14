using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldLoadingScreen : MonoBehaviour
{
    const float MinimumVisibleSeconds = 0.75f;
    const float ReadySettleSeconds = 0.45f;
    const float MissingGeneratorFallbackSeconds = 6f;
    const float PlayerEntryFallbackSeconds = 4f;

    static WorldLoadingScreen instance;
    static bool requestedLoading;

    Canvas canvas;
    CanvasGroup canvasGroup;
    TMP_Text titleText;
    TMP_Text subtitleText;
    TMP_Text detailText;
    TMP_Text percentText;
    Image progressFill;
    RectTransform spinner;

    WorldGenerator generator;
    float shownAt;
    float readyAt = -1f;
    float worldReadyAt = -1f;
    bool playerEntryPrepared;
    string loadingMessage = "Gerando mundo";

    public static void BeginLoading(string message = "Gerando mundo")
    {
        if (LanMultiplayerManager.IsDedicatedRuntime || LanMultiplayerManager.IsDedicatedProcessRequested)
            return;

        requestedLoading = true;
        GameState.IsWorldLoading = true;

        WorldLoadingScreen screen = EnsureInstance();
        screen.loadingMessage = string.IsNullOrWhiteSpace(message) ? "Gerando mundo" : message;
        screen.Show();
    }

    public static void CancelLoading()
    {
        requestedLoading = false;
        GameState.IsWorldLoading = false;

        if (instance != null)
            instance.HideImmediate();
    }

    static WorldLoadingScreen EnsureInstance()
    {
        if (instance != null)
            return instance;

        WorldLoadingScreen existing = FindFirstObjectByType<WorldLoadingScreen>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject obj = new GameObject("WorldLoadingScreen");
        return obj.AddComponent<WorldLoadingScreen>();
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
        BuildUi();

        if (requestedLoading || GameState.IsWorldLoading)
            Show();
        else
            HideImmediate();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (!GameState.IsWorldLoading)
            return;

        if (canvas == null)
            BuildUi();

        if (generator == null)
            generator = FindFirstObjectByType<WorldGenerator>(FindObjectsInactive.Exclude);

        float progress = generator != null ? generator.LoadingProgress : 0f;
        UpdateTexts(progress);

        if (spinner != null)
            spinner.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);

        bool worldReady = generator != null && generator.IsInitialWorldReady;
        if (worldReady)
        {
            if (worldReadyAt < 0f)
                worldReadyAt = Time.unscaledTime;
        }
        else
        {
            worldReadyAt = -1f;
        }

        bool allowPlayerEntryFallback =
            worldReadyAt >= 0f &&
            Time.unscaledTime - worldReadyAt >= PlayerEntryFallbackSeconds;
        bool ready = worldReady && TryPreparePlayerEntry(allowPlayerEntryFallback);
        bool fallbackReady = generator == null && Time.unscaledTime - shownAt >= MissingGeneratorFallbackSeconds;
        bool stalledFallbackReady =
            generator != null &&
            worldReady &&
            playerEntryPrepared &&
            Time.unscaledTime - shownAt >= 20f;

        if (ready || fallbackReady || stalledFallbackReady)
        {
            if (readyAt < 0f)
                readyAt = Time.unscaledTime;

            if (Time.unscaledTime - shownAt >= MinimumVisibleSeconds &&
                Time.unscaledTime - readyAt >= ReadySettleSeconds)
            {
                FinishLoading();
            }
        }
        else
        {
            readyAt = -1f;
        }
    }

    void Show()
    {
        BuildUi();
        generator = null;
        shownAt = Time.unscaledTime;
        readyAt = -1f;
        worldReadyAt = -1f;
        playerEntryPrepared = false;
        requestedLoading = false;
        GameState.IsWorldLoading = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (canvas != null)
            canvas.enabled = true;
    }

    void FinishLoading()
    {
        GameState.IsWorldLoading = false;
        HideImmediate();

        if (!GameState.IsInLobby && !GameState.IsPaused && !GameState.IsInventoryOpen && !GameState.IsBestiaryOpen && !GameState.IsQuestJournalOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    bool TryPreparePlayerEntry(bool allowAirFallback)
    {
        if (playerEntryPrepared)
            return true;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null)
            return false;

        Physics.SyncTransforms();
        playerEntryPrepared = player.WarpToSafePosition(
            player.transform.position,
            player.transform.rotation,
            allowAirFallback);

        return playerEntryPrepared;
    }

    void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (canvas != null)
            canvas.enabled = false;
    }

    void UpdateTexts(float progress)
    {
        if (titleText != null)
            titleText.text = loadingMessage;

        if (subtitleText != null)
            subtitleText.text = "Preparando terreno, biomas e recursos...";

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(progress);

        if (percentText != null)
            percentText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";

        if (detailText == null)
            return;

        if (generator == null)
        {
            detailText.text = "Localizando gerador do mundo...";
        }
        else if (!generator.IsInitialized)
        {
            detailText.text = "Iniciando geracao procedural...";
        }
        else if (!generator.IsInitialWorldReady)
        {
            detailText.text = $"Chunks iniciais: {Mathf.Min(generator.InitialChunkReadyCount, generator.InitialChunkTargetCount)}/{generator.InitialChunkTargetCount}";
        }
        else if (!playerEntryPrepared)
        {
            detailText.text = "Posicionando o jogador em terreno seguro...";
        }
        else
        {
            detailText.text = "Finalizando entrada no mundo...";
        }
    }

    void BuildUi()
    {
        if (canvas != null)
            return;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = CreateImage("Backdrop", transform, new Color(0.03f, 0.025f, 0.02f, 0.96f));
        RectTransform backdropRect = backdrop.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        RectTransform content = CreateRect("Content", transform);
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(680f, 280f);

        Image glow = CreateImage("WarmGlow", content, new Color(0.72f, 0.45f, 0.13f, 0.22f));
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;
        glowRect.sizeDelta = new Vector2(620f, 170f);

        spinner = CreateRect("Spinner", content);
        spinner.anchorMin = new Vector2(0.5f, 0.5f);
        spinner.anchorMax = new Vector2(0.5f, 0.5f);
        spinner.anchoredPosition = new Vector2(0f, 74f);
        spinner.sizeDelta = new Vector2(54f, 54f);

        Image spinnerRing = spinner.gameObject.AddComponent<Image>();
        spinnerRing.color = new Color(0.98f, 0.78f, 0.32f, 0.9f);
        spinnerRing.sprite = CreateRingSprite();
        spinnerRing.preserveAspect = true;

        titleText = CreateText("Title", content, "Gerando mundo", 44, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 18f);
        titleRect.sizeDelta = new Vector2(680f, 60f);
        titleText.color = new Color(1f, 0.89f, 0.58f, 1f);

        subtitleText = CreateText("Subtitle", content, "Preparando terreno, biomas e recursos...", 20, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.anchoredPosition = new Vector2(0f, -28f);
        subtitleRect.sizeDelta = new Vector2(680f, 34f);
        subtitleText.color = new Color(0.92f, 0.84f, 0.67f, 1f);

        Image progressBack = CreateImage("ProgressBack", content, new Color(0.12f, 0.06f, 0.02f, 0.95f));
        RectTransform progressBackRect = progressBack.rectTransform;
        progressBackRect.anchorMin = new Vector2(0.5f, 0.5f);
        progressBackRect.anchorMax = new Vector2(0.5f, 0.5f);
        progressBackRect.anchoredPosition = new Vector2(0f, -72f);
        progressBackRect.sizeDelta = new Vector2(540f, 18f);

        progressFill = CreateImage("ProgressFill", progressBack.transform, new Color(0.94f, 0.62f, 0.18f, 1f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        RectTransform progressFillRect = progressFill.rectTransform;
        progressFillRect.anchorMin = Vector2.zero;
        progressFillRect.anchorMax = Vector2.one;
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;

        percentText = CreateText("Percent", content, "0%", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform percentRect = percentText.rectTransform;
        percentRect.anchorMin = new Vector2(0.5f, 0.5f);
        percentRect.anchorMax = new Vector2(0.5f, 0.5f);
        percentRect.anchoredPosition = new Vector2(0f, -102f);
        percentRect.sizeDelta = new Vector2(160f, 28f);
        percentText.color = new Color(1f, 0.91f, 0.67f, 1f);

        detailText = CreateText("Detail", content, "Iniciando geracao procedural...", 16, FontStyles.Italic, TextAlignmentOptions.Center);
        RectTransform detailRect = detailText.rectTransform;
        detailRect.anchorMin = new Vector2(0.5f, 0.5f);
        detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailRect.anchoredPosition = new Vector2(0f, -134f);
        detailRect.sizeDelta = new Vector2(680f, 30f);
        detailText.color = new Color(0.78f, 0.72f, 0.58f, 1f);
    }

    RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);
        return obj.AddComponent<RectTransform>();
    }

    Image CreateImage(string objectName, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    TMP_Text CreateText(string objectName, Transform parent, string text, int size, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TMP_Text tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    Sprite CreateRingSprite()
    {
        const int size = 64;
        const float center = (size - 1) * 0.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < 0f)
                    angle += 360f;

                bool inRing = distance >= 22f && distance <= 28f;
                bool gap = angle > 35f && angle < 95f;
                texture.SetPixel(x, y, inRing && !gap ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
