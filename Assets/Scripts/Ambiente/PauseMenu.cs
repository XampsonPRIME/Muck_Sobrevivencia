using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    const string MasterVolumeKey = "settings.master_volume";
    const string MouseSensitivityKey = "settings.mouse_sensitivity";
    const float ResetConfirmDuration = 4f;

    Canvas canvas;
    GraphicRaycaster graphicRaycaster;
    GameObject overlayObject;
    GameObject mainPanel;
    GameObject settingsPanel;
    TextMeshProUGUI mainSessionInfoText;
    TextMeshProUGUI settingsSessionInfoText;
    InputAction pauseAction;
    PlayerMovement playerMovement;
    Button resetPlayerButton;
    TextMeshProUGUI resetPlayerButtonText;
    float resetConfirmUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<PauseMenu>() != null)
            return;

        GameObject pauseMenuObject = new GameObject("PauseMenu");
        pauseMenuObject.AddComponent<PauseMenu>();
    }

    void Awake()
    {
        pauseAction = new InputAction("PauseMenu", binding: "<Keyboard>/escape");
    }

    void OnEnable()
    {
        pauseAction.Enable();
    }

    void OnDisable()
    {
        pauseAction.Disable();
    }

    void Start()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
        {
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();
        ResolvePlayer();
        LoadSettings();
        BuildUI();
        SetMenuVisible(false);
    }

    void Update()
    {
        ResolvePlayer();
        UpdateSessionInfo();

        if (GameState.IsInLobby || GameState.IsPlayerDead)
        {
            if (GameState.IsPaused)
                ResumeGame();

            return;
        }

        if (pauseAction.WasPressedThisFrame())
            HandlePausePressed();

        if (!GameState.IsPaused)
            return;

        UpdateResetConfirmationState();
        HandleMouseFallbackClick();
    }

    void HandlePausePressed()
    {
        if (GameState.IsInventoryOpen)
            return;

        if (!GameState.IsPaused)
        {
            PauseGame();
            return;
        }

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            ShowMainPanel();
            return;
        }

        ResumeGame();
    }

    void PauseGame()
    {
        GameState.IsPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetMenuVisible(true);
        ShowMainPanel();
        ClearResetConfirmation();
    }

    void ResumeGame()
    {
        GameState.IsPaused = false;
        Time.timeScale = 1f;
        SetMenuVisible(false);
        ClearResetConfirmation();

        if (!GameState.IsInLobby && !GameState.IsInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OpenSettings()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    void ShowMainPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainPanel != null)
            mainPanel.SetActive(true);

        ClearResetConfirmation();
    }

    void QuitGame()
    {
        Time.timeScale = 1f;
        GameState.IsPaused = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ResolvePlayer()
    {
        if (playerMovement == null)
            playerMovement = LanMultiplayerManager.FindGameplayPlayer();
    }

    void LoadSettings()
    {
        float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 0.9f);
        float mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, 2f);

        AudioListener.volume = Mathf.Clamp01(masterVolume);

        if (playerMovement != null)
            playerMovement.mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.3f, 10f);
    }

    void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
        PlayerPrefs.Save();
    }

    void SetMouseSensitivity(float value)
    {
        float clamped = Mathf.Clamp(value, 0.3f, 10f);

        if (playerMovement != null)
            playerMovement.mouseSensitivity = clamped;

        PlayerPrefs.SetFloat(MouseSensitivityKey, clamped);
        PlayerPrefs.Save();
    }

    void SetInterfaceScale(float value)
    {
        DisplaySettingsManager.SetUiScale(value);
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    void HandleMouseFallbackClick()
    {
        if (canvas == null || graphicRaycaster == null || EventSystem.current == null || Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            Button button = results[i].gameObject.GetComponentInParent<Button>();
            if (button == null || !button.interactable)
                continue;

            button.onClick.Invoke();
            break;
        }
    }

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("PauseCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 460;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);
        graphicRaycaster = canvasObject.AddComponent<GraphicRaycaster>();

        overlayObject = CreateUiObject("PauseOverlay", canvas.transform);
        RectTransform overlayRect = overlayObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.04f, 0.06f, 0.82f);

        mainPanel = CreatePanel("MainPanel", overlayObject.transform, new Vector2(0f, 0f), new Vector2(560f, 620f));
        CreateTitle(mainPanel.transform, "Pausado");
        CreateButton(mainPanel.transform, "ResumeButton", "Continuar", new Vector2(0f, 145f), new Color(0.72f, 0.56f, 0.18f, 1f), ResumeGame);
        CreateButton(mainPanel.transform, "SettingsButton", "Configuracoes", new Vector2(0f, 35f), new Color(0.34f, 0.54f, 0.78f, 1f), OpenSettings);
        resetPlayerButton = CreateButton(mainPanel.transform, "ResetPlayerButton", "Reiniciar personagem", new Vector2(0f, -75f), new Color(0.78f, 0.4f, 0.18f, 1f), HandleResetPlayerPressed);
        resetPlayerButtonText = resetPlayerButton != null ? resetPlayerButton.GetComponentInChildren<TextMeshProUGUI>() : null;
        CreateButton(mainPanel.transform, "QuitButton", "Sair do jogo", new Vector2(0f, -185f), new Color(0.68f, 0.29f, 0.24f, 1f), QuitGame);
        mainSessionInfoText = CreateInfoBlock(mainPanel.transform, new Vector2(0f, -305f));

        settingsPanel = CreatePanel("SettingsPanel", overlayObject.transform, new Vector2(0f, 0f), new Vector2(640f, 680f));
        CreateTitle(settingsPanel.transform, "Configuracoes");
        CreateSliderRow(settingsPanel.transform, "Volume", new Vector2(0f, 70f), 0f, 1f, AudioListener.volume, SetMasterVolume);
        CreateSliderRow(
            settingsPanel.transform,
            "Sensibilidade",
            new Vector2(0f, -40f),
            0.3f,
            10f,
            playerMovement != null ? playerMovement.mouseSensitivity : PlayerPrefs.GetFloat(MouseSensitivityKey, 2f),
            SetMouseSensitivity
        );
        CreateSliderRow(settingsPanel.transform, "Interface", new Vector2(0f, -150f), 0.8f, 1.45f, DisplaySettingsManager.CurrentUiScale, SetInterfaceScale);
        settingsSessionInfoText = CreateInfoBlock(settingsPanel.transform, new Vector2(0f, -285f));
        UpdateSessionInfo();
        CreateButton(settingsPanel.transform, "BackButton", "Voltar", new Vector2(0f, -340f), new Color(0.72f, 0.56f, 0.18f, 1f), ShowMainPanel);
    }

    GameObject CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject panelObject = CreateUiObject(name, parent);
        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panelObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.11f, 0.08f, 0.96f);

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.94f, 0.83f, 0.42f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        return panelObject;
    }

    void CreateTitle(Transform parent, string label)
    {
        GameObject titleObject = CreateUiObject("Title", parent);
        RectTransform rect = titleObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 190f);
        rect.sizeDelta = new Vector2(460f, 80f);

        TextMeshProUGUI text = titleObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 52f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.96f, 0.82f, 1f);
    }

    void CreateSliderRow(Transform parent, string label, Vector2 anchoredPosition, float minValue, float maxValue, float initialValue, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject rowObject = CreateUiObject($"{label}Row", parent);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = anchoredPosition;
        rowRect.sizeDelta = new Vector2(520f, 90f);

        GameObject labelObject = CreateUiObject($"{label}Label", rowObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 22f);
        labelRect.sizeDelta = new Vector2(260f, 36f);

        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.fontSize = 28f;
        labelText.color = Color.white;

        GameObject sliderObject = CreateUiObject($"{label}Slider", rowObject.transform);
        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, -14f);
        sliderRect.sizeDelta = new Vector2(520f, 30f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = initialValue;
        slider.direction = Slider.Direction.LeftToRight;

        GameObject backgroundObject = CreateUiObject("Background", sliderObject.transform);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.25f);
        backgroundRect.anchorMax = new Vector2(1f, 0.75f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        GameObject fillAreaObject = CreateUiObject("Fill Area", sliderObject.transform);
        RectTransform fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(10f, 0f);
        fillAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject fillObject = CreateUiObject("Fill", fillAreaObject.transform);
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = new Color(0.72f, 0.56f, 0.18f, 1f);

        GameObject handleSlideAreaObject = CreateUiObject("Handle Slide Area", sliderObject.transform);
        RectTransform handleSlideAreaRect = handleSlideAreaObject.AddComponent<RectTransform>();
        handleSlideAreaRect.anchorMin = Vector2.zero;
        handleSlideAreaRect.anchorMax = Vector2.one;
        handleSlideAreaRect.offsetMin = new Vector2(10f, 0f);
        handleSlideAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObject = CreateUiObject("Handle", handleSlideAreaObject.transform);
        RectTransform handleRect = handleObject.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(26f, 26f);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(0.96f, 0.87f, 0.62f, 1f);

        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.onValueChanged.AddListener(onChanged);
    }

    TextMeshProUGUI CreateInfoBlock(Transform parent, Vector2 anchoredPosition)
    {
        GameObject infoObject = CreateUiObject("SessionInfo", parent);
        RectTransform infoRect = infoObject.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoRect.pivot = new Vector2(0.5f, 0.5f);
        infoRect.anchoredPosition = anchoredPosition;
        infoRect.sizeDelta = new Vector2(540f, 120f);

        TextMeshProUGUI infoText = infoObject.AddComponent<TextMeshProUGUI>();
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.fontSize = 24f;
        infoText.color = new Color(0.84f, 0.9f, 0.98f, 1f);
        infoText.text = "Sessao: Solo\nID: -\nIP do host: -\nPorta: -";
        return infoText;
    }

    void UpdateSessionInfo()
    {
        if (mainSessionInfoText == null && settingsSessionInfoText == null)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager == null || !manager.IsMultiplayerActive)
        {
            SetSessionInfoText("Sessao: Solo\nID: -\nIP do host: -\nPorta: -");
            return;
        }

        string sessionLabel = manager.Mode == LanMultiplayerManager.SessionMode.Host ? "Host LAN" : "Cliente LAN";
        string hostAddress = string.IsNullOrWhiteSpace(manager.CurrentAddress) ? "-" : manager.CurrentAddress;
        string sessionId = string.IsNullOrWhiteSpace(manager.SessionId) ? "-" : manager.SessionId;
        SetSessionInfoText($"Sessao: {sessionLabel}\nID: {sessionId}\nIP do host: {hostAddress}\nPorta: {manager.CurrentPort}");
    }

    void SetSessionInfoText(string text)
    {
        if (mainSessionInfoText != null)
            mainSessionInfoText.text = text;

        if (settingsSessionInfoText != null)
            settingsSessionInfoText.text = text;
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Color buttonColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(360f, 88f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        GameObject buttonTextObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform buttonTextRect = buttonTextObject.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonText = buttonTextObject.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontSize = 36f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = new Color(0.12f, 0.08f, 0.02f, 1f);

        return button;
    }

    GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<CanvasRenderer>();
        return obj;
    }

    void SetMenuVisible(bool visible)
    {
        if (overlayObject != null)
            overlayObject.SetActive(visible);
    }

    void HandleResetPlayerPressed()
    {
        if (SaveGameManager.Instance == null)
            return;

        if (Time.unscaledTime > resetConfirmUntil)
        {
            resetConfirmUntil = Time.unscaledTime + ResetConfirmDuration;
            SetResetButtonLabel("Confirmar reinicio");
            return;
        }

        if (SaveGameManager.Instance.ResetCurrentPlayerProgress(true))
            ResumeGame();
        else
            MessageSystem.Instance?.ShowMessage("Nao foi possivel reiniciar o personagem.");
    }

    void UpdateResetConfirmationState()
    {
        if (resetConfirmUntil <= 0f || Time.unscaledTime <= resetConfirmUntil)
            return;

        ClearResetConfirmation();
    }

    void ClearResetConfirmation()
    {
        resetConfirmUntil = 0f;
        SetResetButtonLabel("Reiniciar personagem");
    }

    void SetResetButtonLabel(string label)
    {
        if (resetPlayerButtonText != null)
            resetPlayerButtonText.text = label;
    }

    void OnDestroy()
    {
        if (GameState.IsPaused)
        {
            GameState.IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
