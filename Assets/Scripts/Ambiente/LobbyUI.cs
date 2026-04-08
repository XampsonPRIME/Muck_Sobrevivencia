using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    Canvas canvas;
    SaveGameManager saveGameManager;
    GraphicRaycaster graphicRaycaster;
    TMP_InputField addressInput;
    TMP_InputField portInput;
    TextMeshProUGUI statusText;
    bool waitingForSession;

    void Start()
    {
        saveGameManager = FindFirstObjectByType<SaveGameManager>();
        if (saveGameManager == null)
        {
            GameObject managerObject = new GameObject("SaveGameManager");
            saveGameManager = managerObject.AddComponent<SaveGameManager>();
        }

        EnsureEventSystem();
        BuildUI();
        EnterLobby();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("LobbyCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        graphicRaycaster = canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObject = CreateUiObject("Overlay", canvas.transform);
        RectTransform overlayRect = overlayObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);

        GameObject titleObject = CreateUiObject("Title", overlayObject.transform);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(900f, 120f);
        titleRect.anchoredPosition = new Vector2(0f, 140f);

        TextMeshProUGUI titleText = titleObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "Marped Survivor";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 72f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.96f, 0.82f, 1f);

        GameObject subtitleObject = CreateUiObject("Subtitle", overlayObject.transform);
        RectTransform subtitleRect = subtitleObject.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.sizeDelta = new Vector2(920f, 100f);
        subtitleRect.anchoredPosition = new Vector2(0f, 56f);

        TextMeshProUGUI subtitleText = subtitleObject.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "Sobreviva, evolua e enfrente criaturas cada vez mais fortes.";
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.fontSize = 30f;
        subtitleText.color = new Color(0.84f, 0.9f, 0.98f, 1f);

        bool hasSave = saveGameManager != null && saveGameManager.HasSave();

        if (hasSave)
        {
            CreateButton(
                overlayObject.transform,
                "ContinueButton",
                "Continuar solo",
                new Vector2(0f, -12f),
                new Color(0.72f, 0.56f, 0.18f, 1f),
                ContinueGame
            );

            CreateButton(
                overlayObject.transform,
                "NewGameButton",
                "Novo solo",
                new Vector2(0f, -126f),
                new Color(0.34f, 0.54f, 0.78f, 1f),
                StartGame
            );
        }
        else
        {
            CreateButton(
                overlayObject.transform,
                "StartButton",
                "Jogar solo",
                new Vector2(0f, -12f),
                new Color(0.72f, 0.56f, 0.18f, 1f),
                StartGame
            );
        }

        CreateSectionLabel(overlayObject.transform, "Multiplayer LAN", new Vector2(0f, -210f), 36f, new Color(0.97f, 0.88f, 0.7f, 1f));

        CreateButton(
            overlayObject.transform,
            "HostButton",
            "Hospedar",
            new Vector2(-190f, -430f),
            new Color(0.34f, 0.66f, 0.48f, 1f),
            HostGame
        );

        CreateButton(
            overlayObject.transform,
            "JoinButton",
            "Entrar",
            new Vector2(190f, -430f),
            new Color(0.31f, 0.49f, 0.8f, 1f),
            JoinGame
        );

        CreateSectionLabel(overlayObject.transform, "IP do host", new Vector2(-210f, -280f), 24f, new Color(0.78f, 0.86f, 0.95f, 1f));
        CreateSectionLabel(overlayObject.transform, "Porta", new Vector2(230f, -280f), 24f, new Color(0.78f, 0.86f, 0.95f, 1f));

        try
        {
            addressInput = CreateInputField(
                overlayObject.transform,
                "AddressInput",
                "127.0.0.1",
                new Vector2(-120f, -340f),
                new Vector2(460f, 74f),
                TMP_InputField.ContentType.Standard
            );
            addressInput.text = "127.0.0.1";

            portInput = CreateInputField(
                overlayObject.transform,
                "PortInput",
                "7777",
                new Vector2(300f, -340f),
                new Vector2(160f, 74f),
                TMP_InputField.ContentType.IntegerNumber
            );
            portInput.text = "7777";
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Falha ao criar campos do lobby multiplayer: {exception.Message}");
            addressInput = null;
            portInput = null;
        }

        GameObject statusObject = CreateUiObject("StatusText", overlayObject.transform);
        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.sizeDelta = new Vector2(1280f, 200f);
        statusRect.anchoredPosition = new Vector2(0f, -560f);

        statusText = statusObject.AddComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 22f;
        statusText.color = new Color(0.76f, 0.82f, 0.9f, 1f);
        statusText.textWrappingMode = TextWrappingModes.Normal;
        statusText.overflowMode = TextOverflowModes.Overflow;
        statusText.text = "Solo";
    }

    void EnterLobby()
    {
        GameState.IsInLobby = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (GameState.IsInLobby)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (statusText != null && LanMultiplayerManager.Instance != null)
        {
            statusText.text = LanMultiplayerManager.Instance.StatusMessage;
            statusText.color = LanMultiplayerManager.Instance.State == LanMultiplayerManager.SessionState.Error
                ? new Color(1f, 0.55f, 0.55f, 1f)
                : new Color(0.76f, 0.82f, 0.9f, 1f);
        }

        if (waitingForSession && LanMultiplayerManager.Instance != null)
        {
            if (LanMultiplayerManager.Instance.IsSessionReady)
            {
                ExitLobby();
                Destroy(gameObject);
                return;
            }

            if (LanMultiplayerManager.Instance.State == LanMultiplayerManager.SessionState.Error)
                waitingForSession = false;
        }

        if (!GameState.IsInLobby || canvas == null || graphicRaycaster == null || EventSystem.current == null || Mouse.current == null)
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
            TMP_InputField inputField = results[i].gameObject.GetComponentInParent<TMP_InputField>();
            if (inputField != null)
            {
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
                inputField.ActivateInputField();
                return;
            }

            Button button = results[i].gameObject.GetComponentInParent<Button>();
            if (button == null || !button.interactable)
                continue;

            button.onClick.Invoke();
            break;
        }
    }

    void StartGame()
    {
        LanMultiplayerManager.Instance?.StartSolo();
        saveGameManager?.StartNewGame();
        Destroy(gameObject);
    }

    void ContinueGame()
    {
        LanMultiplayerManager.Instance?.StartSolo();
        if (saveGameManager != null && saveGameManager.ContinueFromSave())
            Destroy(gameObject);
    }

    void HostGame()
    {
        int port = ReadPort();
        if (LanMultiplayerManager.Instance == null)
            return;

        if (!LanMultiplayerManager.Instance.StartHost(port))
            return;

        ExitLobby();
        Destroy(gameObject);
    }

    void JoinGame()
    {
        int port = ReadPort();
        string address = addressInput != null && !string.IsNullOrWhiteSpace(addressInput.text)
            ? addressInput.text
            : "127.0.0.1";

        if (LanMultiplayerManager.Instance == null)
            return;

        if (!LanMultiplayerManager.Instance.StartClient(address, port))
            return;

        waitingForSession = true;
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Color buttonColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(360f, 96f);
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
        buttonText.fontSize = 40f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = new Color(0.12f, 0.08f, 0.02f, 1f);

        return button;
    }

    TMP_InputField CreateInputField(Transform parent, string objectName, string placeholder, Vector2 anchoredPosition, Vector2 size, TMP_InputField.ContentType contentType)
    {
        GameObject fieldObject = CreateUiObject(objectName, parent);
        RectTransform fieldRect = fieldObject.AddComponent<RectTransform>();
        fieldRect.anchorMin = new Vector2(0.5f, 0.5f);
        fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
        fieldRect.sizeDelta = size;
        fieldRect.anchoredPosition = anchoredPosition;

        Image fieldImage = fieldObject.AddComponent<Image>();
        fieldImage.color = new Color(0.12f, 0.15f, 0.2f, 0.96f);

        TMP_InputField inputField = fieldObject.AddComponent<TMP_InputField>();
        inputField.contentType = contentType;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.selectionColor = new Color(0.65f, 0.8f, 1f, 0.35f);
        inputField.caretColor = new Color(0.94f, 0.97f, 1f, 1f);
        inputField.customCaretColor = true;

        GameObject viewportObject = CreateUiObject("Viewport", fieldObject.transform);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(18f, 8f);
        viewportRect.offsetMax = new Vector2(-18f, -8f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject textObject = CreateUiObject("Text", viewportObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject placeholderObject = CreateUiObject("Placeholder", viewportObject.transform);
        RectTransform placeholderRect = placeholderObject.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        TextMeshProUGUI placeholderText = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.fontSize = 28f;
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderText.color = new Color(0.63f, 0.7f, 0.78f, 0.85f);
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;

        inputField.textViewport = viewportRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        return inputField;
    }

    void CreateSectionLabel(Transform parent, string label, Vector2 anchoredPosition, float fontSize, Color color)
    {
        GameObject labelObject = CreateUiObject($"{label}Label", parent);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(780f, 54f);
        labelRect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = color;
    }

    int ReadPort()
    {
        if (portInput != null && int.TryParse(portInput.text, out int port))
            return port;

        return 7777;
    }

    void ExitLobby()
    {
        GameState.IsInLobby = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<CanvasRenderer>();
        return obj;
    }

    void OnDestroy()
    {
        if (GameState.IsInLobby)
            Time.timeScale = 1f;
    }
}
