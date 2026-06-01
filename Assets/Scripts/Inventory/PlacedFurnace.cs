using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class PlacedFurnace : MonoBehaviour, IPlayerInteractable
{
    public float smeltDuration = 8f;

    [SerializeField] int rawIronCount;
    [SerializeField] int fuelCount;
    [SerializeField] int refinedIronCount;
    [SerializeField] float smeltTimer;

    public int RawIronCount => rawIronCount;
    public int FuelCount => fuelCount;
    public int RefinedIronCount => refinedIronCount;
    public float SmeltProgress => smeltDuration > 0f && IsSmelting ? Mathf.Clamp01(smeltTimer / smeltDuration) : 0f;
    public bool IsSmelting => rawIronCount > 0 && fuelCount > 0;

    void Update()
    {
        if (!IsSmelting)
        {
            smeltTimer = 0f;
            return;
        }

        smeltTimer += Time.deltaTime;
        if (smeltTimer < Mathf.Max(0.1f, smeltDuration))
            return;

        smeltTimer = 0f;
        rawIronCount--;
        fuelCount--;
        refinedIronCount++;
    }

    public bool Interact(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null || playerInteraction.inventory == null)
            return false;

        PlacedFurnaceUI ui = PlacedFurnaceUI.Instance ?? FindFirstObjectByType<PlacedFurnaceUI>();
        if (ui == null)
        {
            GameObject uiObject = new GameObject("FurnaceUI");
            ui = uiObject.AddComponent<PlacedFurnaceUI>();
        }

        ui.Open(this, playerInteraction.inventory, playerInteraction.hotbar, playerInteraction);
        return true;
    }

    public bool TryAddRawIron(Inventory inventory, Hotbar hotbar)
    {
        return TryMoveFromInventory(inventory, hotbar, IronItemRegistry.ItemName, ref rawIronCount);
    }

    public bool TryAddFuel(Inventory inventory, Hotbar hotbar, out string fuelName)
    {
        fuelName = "";

        if (inventory == null || inventory.items == null)
            return false;

        for (int i = 0; i < inventory.items.Count; i++)
        {
            InventoryItem item = inventory.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemName) || item.quantity <= 0)
                continue;

            if (!IsFuelItem(item.itemName))
                continue;

            fuelName = item.itemName;
            return TryMoveFromInventory(inventory, hotbar, item.itemName, ref fuelCount);
        }

        return false;
    }

    public bool TryCollectOutput(Inventory inventory)
    {
        if (inventory == null || refinedIronCount <= 0)
            return false;

        Item refinedIron = RefinedIronItemRegistry.GetOrCreate();
        inventory.AddItem(refinedIron.itemName, refinedIronCount, refinedIron);
        refinedIronCount = 0;
        return true;
    }

    public static bool IsFuelItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return false;

        string trimmedName = itemName.Trim();
        return string.Equals(trimmedName, "Graveto", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmedName, "Carvao", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmedName, "Carvão", System.StringComparison.OrdinalIgnoreCase) ||
               trimmedName.StartsWith("Madeira", System.StringComparison.OrdinalIgnoreCase);
    }

    bool TryMoveFromInventory(Inventory inventory, Hotbar hotbar, string itemName, ref int targetCount)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(itemName))
            return false;

        InventoryItem item = inventory.GetItem(itemName);
        if (item == null || item.quantity <= 0)
            return false;

        hotbar?.RemoveInventoryItem(item, 1);
        if (!inventory.RemoveItem(item.itemName, 1))
            return false;

        targetCount++;
        return true;
    }
}

public class PlacedFurnaceUI : MonoBehaviour
{
    public static PlacedFurnaceUI Instance { get; private set; }

    GameObject overlayObject;
    Image progressFill;
    TextMeshProUGUI rawCountText;
    TextMeshProUGUI fuelCountText;
    TextMeshProUGUI outputCountText;
    TextMeshProUGUI statusText;
    Button addOreButton;
    Button addFuelButton;
    Button collectButton;
    InputAction closeAction;

    PlacedFurnace currentFurnace;
    Inventory currentInventory;
    Hotbar currentHotbar;
    PlayerInteraction currentPlayerInteraction;
    PlayerMovement currentPlayerMovement;

