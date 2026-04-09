using System.Collections.Generic;
using System.Net;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    const string LastAddressPrefKey = "lobby.last_address";
    const string LastPortPrefKey = "lobby.last_port";

    Canvas canvas;
    SaveGameManager saveGameManager;
    GraphicRaycaster graphicRaycaster;
    TMP_InputField addressInput;
    TMP_InputField portInput;
    TextMeshProUGUI statusText;
    TextMeshProUGUI discoveryText;
    Button primarySoloButton;
    Button newSoloButton;
    Button openMultiplayerButton;
    Button closeMultiplayerButton;
    Button hostButton;
    Button continueSessionButton;
    Button joinButton;
    GameObject mainMenuRoot;
    GameObject multiplayerPopupBackdrop;
    GameObject multiplayerPopupPanel;
    LobbyUIViewRefs viewRefs;
    bool waitingForSession;
    string autoSelectedSessionId;
    string lobbyErrorMessage;
    bool hasDetectedJoinSession;
    float nextDiscoveryRefreshTime;

    void Start()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.IsSessionReady && manager.IsMultiplayerActive)
        {
            ExitLobby();
            Destroy(gameObject);
            return;
        }

        saveGameManager = FindFirstObjectByType<SaveGameManager>();
        if (saveGameManager == null)
        {
            GameObject managerObject = new GameObject("SaveGameManager");
            saveGameManager = managerObject.AddComponent<SaveGameManager>();
        }

        EnsureEventSystem();
        if (!TryBindHierarchyUi())
            BuildUI();

        RefreshPrimarySoloButton();
        RefreshContinueSessionButton();
        RefreshJoinButton();
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
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);
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

        mainMenuRoot = CreateUiObject("MainMenuRoot", overlayObject.transform);
        RectTransform mainMenuRect = mainMenuRoot.AddComponent<RectTransform>();
        mainMenuRect.anchorMin = Vector2.zero;
        mainMenuRect.anchorMax = Vector2.one;
        mainMenuRect.offsetMin = Vector2.zero;
        mainMenuRect.offsetMax = Vector2.zero;

        primarySoloButton = CreateButton(
            mainMenuRoot.transform,
            "PrimarySoloButton",
            "Jogar solo",
            new Vector2(0f, -12f),
            new Color(0.72f, 0.56f, 0.18f, 1f),
            OnPrimarySoloClicked
        );

        newSoloButton = CreateButton(
            mainMenuRoot.transform,
            "NewGameButton",
            "Novo solo",
            new Vector2(0f, -126f),
            new Color(0.34f, 0.54f, 0.78f, 1f),
            StartGame
        );

        openMultiplayerButton = CreateButton(
            mainMenuRoot.transform,
            "OpenMultiplayerButton",
            "Ver sessoes online",
            new Vector2(0f, -240f),
            new Color(0.3f, 0.52f, 0.76f, 1f),
            OpenMultiplayerPopup
        );

        CreateSectionLabel(mainMenuRoot.transform, "Entre por descoberta automatica na LAN ou abra uma nova sessao host.", new Vector2(0f, -316f), 22f, new Color(0.78f, 0.86f, 0.95f, 1f));

        multiplayerPopupBackdrop = CreateUiObject("MultiplayerPopupBackdrop", overlayObject.transform);
        RectTransform popupBackdropRect = multiplayerPopupBackdrop.AddComponent<RectTransform>();
        popupBackdropRect.anchorMin = Vector2.zero;
        popupBackdropRect.anchorMax = Vector2.one;
        popupBackdropRect.offsetMin = Vector2.zero;
        popupBackdropRect.offsetMax = Vector2.zero;
        Image popupBackdropImage = multiplayerPopupBackdrop.AddComponent<Image>();
        popupBackdropImage.color = new Color(0.01f, 0.02f, 0.04f, 0.72f);

        multiplayerPopupPanel = CreateUiObject("MultiplayerPopupPanel", multiplayerPopupBackdrop.transform);
        RectTransform popupPanelRect = multiplayerPopupPanel.AddComponent<RectTransform>();
        popupPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupPanelRect.sizeDelta = new Vector2(1340f, 640f);
        popupPanelRect.anchoredPosition = new Vector2(0f, -20f);
        Image popupPanelImage = multiplayerPopupPanel.AddComponent<Image>();
        popupPanelImage.color = new Color(0.08f, 0.1f, 0.15f, 0.98f);

        CreateSectionLabel(multiplayerPopupPanel.transform, "Sessoes Multiplayer", new Vector2(0f, 250f), 40f, new Color(0.98f, 0.9f, 0.74f, 1f));
        CreateSectionLabel(multiplayerPopupPanel.transform, "Mesma rede local: a sessao aparece aqui automaticamente. Tailscale/internet: informe o IP manualmente.", new Vector2(0f, 208f), 20f, new Color(0.72f, 0.82f, 0.92f, 1f));

        closeMultiplayerButton = CreateButton(
            multiplayerPopupPanel.transform,
            "ClosePopupButton",
            "Voltar",
            new Vector2(500f, 248f),
            new Color(0.38f, 0.42f, 0.52f, 1f),
            CloseMultiplayerPopup
        );
        RectTransform closeRect = closeMultiplayerButton.GetComponent<RectTransform>();
        if (closeRect != null)
            closeRect.sizeDelta = new Vector2(220f, 74f);

        CreateSectionLabel(multiplayerPopupPanel.transform, "Sessoes encontradas", new Vector2(0f, 142f), 26f, new Color(0.94f, 0.97f, 1f, 1f));

        GameObject discoveryPanelObject = CreateUiObject("DiscoveryPanel", multiplayerPopupPanel.transform);
        RectTransform discoveryPanelRect = discoveryPanelObject.AddComponent<RectTransform>();
        discoveryPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        discoveryPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        discoveryPanelRect.sizeDelta = new Vector2(1220f, 170f);
        discoveryPanelRect.anchoredPosition = new Vector2(0f, 42f);
        Image discoveryPanelImage = discoveryPanelObject.AddComponent<Image>();
        discoveryPanelImage.color = new Color(0.11f, 0.15f, 0.21f, 0.98f);

        GameObject discoveryObject = CreateUiObject("DiscoveryText", discoveryPanelObject.transform);
        RectTransform discoveryRect = discoveryObject.AddComponent<RectTransform>();
        discoveryRect.anchorMin = new Vector2(0.5f, 0.5f);
        discoveryRect.anchorMax = new Vector2(0.5f, 0.5f);
        discoveryRect.sizeDelta = new Vector2(1160f, 142f);
        discoveryRect.anchoredPosition = Vector2.zero;

        discoveryText = discoveryObject.AddComponent<TextMeshProUGUI>();
        discoveryText.alignment = TextAlignmentOptions.Center;
        discoveryText.fontSize = 24f;
        discoveryText.color = new Color(0.86f, 0.92f, 0.98f, 1f);
        discoveryText.textWrappingMode = TextWrappingModes.Normal;
        discoveryText.overflowMode = TextOverflowModes.Overflow;
        discoveryText.text = "Buscando sessoes LAN automaticamente...";

        hostButton = CreateButton(
            multiplayerPopupPanel.transform,
            "HostButton",
            "Hospedar",
            new Vector2(-380f, -106f),
            new Color(0.34f, 0.66f, 0.48f, 1f),
            HostGame
        );

        continueSessionButton = CreateButton(
            multiplayerPopupPanel.transform,
            "ContinueSessionButton",
            "Continuar sessao",
            new Vector2(0f, -106f),
            new Color(0.66f, 0.58f, 0.28f, 1f),
            ContinueHostedSession
        );

        joinButton = CreateButton(
            multiplayerPopupPanel.transform,
            "JoinButton",
            "Entrar",
            new Vector2(380f, -106f),
            new Color(0.31f, 0.49f, 0.8f, 1f),
            JoinGame
        );

        CreateSectionLabel(multiplayerPopupPanel.transform, "IP do host", new Vector2(-220f, -192f), 24f, new Color(0.78f, 0.86f, 0.95f, 1f));
        CreateSectionLabel(multiplayerPopupPanel.transform, "Porta", new Vector2(230f, -192f), 24f, new Color(0.78f, 0.86f, 0.95f, 1f));

        string savedAddress = PlayerPrefs.GetString(LastAddressPrefKey, string.Empty);
        string savedPort = PlayerPrefs.GetString(LastPortPrefKey, "7777");

        try
        {
            addressInput = CreateInputField(
                multiplayerPopupPanel.transform,
                "AddressInput",
                "127.0.0.1",
                new Vector2(-120f, -242f),
                new Vector2(460f, 64f),
                TMP_InputField.ContentType.Standard
            );
            addressInput.text = savedAddress;

            portInput = CreateInputField(
                multiplayerPopupPanel.transform,
                "PortInput",
                savedPort,
                new Vector2(300f, -242f),
                new Vector2(160f, 64f),
                TMP_InputField.ContentType.IntegerNumber
            );
            portInput.text = savedPort;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Falha ao criar campos do lobby multiplayer: {exception.Message}");
            addressInput = null;
            portInput = null;
        }

        multiplayerPopupBackdrop.SetActive(false);

        GameObject statusObject = CreateUiObject("StatusText", overlayObject.transform);
        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.sizeDelta = new Vector2(1280f, 90f);
        statusRect.anchoredPosition = new Vector2(0f, 40f);

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
            if (!string.IsNullOrWhiteSpace(lobbyErrorMessage))
            {
                statusText.text = lobbyErrorMessage;
                statusText.color = new Color(1f, 0.55f, 0.55f, 1f);
            }
            else
            {
                statusText.text = LanMultiplayerManager.Instance.StatusMessage;
                statusText.color = LanMultiplayerManager.Instance.State == LanMultiplayerManager.SessionState.Error
                    ? new Color(1f, 0.55f, 0.55f, 1f)
                    : new Color(0.76f, 0.82f, 0.9f, 1f);
            }
        }

        UpdateDiscoveryInfo();
        RefreshContinueSessionButton();
        RefreshJoinButton();

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
        }

        if (EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);
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

    void OnPrimarySoloClicked()
    {
        if (saveGameManager != null && saveGameManager.HasSave())
            ContinueGame();
        else
            StartGame();
    }

    void OpenMultiplayerPopup()
    {
        if (multiplayerPopupBackdrop != null)
            multiplayerPopupBackdrop.SetActive(true);
    }

    void CloseMultiplayerPopup()
    {
        if (multiplayerPopupBackdrop != null)
            multiplayerPopupBackdrop.SetActive(false);
    }

    void HostGame()
    {
        int port = ReadPort();
        if (LanMultiplayerManager.Instance == null)
            return;

        if (!LanMultiplayerManager.Instance.StartHost(port))
            return;

        SaveLastConnection(null, port);

        CloseMultiplayerPopup();
        ExitLobby();
        Destroy(gameObject);
    }

    void ContinueHostedSession()
    {
        int port = ReadPort();
        if (saveGameManager == null)
            return;

        if (!saveGameManager.ContinueMultiplayerSession(port))
            return;

        SaveLastConnection(null, port);
        CloseMultiplayerPopup();
        Destroy(gameObject);
    }

    void JoinGame()
    {
        if (LanMultiplayerManager.Instance == null)
            return;

        string address = addressInput != null ? addressInput.text.Trim() : string.Empty;
        if (!TryReadJoinPort(out int port))
            return;

        if (!ValidateJoinAddress(address))
            return;

        if (!LanMultiplayerManager.Instance.StartClient(address, port))
            return;

        lobbyErrorMessage = null;
        SetButtonEnabled(joinButton, false);
        SaveLastConnection(address, port);
        waitingForSession = true;
    }

    void SaveLastConnection(string address, int port)
    {
        if (!string.IsNullOrWhiteSpace(address))
            PlayerPrefs.SetString(LastAddressPrefKey, address.Trim());

        PlayerPrefs.SetString(LastPortPrefKey, Mathf.Max(1, port).ToString());
        PlayerPrefs.Save();
    }

    void UpdateDiscoveryInfo()
    {
        if (discoveryText == null || Time.unscaledTime < nextDiscoveryRefreshTime)
            return;

        nextDiscoveryRefreshTime = Time.unscaledTime + 0.5f;

        LanSessionDiscovery discovery = LanSessionDiscovery.Instance;
        if (discovery == null)
        {
            discoveryText.text = "Descoberta LAN indisponivel.";
            return;
        }

        List<LanDiscoveredSession> sessions = discovery.GetSessions();
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        string currentSessionId = manager != null ? manager.SessionId : null;

        sessions.RemoveAll(session =>
            session == null ||
            string.IsNullOrWhiteSpace(session.address) ||
            (manager != null &&
             manager.Mode == LanMultiplayerManager.SessionMode.Host &&
             !string.IsNullOrWhiteSpace(currentSessionId) &&
             session.sessionId == currentSessionId));

        if (sessions.Count == 0)
        {
            hasDetectedJoinSession = false;
            discoveryText.text = "Sessoes LAN detectadas: nenhuma no momento. Isso funciona automaticamente na mesma rede local.";
            autoSelectedSessionId = null;
            return;
        }

        hasDetectedJoinSession = true;
        LanDiscoveredSession preferredSession = sessions[0];
        if (CanAutoFillSession(preferredSession))
        {
            addressInput.text = preferredSession.address;
            portInput.text = preferredSession.port.ToString();
            autoSelectedSessionId = preferredSession.sessionId;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("Sessoes LAN detectadas: ");

        int visibleCount = Mathf.Min(3, sessions.Count);
        for (int i = 0; i < visibleCount; i++)
        {
            LanDiscoveredSession session = sessions[i];
            if (i > 0)
                builder.Append(" | ");

            builder.Append(session.hostName);
            builder.Append(" [");
            builder.Append(session.sessionId);
            builder.Append("] ");
            builder.Append(session.address);
            builder.Append(':');
            builder.Append(session.port);

            if (!string.IsNullOrWhiteSpace(session.sceneName))
            {
                builder.Append(" - ");
                builder.Append(session.sceneName);
            }

            builder.Append(" - ");
            builder.Append(session.playerCount);
            builder.Append(session.playerCount == 1 ? " jogador" : " jogadores");
        }

        if (sessions.Count > visibleCount)
        {
            builder.Append(" | +");
            builder.Append(sessions.Count - visibleCount);
            builder.Append(" sessoes");
        }

        discoveryText.text = builder.ToString();
    }

    void RefreshContinueSessionButton()
    {
        if (continueSessionButton == null || saveGameManager == null)
            return;

        bool hasSessionSave = saveGameManager.HasMultiplayerSessionSave();
        SetButtonEnabled(continueSessionButton, hasSessionSave);

        TextMeshProUGUI buttonText = continueSessionButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.text = hasSessionSave ? "Continuar sessao" : "Sem sessao salva";

        TextMeshProUGUI hostText = hostButton != null ? hostButton.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (hostText != null)
            hostText.text = hasSessionSave ? "Novo host" : "Hospedar";
    }

    void RefreshPrimarySoloButton()
    {
        bool hasSave = saveGameManager != null && saveGameManager.HasSave();

        TextMeshProUGUI primaryText = primarySoloButton != null ? primarySoloButton.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (primaryText != null)
            primaryText.text = hasSave ? "Continuar solo" : "Jogar solo";

        if (newSoloButton != null)
            newSoloButton.gameObject.SetActive(hasSave);
    }

    void SetButtonEnabled(Button button, bool enabled)
    {
        if (button == null)
            return;

        button.interactable = enabled;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = enabled ? 1f : 0.38f;
            image.color = color;
        }

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.color = enabled ? new Color(0.12f, 0.08f, 0.02f, 1f) : new Color(0.22f, 0.22f, 0.22f, 0.88f);
    }

    bool TryBindHierarchyUi()
    {
        viewRefs = GetComponent<LobbyUIViewRefs>();
        if (viewRefs == null || !viewRefs.IsConfigured())
            return false;

        canvas = viewRefs.canvas;
        graphicRaycaster = viewRefs.graphicRaycaster;
        mainMenuRoot = viewRefs.mainMenuRoot;
        multiplayerPopupBackdrop = viewRefs.multiplayerPopupBackdrop;
        multiplayerPopupPanel = viewRefs.multiplayerPopupPanel;
        primarySoloButton = viewRefs.primarySoloButton;
        newSoloButton = viewRefs.newSoloButton;
        openMultiplayerButton = viewRefs.openMultiplayerButton;
        closeMultiplayerButton = viewRefs.closeMultiplayerButton;
        hostButton = viewRefs.hostButton;
        continueSessionButton = viewRefs.continueSessionButton;
        joinButton = viewRefs.joinButton;
        addressInput = viewRefs.addressInput;
        portInput = viewRefs.portInput;
        statusText = viewRefs.statusText;
        discoveryText = viewRefs.discoveryText;

        WireButton(primarySoloButton, OnPrimarySoloClicked);
        WireButton(newSoloButton, StartGame);
        WireButton(openMultiplayerButton, OpenMultiplayerPopup);
        WireButton(closeMultiplayerButton, CloseMultiplayerPopup);
        WireButton(hostButton, HostGame);
        WireButton(continueSessionButton, ContinueHostedSession);
        WireButton(joinButton, JoinGame);

        string savedAddress = PlayerPrefs.GetString(LastAddressPrefKey, string.Empty);
        string savedPort = PlayerPrefs.GetString(LastPortPrefKey, "7777");

        if (addressInput != null && string.IsNullOrWhiteSpace(addressInput.text))
            addressInput.text = savedAddress;

        if (portInput != null && string.IsNullOrWhiteSpace(portInput.text))
            portInput.text = savedPort;

        if (multiplayerPopupBackdrop != null)
            multiplayerPopupBackdrop.SetActive(false);

        return true;
    }

    void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void RefreshJoinButton()
    {
        if (joinButton == null)
            return;

        bool canJoin = IsJoinFormValid();
        SetButtonEnabled(joinButton, canJoin && !waitingForSession);
    }

    bool IsJoinFormValid()
    {
        string address = addressInput != null ? addressInput.text.Trim() : string.Empty;

        if (!TryGetPortValue(out int port))
            return false;

        if (port <= 0 || port > 65535)
            return false;

        if (hasDetectedJoinSession && !string.IsNullOrWhiteSpace(address) && IPAddress.TryParse(address, out _))
            return true;

        return !string.IsNullOrWhiteSpace(address) && IPAddress.TryParse(address, out _);
    }

    bool CanAutoFillSession(LanDiscoveredSession session)
    {
        if (session == null || addressInput == null || portInput == null)
            return false;

        if (EventSystem.current != null)
        {
            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
            if (selectedObject == addressInput.gameObject || selectedObject == portInput.gameObject)
                return false;
        }

        string currentAddress = addressInput.text != null ? addressInput.text.Trim() : string.Empty;
        bool looksDefault = string.IsNullOrWhiteSpace(currentAddress) || currentAddress == "127.0.0.1";
        bool alreadyAutoFilled = !string.IsNullOrWhiteSpace(autoSelectedSessionId);

        if (looksDefault)
            return true;

        return alreadyAutoFilled && autoSelectedSessionId != session.sessionId;
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

    bool TryReadJoinPort(out int port)
    {
        port = 0;

        if (!TryGetPortValue(out port) || port <= 0 || port > 65535)
        {
            SetLobbyError("Informe uma porta valida antes de entrar.");
            return false;
        }

        return true;
    }

    bool ValidateJoinAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            SetLobbyError("Selecione uma sessao encontrada ou informe o IP do host.");
            return false;
        }

        if (!IPAddress.TryParse(address, out _))
        {
            SetLobbyError("Informe um IP valido para entrar na sessao.");
            return false;
        }

        return true;
    }

    bool TryGetPortValue(out int port)
    {
        port = 0;
        return portInput != null &&
               !string.IsNullOrWhiteSpace(portInput.text) &&
               int.TryParse(portInput.text.Trim(), out port);
    }

    void SetLobbyError(string message)
    {
        lobbyErrorMessage = message;

        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = new Color(1f, 0.55f, 0.55f, 1f);
        }

        if (LanMultiplayerManager.Instance != null)
            return;

        Debug.LogWarning($"[LobbyUI] {message}");
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
