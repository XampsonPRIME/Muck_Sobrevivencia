using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DemoPolishRuntime : MonoBehaviour
{
    const string IntroSeenKey = "demo.intro_seen";
    const float IntroVisibleSeconds = 12f;
    const float StartupGuideMinimumVisibleSeconds = 4f;
    const float StartupGuideInputGraceSeconds = 1.25f;

    static DemoPolishRuntime instance;

    Canvas canvas;
    CanvasGroup introGroup;
    CanvasGroup helpGroup;
    TextMeshProUGUI cornerHintText;
    TextMeshProUGUI helpBodyText;
    Button helpCloseButton;
    bool introShownThisSession;
    bool startupGuideShownForCurrentWorld;
    bool startupGuideAwaitingManualClose;
    bool helpVisible;
    float introShownAt;
    float helpShownAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<DemoPolishRuntime>() != null)
            return;

        GameObject runtimeObject = new GameObject("DemoPolishRuntime");
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<DemoPolishRuntime>();
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
        SetIntroVisible(false);
        SetHelpVisible(false);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (GameState.IsDemoGuideOpen)
            GameState.IsDemoGuideOpen = false;
    }

    void Update()
    {
        HandleInput();

        bool gameplayVisible = IsGameplayVisible();
        if (!gameplayVisible)
        {
            SetIntroVisible(false);
            SetCornerHintVisible(false);

            if (ShouldKeepStartupGuideDuringStateChange())
                return;

            SetHelpVisible(false);
            if (GameState.IsInLobby || GameState.IsWorldLoading)
                startupGuideShownForCurrentWorld = false;
            return;
        }

        SetCornerHintVisible(true);
        UpdateHelpText();

        if (!startupGuideShownForCurrentWorld && IsGameplayPlayerReady())
            ShowStartupGuide();

        if (!introShownThisSession && !helpVisible && PlayerPrefs.GetInt(IntroSeenKey, 0) == 0)
            ShowIntro();

        if (introGroup != null &&
            introGroup.alpha > 0f &&
            Time.unscaledTime - introShownAt >= IntroVisibleSeconds)
        {
            DismissIntro();
        }
    }

    void HandleInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            if (helpVisible && ShouldIgnoreStartupGuideCloseInput())
                return;

            SetHelpVisible(!helpVisible);
        }

        if (helpVisible && keyboard.escapeKey.wasPressedThisFrame && !ShouldIgnoreStartupGuideCloseInput())
            SetHelpVisible(false);

        if (introGroup != null &&
            introGroup.alpha > 0f &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            DismissIntro();
        }
    }

    bool IsGameplayVisible()
    {
        return !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPowerSelectionOpen &&
               !GameState.IsPlayerDead &&
               !GameState.IsPaused;
    }

    bool IsGameplayPlayerReady()
    {
        return LanMultiplayerManager.FindGameplayPlayer() != null;
    }

    void ShowStartupGuide()
    {
        startupGuideShownForCurrentWorld = true;
        startupGuideAwaitingManualClose = true;
        introShownThisSession = true;
        SetIntroVisible(false);
        SetHelpVisible(true);
    }

    void ShowIntro()
    {
        introShownThisSession = true;
        introShownAt = Time.unscaledTime;
        SetIntroVisible(true);
    }

    void DismissIntro()
    {
        PlayerPrefs.SetInt(IntroSeenKey, 1);
        PlayerPrefs.Save();
        SetIntroVisible(false);
    }

    void BuildUi()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("DemoPolishCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 440;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        BuildCornerHint(canvasObject.transform);
        BuildIntroPanel(canvasObject.transform);
        BuildHelpPanel(canvasObject.transform);
    }

    void BuildCornerHint(Transform parent)
    {
        GameObject hintObject = CreateUiObject("DemoCornerHint", parent);
        RectTransform rect = hintObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(22f, -18f);
        rect.sizeDelta = new Vector2(700f, 42f);

        cornerHintText = hintObject.AddComponent<TextMeshProUGUI>();
        cornerHintText.text = BuildCornerHintText();
        cornerHintText.alignment = TextAlignmentOptions.Left;
        cornerHintText.fontSize = 20f;
        cornerHintText.fontStyle = FontStyles.Bold;
        cornerHintText.color = new Color(0.92f, 0.9f, 0.76f, 0.86f);
        cornerHintText.raycastTarget = false;
    }

    void BuildIntroPanel(Transform parent)
    {
        GameObject panel = CreatePanel("DemoIntroPanel", parent, new Vector2(0f, 132f), new Vector2(760f, 170f), new Color(0.05f, 0.045f, 0.028f, 0.9f));
        introGroup = panel.AddComponent<CanvasGroup>();

        TextMeshProUGUI title = CreateText("Title", panel.transform, "Demo de julho", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        title.rectTransform.anchoredPosition = new Vector2(0f, 48f);
        title.rectTransform.sizeDelta = new Vector2(700f, 42f);
        title.color = new Color(1f, 0.86f, 0.42f, 1f);

        TextMeshProUGUI body = CreateText(
            "Body",
            panel.transform,
            "Explore, colete recursos, evolua, escolha um legado na Caverna dos Portadores e teste combate, bestiario e mapa.\nF1 abre o guia rapido. Enter ou Espaco fecha este aviso.",
            20,
            FontStyles.Normal,
            TextAlignmentOptions.Center);
        body.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        body.rectTransform.sizeDelta = new Vector2(700f, 96f);
        body.color = new Color(0.92f, 0.88f, 0.74f, 1f);
    }

    void BuildHelpPanel(Transform parent)
    {
        GameObject panel = CreatePanel("DemoHelpPanel", parent, new Vector2(0f, 0f), new Vector2(900f, 660f), new Color(0.025f, 0.035f, 0.065f, 0.97f));
        helpGroup = panel.AddComponent<CanvasGroup>();

        TextMeshProUGUI title = CreateText("Title", panel.transform, "Guia rapido da demo", 36, FontStyles.Bold, TextAlignmentOptions.Center);
        title.rectTransform.anchoredPosition = new Vector2(0f, 274f);
        title.rectTransform.sizeDelta = new Vector2(840f, 58f);
        title.color = new Color(1f, 0.88f, 0.5f, 1f);

        helpBodyText = CreateText("Body", panel.transform, string.Empty, 22, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        helpBodyText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        helpBodyText.rectTransform.sizeDelta = new Vector2(820f, 475f);
        helpBodyText.color = new Color(0.9f, 0.93f, 0.98f, 1f);
        helpBodyText.textWrappingMode = TextWrappingModes.Normal;

        helpCloseButton = CreateButton("CloseButton", panel.transform, "FECHAR GUIA", new Vector2(0f, -282f), new Vector2(250f, 52f));
        helpCloseButton.onClick.AddListener(() => SetHelpVisible(false));

        TextMeshProUGUI footer = CreateText("Footer", panel.transform, "F1 ou ESC tambem fecham este guia", 18, FontStyles.Italic, TextAlignmentOptions.Center);
        footer.rectTransform.anchoredPosition = new Vector2(0f, -324f);
        footer.rectTransform.sizeDelta = new Vector2(820f, 34f);
        footer.color = new Color(0.72f, 0.78f, 0.88f, 1f);
    }

    void UpdateHelpText()
    {
        if (helpBodyText == null)
            return;

        helpBodyText.text =
            "Objetivo da demo\n" +
            "- Sobreviva ao primeiro dia, complete a jornada inicial e teste os combates.\n" +
            "- Pressione E nos legados da Caverna dos Portadores para escolher um poder.\n" +
            "- Use o mapa e o bestiario para entender o mundo e as criaturas.\n\n" +
            "Controles essenciais\n" +
            "WASD mover | Mouse camera/ataque | Shift correr | Espaco pular | E interagir\n" +
            "Tab inventario | M mapa | B bestiario | J missoes | 1-8 hotbar | ESC pausa\n" +
            "Q/R/F/V habilidades do portador | F2 mostrar/ocultar FPS\n\n" +
            $"Versao: {Application.version}  |  Build: Windows demo";
    }

    string BuildCornerHintText()
    {
        return $"Demo {Application.version} | F1 guia rapido | F2 FPS | ESC pausa";
    }

    void SetIntroVisible(bool visible)
    {
        SetGroupVisible(introGroup, visible);
    }

    void SetHelpVisible(bool visible)
    {
        bool wasVisible = helpVisible;
        helpVisible = visible;
        GameState.IsDemoGuideOpen = visible;
        if (visible)
            helpShownAt = Time.unscaledTime;
        else
            startupGuideAwaitingManualClose = false;

        if (wasVisible && !visible)
            GameState.LastUiCloseFrame = Time.frameCount;
        SetGroupVisible(helpGroup, visible, visible);

        if (visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (wasVisible && IsGameplayVisible())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    bool ShouldIgnoreStartupGuideCloseInput()
    {
        return startupGuideAwaitingManualClose &&
               Time.unscaledTime - helpShownAt < StartupGuideInputGraceSeconds;
    }

    bool ShouldKeepStartupGuideDuringStateChange()
    {
        if (!helpVisible || !startupGuideAwaitingManualClose)
            return false;

        if (GameState.IsInLobby || GameState.IsPlayerDead)
            return false;

        return Time.unscaledTime - helpShownAt < StartupGuideMinimumVisibleSeconds;
    }

    void SetCornerHintVisible(bool visible)
    {
        if (cornerHintText != null)
        {
            cornerHintText.enabled = visible;
            cornerHintText.text = BuildCornerHintText();
        }
    }

    static void SetGroupVisible(CanvasGroup group, bool visible, bool interactive = false)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && interactive;
        group.blocksRaycasts = visible && interactive;
    }

    GameObject CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panel = CreateUiObject(name, parent);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.94f, 0.72f, 0.28f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        return panel;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    Button CreateButton(string name, Transform parent, string text, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.72f, 0.42f, 0.12f, 0.95f);
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.72f, 0.42f, 0.12f, 0.95f);
        colors.highlightedColor = new Color(0.92f, 0.62f, 0.2f, 1f);
        colors.pressedColor = new Color(0.5f, 0.27f, 0.08f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TextMeshProUGUI label = CreateText("Label", buttonObject.transform, text, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.rectTransform.sizeDelta = size;
        label.color = new Color(1f, 0.94f, 0.74f, 1f);

        return button;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj;
    }
}