    readonly Color overlayColor = new Color(0f, 0f, 0f, 0.55f);
    readonly Color panelColor = new Color(0.16f, 0.15f, 0.14f, 0.98f);
    readonly Color sectionColor = new Color(0.24f, 0.22f, 0.2f, 0.98f);
    readonly Color slotColor = new Color(0.08f, 0.075f, 0.07f, 0.95f);
    readonly Color buttonColor = new Color(0.78f, 0.42f, 0.12f, 1f);
    readonly Color disabledColor = new Color(0.43f, 0.4f, 0.38f, 0.9f);
    readonly Color textColor = new Color(1f, 0.94f, 0.84f, 1f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        closeAction = new InputAction("CloseFurnace", binding: "<Keyboard>/escape");
    }

    void OnEnable()
    {
        closeAction?.Enable();
    }

    void OnDisable()
    {
        closeAction?.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (overlayObject == null || !overlayObject.activeSelf)
            return;

        if (closeAction.WasPressedThisFrame())
            Close();

        Refresh();
    }

    public void Open(PlacedFurnace furnace, Inventory inventory, Hotbar hotbar, PlayerInteraction playerInteraction)
    {
        if (furnace == null || inventory == null)
            return;

        EnsureEventSystem();
        BuildUi();

        currentFurnace = furnace;
        currentInventory = inventory;
        currentHotbar = hotbar;
        currentPlayerInteraction = playerInteraction;
        currentPlayerMovement = playerInteraction != null ? playerInteraction.GetComponent<PlayerMovement>() : null;

        GameState.IsInventoryOpen = false;
        GameState.IsCraftingOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (currentPlayerMovement != null)
            currentPlayerMovement.enabled = false;

        SetVisible(true);
        Refresh();
    }

