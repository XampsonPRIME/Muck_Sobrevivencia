using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class LobbyPrefabFactory
{
    const string PrefabFolderPath = "Assets/Prefabs/UI";
    const string PrefabPath = "Assets/Prefabs/UI/LobbyUI.prefab";

    [MenuItem("Tools/Marped/Create Lobby Prefab")]
    public static void CreateLobbyPrefab()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "UI");

        GameObject root = new GameObject("LobbyUI");
        root.AddComponent<LobbyUI>();
        LobbyUIViewRefs refs = root.AddComponent<LobbyUIViewRefs>();

        GameObject canvasObject = CreateUiObject("LobbyCanvas", root.transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        GraphicRaycaster graphicRaycaster = canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObject = CreateUiObject("Overlay", canvasObject.transform);
        Stretch(overlayObject);
        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);

        CreateLabel(overlayObject.transform, "Title", "Marped Survivor", new Vector2(900f, 120f), new Vector2(0f, 140f), 72f, new Color(1f, 0.96f, 0.82f, 1f), FontStyles.Bold);
        CreateLabel(overlayObject.transform, "Subtitle", "Sobreviva, evolua e enfrente criaturas cada vez mais fortes.", new Vector2(920f, 100f), new Vector2(0f, 56f), 30f, new Color(0.84f, 0.9f, 0.98f, 1f), FontStyles.Normal);

        GameObject mainMenuRoot = CreateUiObject("MainMenuRoot", overlayObject.transform);
        Stretch(mainMenuRoot);

        Button primarySoloButton = CreateButton(mainMenuRoot.transform, "PrimarySoloButton", "Jogar solo", new Vector2(0f, -12f), new Vector2(360f, 96f), new Color(0.72f, 0.56f, 0.18f, 1f));
        Button newSoloButton = CreateButton(mainMenuRoot.transform, "NewGameButton", "Novo solo", new Vector2(0f, -126f), new Vector2(360f, 96f), new Color(0.34f, 0.54f, 0.78f, 1f));
        Button openMultiplayerButton = CreateButton(mainMenuRoot.transform, "OpenMultiplayerButton", "Ver sessoes online", new Vector2(0f, -240f), new Vector2(420f, 96f), new Color(0.3f, 0.52f, 0.76f, 1f));
        CreateLabel(mainMenuRoot.transform, "MainHint", "Entre por descoberta automatica na LAN ou abra uma nova sessao host.", new Vector2(1080f, 60f), new Vector2(0f, -316f), 22f, new Color(0.78f, 0.86f, 0.95f, 1f), FontStyles.Normal);

        GameObject popupBackdrop = CreateUiObject("MultiplayerPopupBackdrop", overlayObject.transform);
        Stretch(popupBackdrop);
        Image popupBackdropImage = popupBackdrop.AddComponent<Image>();
        popupBackdropImage.color = new Color(0.01f, 0.02f, 0.04f, 0.72f);

        GameObject popupPanel = CreateUiObject("MultiplayerPopupPanel", popupBackdrop.transform);
        RectTransform popupPanelRect = popupPanel.AddComponent<RectTransform>();
        popupPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupPanelRect.sizeDelta = new Vector2(1340f, 640f);
        popupPanelRect.anchoredPosition = new Vector2(0f, -20f);
        Image popupPanelImage = popupPanel.AddComponent<Image>();
        popupPanelImage.color = new Color(0.08f, 0.1f, 0.15f, 0.98f);

        CreateLabel(popupPanel.transform, "PopupTitle", "Sessoes Multiplayer", new Vector2(1000f, 70f), new Vector2(0f, 250f), 40f, new Color(0.98f, 0.9f, 0.74f, 1f), FontStyles.Bold);
        CreateLabel(popupPanel.transform, "PopupHint", "Mesma rede local: a sessao aparece aqui automaticamente. Tailscale/internet: informe o IP manualmente.", new Vector2(1140f, 60f), new Vector2(0f, 208f), 20f, new Color(0.72f, 0.82f, 0.92f, 1f), FontStyles.Normal);

        Button closePopupButton = CreateButton(popupPanel.transform, "ClosePopupButton", "Voltar", new Vector2(500f, 248f), new Vector2(220f, 74f), new Color(0.38f, 0.42f, 0.52f, 1f));
        CreateLabel(popupPanel.transform, "SessionsLabel", "Sessoes encontradas", new Vector2(700f, 50f), new Vector2(0f, 142f), 26f, new Color(0.94f, 0.97f, 1f, 1f), FontStyles.Bold);

        GameObject discoveryPanel = CreateUiObject("DiscoveryPanel", popupPanel.transform);
        RectTransform discoveryPanelRect = discoveryPanel.AddComponent<RectTransform>();
        discoveryPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        discoveryPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        discoveryPanelRect.sizeDelta = new Vector2(1220f, 170f);
        discoveryPanelRect.anchoredPosition = new Vector2(0f, 42f);
        Image discoveryPanelImage = discoveryPanel.AddComponent<Image>();
        discoveryPanelImage.color = new Color(0.11f, 0.15f, 0.21f, 0.98f);

        TextMeshProUGUI discoveryText = CreateLabel(discoveryPanel.transform, "DiscoveryText", "Buscando sessoes LAN automaticamente...", new Vector2(1160f, 142f), Vector2.zero, 24f, new Color(0.86f, 0.92f, 0.98f, 1f), FontStyles.Normal);

        Button hostButton = CreateButton(popupPanel.transform, "HostButton", "Hospedar", new Vector2(-380f, -106f), new Vector2(360f, 96f), new Color(0.34f, 0.66f, 0.48f, 1f));
        Button continueSessionButton = CreateButton(popupPanel.transform, "ContinueSessionButton", "Continuar sessao", new Vector2(0f, -106f), new Vector2(360f, 96f), new Color(0.66f, 0.58f, 0.28f, 1f));
        Button joinButton = CreateButton(popupPanel.transform, "JoinButton", "Entrar", new Vector2(380f, -106f), new Vector2(360f, 96f), new Color(0.31f, 0.49f, 0.8f, 1f));

        CreateLabel(popupPanel.transform, "AddressLabel", "IP do host", new Vector2(420f, 42f), new Vector2(-220f, -192f), 24f, new Color(0.78f, 0.86f, 0.95f, 1f), FontStyles.Normal);
        CreateLabel(popupPanel.transform, "PortLabel", "Porta", new Vector2(220f, 42f), new Vector2(230f, -192f), 24f, new Color(0.78f, 0.86f, 0.95f, 1f), FontStyles.Normal);

        TMP_InputField addressInput = CreateInputField(popupPanel.transform, "AddressInput", "127.0.0.1", new Vector2(-120f, -242f), new Vector2(460f, 64f), TMP_InputField.ContentType.Standard);
        TMP_InputField portInput = CreateInputField(popupPanel.transform, "PortInput", "7777", new Vector2(300f, -242f), new Vector2(160f, 64f), TMP_InputField.ContentType.IntegerNumber);

        TextMeshProUGUI statusText = CreateLabel(overlayObject.transform, "StatusText", "Solo", new Vector2(1280f, 90f), new Vector2(0f, 40f), 22f, new Color(0.76f, 0.82f, 0.9f, 1f), FontStyles.Normal);
        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);

        popupBackdrop.SetActive(false);

        refs.canvas = canvas;
        refs.graphicRaycaster = graphicRaycaster;
        refs.mainMenuRoot = mainMenuRoot;
        refs.multiplayerPopupBackdrop = popupBackdrop;
        refs.multiplayerPopupPanel = popupPanel;
        refs.primarySoloButton = primarySoloButton;
        refs.newSoloButton = newSoloButton;
        refs.openMultiplayerButton = openMultiplayerButton;
        refs.closeMultiplayerButton = closePopupButton;
        refs.hostButton = hostButton;
        refs.continueSessionButton = continueSessionButton;
        refs.joinButton = joinButton;
        refs.addressInput = addressInput;
        refs.portInput = portInput;
        refs.statusText = statusText;
        refs.discoveryText = discoveryText;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        Debug.Log($"Lobby prefab criado em {PrefabPath}");
    }

    static void EnsureFolder(string parent, string child)
    {
        string target = $"{parent}/{child}";
        if (AssetDatabase.IsValidFolder(target))
            return;

        AssetDatabase.CreateFolder(parent, child);
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<CanvasRenderer>();
        return obj;
    }

    static void Stretch(GameObject gameObject)
    {
        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 size, Vector2 anchoredPosition, float fontSize, Color color, FontStyles fontStyle)
    {
        GameObject labelObject = CreateUiObject(name, parent);
        RectTransform rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.targetGraphic = image;

        CreateLabel(buttonObject.transform, "Text", label, size, Vector2.zero, 40f, new Color(0.12f, 0.08f, 0.02f, 1f), FontStyles.Bold);
        return button;
    }

    static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchoredPosition, Vector2 size, TMP_InputField.ContentType contentType)
    {
        GameObject fieldObject = CreateUiObject(name, parent);
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
}
