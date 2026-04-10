using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class VendorShopUI : MonoBehaviour
{
    public static VendorShopUI Instance { get; private set; }

    Canvas canvas;
    GameObject overlayObject;
    GameObject panelObject;
    TextMeshProUGUI vendorNameText;
    TextMeshProUGUI goldAmountText;
    Transform buyContent;
    Transform sellContent;
    InputAction closeAction;

    VendorShop currentVendor;
    Inventory currentInventory;
    Hotbar currentHotbar;
    PlayerMovement currentPlayerMovement;
    PlayerInteraction currentPlayerInteraction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<VendorShopUI>() != null)
            return;

        GameObject uiObject = new GameObject("VendorShopUI");
        uiObject.AddComponent<VendorShopUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        closeAction = new InputAction("CloseVendorShop", binding: "<Keyboard>/escape");
    }

    void OnEnable()
    {
        closeAction.Enable();
    }

    void OnDisable()
    {
        closeAction.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
        {
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();
        BuildUi();
        SetVisible(false);
    }

    void Update()
    {
        if (!GameState.IsVendorOpen)
            return;

        if (closeAction.WasPressedThisFrame())
        {
            Close();
            return;
        }

        RefreshGoldLabel();
    }

    public void Open(VendorShop vendor, Inventory inventory, Hotbar hotbar, PlayerMovement playerMovement, PlayerInteraction playerInteraction)
    {
        if (vendor == null || inventory == null)
            return;

        if (panelObject == null)
            BuildUi();

        currentVendor = vendor;
        currentInventory = inventory;
        currentHotbar = hotbar;
        currentPlayerMovement = playerMovement;
        currentPlayerInteraction = playerInteraction;

        GameState.IsInventoryOpen = false;
        GameState.IsVendorOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (currentPlayerMovement != null)
            currentPlayerMovement.enabled = false;

        if (currentPlayerInteraction != null)
            currentPlayerInteraction.enabled = false;

        SetVisible(true);
        Refresh();
    }

    public void Close()
    {
        GameState.IsVendorOpen = false;
        SetVisible(false);

        if (currentPlayerMovement != null && !GameState.IsPlayerDead && !GameState.IsPaused && !GameState.IsInLobby)
            currentPlayerMovement.enabled = true;

        if (currentPlayerInteraction != null && !GameState.IsPlayerDead && !GameState.IsPaused && !GameState.IsInLobby)
            currentPlayerInteraction.enabled = true;

        if (!GameState.IsPaused && !GameState.IsInventoryOpen && !GameState.IsInLobby)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void Refresh()
    {
        if (currentVendor == null || currentInventory == null)
            return;

        if (vendorNameText != null)
            vendorNameText.text = currentVendor.vendorName;

        RefreshGoldLabel();
        RebuildBuyList();
        RebuildSellList();

        InventoryUI inventoryUi = FindFirstObjectByType<InventoryUI>();
        if (inventoryUi != null)
            inventoryUi.Refresh();

        currentPlayerInteraction?.RefreshEquippedSelection();
    }

    void RefreshGoldLabel()
    {
        if (goldAmountText == null || currentInventory == null)
            return;

        goldAmountText.text = $"Gold: {currentInventory.GetGoldAmount()}";
    }

    void RebuildBuyList()
    {
        ClearChildren(buyContent);

        IReadOnlyList<VendorOffer> offers = currentVendor.GetOffers();
        bool createdAnyRow = false;
        for (int i = 0; i < offers.Count; i++)
        {
            VendorOffer offer = offers[i];
            if (offer == null || offer.itemPrefab == null)
                continue;

            int captureIndex = i;
            string stockLabel = offer.infiniteStock ? "Infinito" : offer.CurrentStock.ToString();
            string priceLabel = offer.itemPrefab.GetBuyPrice().ToString();
            bool canBuy = offer.HasStock;

            CreateTradeRow(
                buyContent,
                offer.itemPrefab.icon,
                offer.itemPrefab.itemName,
                $"Compra: {priceLabel} gold  |  Estoque: {stockLabel}",
                canBuy ? "Comprar" : "Esgotado",
                canBuy,
                () => HandleBuy(captureIndex)
            );
            createdAnyRow = true;
        }

        if (!createdAnyRow)
            CreateEmptyState(buyContent, "Esse vendedor ainda nao tem itens configurados.");
    }

    void RebuildSellList()
    {
        ClearChildren(sellContent);

        List<InventoryItem> items = currentInventory.items;
        bool createdAnyRow = false;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem inventoryItem = items[i];
            if (inventoryItem == null || inventoryItem.itemData == null || inventoryItem.itemName == "Gold")
                continue;

            InventoryItem capturedItem = inventoryItem;
            int sellPrice = capturedItem.GetSellPrice();
            bool canSell = sellPrice > 0 && capturedItem.quantity > 0;

            CreateTradeRow(
                sellContent,
                capturedItem.GetDisplayIcon(),
                capturedItem.itemName,
                $"Venda: {sellPrice} gold  |  Quantidade: {capturedItem.quantity}",
                canSell ? "Vender" : "Sem valor",
                canSell,
                () => HandleSell(capturedItem)
            );
            createdAnyRow = true;
        }

        if (!createdAnyRow)
            CreateEmptyState(sellContent, "Voce nao tem itens vendaveis agora.");
    }

    void HandleBuy(int offerIndex)
    {
        if (currentVendor == null || currentInventory == null)
            return;

        if (currentVendor.TryBuy(offerIndex, currentInventory, currentHotbar, out string message))
            MessageSystem.Instance?.ShowMessage(message);
        else
            MessageSystem.Instance?.ShowMessage(message);

        Refresh();
    }

    void HandleSell(InventoryItem inventoryItem)
    {
        if (currentVendor == null || currentInventory == null || inventoryItem == null)
            return;

        if (currentVendor.TrySell(currentInventory, currentHotbar, inventoryItem, out string message))
            MessageSystem.Instance?.ShowMessage(message);
        else
            MessageSystem.Instance?.ShowMessage(message);

        Refresh();
    }

    void BuildUi()
    {
        if (panelObject != null)
            return;

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        overlayObject = CreatePanel("VendorOverlay", transform, new Color(0f, 0f, 0f, 0.65f));
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        panelObject = CreatePanel("VendorPanel", overlayObject.transform, new Color(0.12f, 0.1f, 0.06f, 0.96f));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1120f, 700f);
        panelRect.anchoredPosition = Vector2.zero;

        // 🔥 LAYOUT PRINCIPAL
        VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(28, 28, 24, 16);
        rootLayout.spacing = 16f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter rootFitter = panelObject.AddComponent<ContentSizeFitter>();
        rootFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        vendorNameText = CreateText("VendorName", panelObject.transform, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        goldAmountText = CreateText("GoldAmount", panelObject.transform, 24, FontStyles.Bold, TextAlignmentOptions.Center);

        // 🔥 COLUNAS
        GameObject columns = new GameObject("Columns", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        columns.transform.SetParent(panelObject.transform, false);

        HorizontalLayoutGroup columnsLayout = columns.GetComponent<HorizontalLayoutGroup>();
        columnsLayout.spacing = 18f;
        columnsLayout.childControlWidth = true;
        columnsLayout.childControlHeight = true;
        columnsLayout.childForceExpandWidth = true;
        columnsLayout.childForceExpandHeight = true;

        LayoutElement columnsLayoutElement = columns.AddComponent<LayoutElement>();
        columnsLayoutElement.preferredHeight = 500f;
        columnsLayoutElement.flexibleHeight = 1f;

        buyContent = CreateShopColumn(columns.transform, "Comprar");
        sellContent = CreateShopColumn(columns.transform, "Vender");

        // =========================================================
        // 🔥 BOTÃO FIXO NO CANTO (IGNORA LAYOUT)
        // =========================================================

        GameObject closeContainer = new GameObject("CloseButton", typeof(RectTransform));
        closeContainer.transform.SetParent(panelObject.transform, false);
        closeContainer.transform.SetAsLastSibling(); // garante que fica na frente

        RectTransform rect = closeContainer.GetComponent<RectTransform>();

        // 🔥 ignora layout
        LayoutElement ignoreLayout = closeContainer.AddComponent<LayoutElement>();
        ignoreLayout.ignoreLayout = true;

        // 🔥 posição no canto superior direito
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-30f, -40f);
        rect.sizeDelta = new Vector2(60f, 40f);
        

        // 🔥 botão
        Button closeButton = CreateButton(closeContainer.transform, "FECHAR", Close);

        // opcional: cor mais bonita
        Image img = closeButton.GetComponent<Image>();
        if (img != null)
            img.color = new Color(0.45f, 0.26f, 0.12f, 1f);
    }

    Transform CreateShopColumn(Transform parent, string title)
    {
        GameObject column = CreatePanel($"{title}Column", parent, new Color(0.2f, 0.16f, 0.09f, 0.95f));
        LayoutElement layoutElement = column.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
        layoutElement.preferredWidth = 520f;

        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText($"{title}Title", column.transform, 28, FontStyles.Bold, TextAlignmentOptions.Center, title);

        GameObject scrollObject = new GameObject($"{title}Scroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scrollObject.transform.SetParent(column.transform, false);
        scrollObject.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.04f, 0.55f);
        scrollObject.GetComponent<Mask>().showMaskGraphic = false;

        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.preferredHeight = 420f;

        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(0f, 420f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 22f;

        return contentObject.transform;
    }

    void CreateTradeRow(Transform parent, Sprite iconSprite, string itemName, string detailText, string buttonLabel, bool buttonInteractable, UnityEngine.Events.UnityAction action)
    {
        GameObject row = CreatePanel($"{itemName}Row", parent, new Color(0.29f, 0.23f, 0.13f, 0.95f));
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 88f;

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(14, 14, 12, 12);
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(row.transform, false);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.preserveAspect = true;
        iconImage.enabled = iconSprite != null;
        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 56f;
        iconLayout.preferredHeight = 56f;

        GameObject textColumn = new GameObject("TextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        textColumn.transform.SetParent(row.transform, false);
        VerticalLayoutGroup textLayout = textColumn.GetComponent<VerticalLayoutGroup>();
        textLayout.spacing = 4f;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = false;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;
        LayoutElement textColumnLayout = textColumn.AddComponent<LayoutElement>();
        textColumnLayout.flexibleWidth = 1f;

        CreateText("ItemName", textColumn.transform, 22, FontStyles.Bold, TextAlignmentOptions.Left, itemName);
        CreateText("Details", textColumn.transform, 17, FontStyles.Normal, TextAlignmentOptions.Left, detailText);

        Button actionButton = CreateButton(row.transform, buttonLabel, action);
        actionButton.interactable = buttonInteractable;
        LayoutElement buttonLayout = actionButton.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 136f;
        buttonLayout.preferredHeight = 50f;
    }

    void CreateEmptyState(Transform parent, string message)
    {
        GameObject row = CreatePanel("EmptyState", parent, new Color(0.22f, 0.18f, 0.1f, 0.9f));
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 72f;

        TextMeshProUGUI text = CreateText("Message", row.transform, 20, FontStyles.Italic, TextAlignmentOptions.Center, message);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.77f, 0.57f, 0.18f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.77f, 0.57f, 0.18f, 1f);
        colors.highlightedColor = new Color(0.9f, 0.69f, 0.24f, 1f);
        colors.pressedColor = new Color(0.58f, 0.41f, 0.12f, 1f);
        colors.disabledColor = new Color(0.35f, 0.31f, 0.22f, 0.85f);
        button.colors = colors;

        TextMeshProUGUI labelText = CreateText("Label", buttonObject.transform, 22, FontStyles.Bold, TextAlignmentOptions.Center, label);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, string text = "")
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = new Color(1f, 0.96f, 0.87f, 1f);
        textComponent.text = text;
        return textComponent;
    }

    GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    void SetVisible(bool visible)
    {
        if (overlayObject != null)
            overlayObject.SetActive(visible);
    }

    void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }
}