    public void Close()
    {
        GameState.IsCraftingOpen = false;
        GameState.LastUiCloseFrame = Time.frameCount;
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

    void AddOre()
    {
        if (currentFurnace == null || currentInventory == null)
            return;

        if (!currentFurnace.TryAddRawIron(currentInventory, currentHotbar))
            MessageSystem.Instance?.ShowMessage("Voce precisa de Ferro Bruto.");

        RefreshInventoryUi();
        Refresh();
    }

    void AddFuel()
    {
        if (currentFurnace == null || currentInventory == null)
            return;

        if (!currentFurnace.TryAddFuel(currentInventory, currentHotbar, out string fuelName))
            MessageSystem.Instance?.ShowMessage("Use graveto ou madeira como combustivel.");
        else
            MessageSystem.Instance?.ShowMessage($"+1 combustivel ({fuelName})");

        RefreshInventoryUi();
        Refresh();
    }

    void CollectOutput()
    {
        if (currentFurnace == null || currentInventory == null)
            return;

        if (!currentFurnace.TryCollectOutput(currentInventory))
            MessageSystem.Instance?.ShowMessage("Nada refinado ainda.");

        RefreshInventoryUi();
        Refresh();
    }

    void Refresh()
    {
        if (currentFurnace == null || rawCountText == null)
            return;

        rawCountText.text = currentFurnace.RawIronCount.ToString();
        fuelCountText.text = currentFurnace.FuelCount.ToString();
        outputCountText.text = currentFurnace.RefinedIronCount.ToString();
        progressFill.fillAmount = currentFurnace.SmeltProgress;

        addOreButton.interactable = currentInventory != null && currentInventory.GetItem(IronItemRegistry.ItemName) != null;
        addFuelButton.interactable = HasFuelInInventory();
        collectButton.interactable = currentFurnace.RefinedIronCount > 0;

        if (currentFurnace.IsSmelting)
            statusText.text = "Refinando Ferro Bruto...";
        else if (currentFurnace.RawIronCount <= 0)
            statusText.text = "Adicione Ferro Bruto.";
        else if (currentFurnace.FuelCount <= 0)
            statusText.text = "Adicione combustivel.";
        else
            statusText.text = "Fornalha pronta.";
    }

    bool HasFuelInInventory()
    {
        if (currentInventory == null || currentInventory.items == null)
            return false;

        for (int i = 0; i < currentInventory.items.Count; i++)
        {
            InventoryItem item = currentInventory.items[i];
            if (item != null && item.quantity > 0 && PlacedFurnace.IsFuelItem(item.itemName))
                return true;
        }

        return false;
    }

    void RefreshInventoryUi()
    {
        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();
    }

    void BuildUi()
    {
        if (overlayObject != null)
            return;

        GameObject canvasObject = new GameObject("FurnaceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        overlayObject = CreatePanel("FurnaceOverlay", canvasObject.transform, new Color(0f, 0f, 0f, 0.55f));
        Stretch(overlayObject, 0f);

        GameObject panel = CreatePanel("FurnacePanel", overlayObject.transform, panelColor);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 360f);
        panelRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(18, 18, 14, 18);
        panelLayout.spacing = 12f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText("Title", panel.transform, 28, FontStyles.Bold, TextAlignmentOptions.Center, "FORNALHA");
        SetLayoutHeight(title.gameObject, 42f);

        GameObject workRow = new GameObject("WorkRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        workRow.transform.SetParent(panel.transform, false);
        SetLayoutHeight(workRow, 205f);
        HorizontalLayoutGroup workLayout = workRow.GetComponent<HorizontalLayoutGroup>();
        workLayout.spacing = 12f;
        workLayout.childControlWidth = true;
        workLayout.childControlHeight = true;
        workLayout.childForceExpandWidth = true;

        CreateStationSection(workRow.transform, "Minerio", IronItemRegistry.GetSprite(), "Ferro Bruto", out rawCountText, out addOreButton, AddOre);
        CreateStationSection(workRow.transform, "Calor", null, "Combustivel", out fuelCountText, out addFuelButton, AddFuel);
        CreateStationSection(workRow.transform, "Refinado", RefinedIronItemRegistry.GetSprite(), RefinedIronItemRegistry.ItemName, out outputCountText, out collectButton, CollectOutput);
        collectButton.GetComponentInChildren<TextMeshProUGUI>().text = "Coletar";

        GameObject progressPanel = CreatePanel("ProgressPanel", panel.transform, new Color(0f, 0f, 0f, 0.24f));
        SetLayoutHeight(progressPanel, 48f);

        Image progressBack = CreateImage("ProgressBack", progressPanel.transform, new Color(0.08f, 0.06f, 0.04f, 1f));
        RectTransform backRect = progressBack.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.03f, 0.28f);
        backRect.anchorMax = new Vector2(0.97f, 0.72f);
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;

        progressFill = CreateImage("ProgressFill", progressBack.transform, new Color(1f, 0.42f, 0.08f, 1f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;
        Stretch(progressFill.gameObject, 0f);

        statusText = CreateText("Status", panel.transform, 17, FontStyles.Bold, TextAlignmentOptions.Center, "");
        SetLayoutHeight(statusText.gameObject, 32f);

        Button closeButton = CreateButton(panel.transform, "Fechar", Close, disabledColor);
        SetLayoutHeight(closeButton.gameObject, 34f);
        SetVisible(false);
    }

    void CreateStationSection(Transform parent, string title, Sprite icon, string itemName, out TextMeshProUGUI countText, out Button button, UnityEngine.Events.UnityAction onClick)
    {
        GameObject section = CreatePanel(title + "Section", parent, sectionColor);
        VerticalLayoutGroup layout = section.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 9, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        TextMeshProUGUI titleText = CreateText("Header", section.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center, title);
        SetLayoutHeight(titleText.gameObject, 28f);

        GameObject slot = CreatePanel("Slot", section.transform, slotColor);
        SetLayoutHeight(slot, 86f);

        if (icon != null)
        {
            Image iconImage = CreateImage("Icon", slot.transform, Color.white);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            RectTransform iconRect = iconImage.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.22f, 0.16f);
            iconRect.anchorMax = new Vector2(0.78f, 0.8f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
        }
        else
        {
            CreateFuelGlyph(slot.transform);
        }

        countText = CreateText("Count", slot.transform, 18, FontStyles.Bold, TextAlignmentOptions.BottomRight, "0");
        Stretch(countText.gameObject, 6f);

        TextMeshProUGUI label = CreateText("ItemName", section.transform, 14, FontStyles.Normal, TextAlignmentOptions.Center, itemName);
        SetLayoutHeight(label.gameObject, 24f);

        button = CreateButton(section.transform, "Adicionar", onClick, buttonColor);
        SetLayoutHeight(button.gameObject, 34f);
    }

    void CreateFuelGlyph(Transform parent)
    {
        Image ember = CreateImage("FuelEmber", parent, new Color(1f, 0.45f, 0.08f, 1f));
        RectTransform emberRect = ember.GetComponent<RectTransform>();
        emberRect.anchorMin = new Vector2(0.36f, 0.2f);
        emberRect.anchorMax = new Vector2(0.64f, 0.5f);
        emberRect.offsetMin = Vector2.zero;
        emberRect.offsetMax = Vector2.zero;

        Image flame = CreateImage("FuelFlame", parent, new Color(1f, 0.84f, 0.16f, 1f));
        RectTransform flameRect = flame.GetComponent<RectTransform>();
        flameRect.anchorMin = new Vector2(0.42f, 0.42f);
        flameRect.anchorMax = new Vector2(0.58f, 0.76f);
        flameRect.offsetMin = Vector2.zero;
        flameRect.offsetMax = Vector2.zero;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, Color color)
    {
        GameObject obj = CreatePanel(label + "Button", parent, color);
        Button button = obj.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.disabledColor = disabledColor;
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Label", obj.transform, 15, FontStyles.Bold, TextAlignmentOptions.Center, label);
        Stretch(text.gameObject, 3f);
        return button;
    }

    GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return obj;
    }

    Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, int size, FontStyles style, TextAlignmentOptions alignment, string text)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = textColor;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    void SetLayoutHeight(GameObject obj, float height)
    {
        LayoutElement layout = obj.GetComponent<LayoutElement>();
        if (layout == null)
            layout = obj.AddComponent<LayoutElement>();

        layout.preferredHeight = height;
        layout.minHeight = height;
    }

    void Stretch(GameObject obj, float padding)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    void SetVisible(bool visible)
    {
        if (overlayObject != null)
            overlayObject.SetActive(visible);
    }

    void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}

public static class RefinedIronItemRegistry
{
    public const string ItemName = "Ferro Refinado";
    const string IconPath = "Icons/FerroRefinado";

    static Item refinedIronItem;
    static Sprite refinedIronSprite;

    public static Item GetOrCreate()
    {
        if (refinedIronItem != null)
            return refinedIronItem;

        GameObject itemObject = new GameObject("FerroRefinadoItemData");
        Object.DontDestroyOnLoad(itemObject);

        refinedIronItem = itemObject.AddComponent<Item>();
        refinedIronItem.itemName = ItemName;
        refinedIronItem.itemType = ItemType.Resource;
        refinedIronItem.toolType = ToolType.None;
        refinedIronItem.toolDamage = 0;
        refinedIronItem.buyPrice = 0;
        refinedIronItem.sellPrice = 4;
        refinedIronItem.icon = GetSprite();

        return refinedIronItem;
    }

    public static Sprite GetSprite()
    {
        if (refinedIronSprite != null)
            return refinedIronSprite;

        refinedIronSprite = Resources.Load<Sprite>(IconPath);
        if (refinedIronSprite != null)
            return refinedIronSprite;

        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color dark = new Color(0.28f, 0.29f, 0.3f, 1f);
        Color mid = new Color(0.62f, 0.64f, 0.66f, 1f);
        Color light = new Color(0.88f, 0.9f, 0.92f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 7; y <= 16; y++)
        {
            for (int x = 4; x <= 19; x++)
            {
                if (x < 6 && y < 9)
                    continue;

                Color color = y >= 14 || x <= 5 || x >= 18 ? dark : mid;
                if (y <= 9 && x >= 8 && x <= 16)
                    color = light;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        refinedIronSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        refinedIronSprite.name = "FerroRefinadoRuntimeSprite";
        return refinedIronSprite;
    }
}
