using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CraftingBenchUI : MonoBehaviour
{
    const int LayoutVersion = 26;

    public static CraftingBenchUI Instance { get; private set; }

    GameObject overlayObject;
    GameObject panelObject;
    Transform recipeListRoot;
    Transform artisanSlotRoot;
    Transform inventoryGridRoot;
    Transform selectedOutputRoot;
    TextMeshProUGUI selectedNameText;
    TextMeshProUGUI selectedRequirementText;
    TextMeshProUGUI quantityText;
    TextMeshProUGUI statusText;
    TextMeshProUGUI inventoryGoldText;
    Button prepareButton;
    Button minusButton;
    Button plusButton;
    Button maxButton;
    InputAction closeAction;

    CraftingBench currentBench;
    Inventory currentInventory;
    Hotbar currentHotbar;
    PlayerInteraction currentPlayerInteraction;
    PlayerMovement currentPlayerMovement;

    int craftAmount = 1;
    int builtLayoutVersion;

    readonly Color overlayColor = new Color(0f, 0f, 0f, 0.56f);
    readonly Color sectionColor = new Color(0.2f, 0.21f, 0.22f, 0.99f);
    readonly Color headerColor = new Color(0.3f, 0.4f, 0.49f, 1f);
    readonly Color rowColor = new Color(0.16f, 0.17f, 0.18f, 0.9f);
    readonly Color selectedRowColor = new Color(0.24f, 0.25f, 0.26f, 0.96f);
    readonly Color slotColor = new Color(0.33f, 0.33f, 0.33f, 0.95f);
    readonly Color slotBorderColor = new Color(1f, 0.82f, 0.12f, 1f);
    readonly Color missingColor = new Color(0.86f, 0.05f, 0.05f, 0.92f);
    readonly Color prepareColor = new Color(0.12f, 0.78f, 0.32f, 1f);
    readonly Color disabledColor = new Color(0.48f, 0.5f, 0.52f, 0.85f);
    readonly Color textColor = new Color(1f, 0.96f, 0.88f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<CraftingBenchUI>() != null)
            return;

        GameObject uiObject = new GameObject("CraftingBenchUI");
        uiObject.AddComponent<CraftingBenchUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        closeAction = new InputAction("CloseCraftingBench", binding: "<Keyboard>/escape");
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
        BuildUi(false);
        SetVisible(GameState.IsCraftingOpen);
    }

    void Update()
    {
        if (!GameState.IsCraftingOpen)
            return;

        if (closeAction.WasPressedThisFrame())
            Close();
    }

    public void Open(CraftingBench bench, Inventory inventory, Hotbar hotbar, PlayerInteraction playerInteraction)
    {
        if (bench == null || inventory == null)
            return;

        EnsureEventSystem();

        BuildUi(true);

        currentBench = bench;
        currentInventory = inventory;
        currentHotbar = hotbar;
        currentPlayerInteraction = playerInteraction;
        currentPlayerMovement = playerInteraction != null ? playerInteraction.GetComponent<PlayerMovement>() : null;
        craftAmount = 1;

        GameState.IsInventoryOpen = false;
        GameState.IsCraftingOpen = true;
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

    void Refresh()
    {
        if (currentBench == null || currentInventory == null)
            return;

        int availableCrafts = currentBench.GetAvailableCraftCount(currentInventory);
        craftAmount = Mathf.Clamp(craftAmount, 1, Mathf.Max(1, availableCrafts));

        RefreshRecipes(availableCrafts);
        RefreshArtisan(availableCrafts);
        RefreshInventory();
    }

    void RefreshRecipes(int availableCrafts)
    {
        ClearChildren(recipeListRoot);

        CreateRecipeRow(availableCrafts);

        for (int i = 0; i < 3; i++)
            CreateRecipePlaceholder(recipeListRoot);
    }

    void RefreshArtisan(int availableCrafts)
    {
        ClearChildren(artisanSlotRoot);
        ClearChildren(selectedOutputRoot);

        CreateCraftSlot(artisanSlotRoot, currentBench.GetInputIcon(currentInventory), currentBench.GetInputAmount().ToString(), availableCrafts <= 0);
        int emptySlots = 9;
        for (int i = 0; i < emptySlots; i++)
            CreateCraftSlot(artisanSlotRoot, null, string.Empty, false);

        CreateSlotContent(selectedOutputRoot, currentBench.GetOutputIcon(), string.Empty, false);

        string outputName = currentBench.GetOutputName();
        selectedNameText.text = outputName;
        selectedRequirementText.text = string.Empty;
        quantityText.text = craftAmount.ToString();

        bool canCraft = availableCrafts > 0;
        prepareButton.interactable = canCraft;
        minusButton.interactable = canCraft && craftAmount > 1;
        plusButton.interactable = canCraft && craftAmount < availableCrafts;
        maxButton.interactable = canCraft && craftAmount < availableCrafts;

        statusText.text = canCraft
            ? $"Voce pode preparar ate {availableCrafts} vez(es)."
            : "Receita indisponivel: falta madeira no inventario.";
        statusText.color = canCraft ? textColor : new Color(1f, 0.62f, 0.62f, 1f);
    }

    void RefreshInventory()
    {
        ClearChildren(inventoryGridRoot);

        List<InventoryItem> visibleItems = new List<InventoryItem>();
        if (currentInventory != null && currentInventory.items != null)
        {
            for (int i = 0; i < currentInventory.items.Count; i++)
            {
                InventoryItem item = currentInventory.items[i];
                if (item == null || item.quantity <= 0 || string.Equals(item.itemName, "Gold", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                visibleItems.Add(item);
            }
        }

        int totalSlots = 12;
        for (int i = 0; i < totalSlots; i++)
        {
            InventoryItem item = i < visibleItems.Count ? visibleItems[i] : null;
            CreateInventorySlot(inventoryGridRoot, item);
        }

        inventoryGoldText.text = $"{(currentInventory != null ? currentInventory.GetGoldAmount() : 0)} K";
    }

    void CraftSelectedRecipe()
    {
        if (currentBench == null)
            return;

        if (currentBench.TryCraftSticks(currentInventory, currentHotbar, craftAmount, out string message))
            MessageSystem.Instance?.ShowMessage(message);
        else
            MessageSystem.Instance?.ShowMessage(message);

        Refresh();
    }

    void ChangeCraftAmount(int delta)
    {
        int availableCrafts = currentBench != null ? currentBench.GetAvailableCraftCount(currentInventory) : 0;
        craftAmount = Mathf.Clamp(craftAmount + delta, 1, Mathf.Max(1, availableCrafts));
        RefreshArtisan(availableCrafts);
    }

    void SetMaxCraftAmount()
    {
        int availableCrafts = currentBench != null ? currentBench.GetAvailableCraftCount(currentInventory) : 0;
        craftAmount = Mathf.Max(1, availableCrafts);
        RefreshArtisan(availableCrafts);
    }

    int CountItemsStartingWith(string prefix)
    {
        int total = 0;

        if (currentInventory == null || currentInventory.items == null)
            return total;

        for (int i = 0; i < currentInventory.items.Count; i++)
        {
            InventoryItem item = currentInventory.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                continue;

            if (item.itemName.Trim().StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                total += item.quantity;
        }

        return total;
    }

    void BuildUi(bool forceRebuild)
    {
        if (!forceRebuild && panelObject != null && builtLayoutVersion == LayoutVersion)
            return;

        RebuildUiFromScratch();

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        ConfigureCraftCanvasScaler(scaler);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        overlayObject = CreatePanel("CraftingOverlay", transform, overlayColor);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        panelObject = new GameObject("CraftingPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panelObject.transform.SetParent(overlayObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1240f, 560f);
        panelRect.anchoredPosition = new Vector2(0f, 8f);

        HorizontalLayoutGroup panelLayout = panelObject.GetComponent<HorizontalLayoutGroup>();
        panelLayout.spacing = 8f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = false;
        panelLayout.childForceExpandHeight = true;

        BuildRecipesSection(panelObject.transform);
        BuildArtisanSection(panelObject.transform);
        BuildInventorySection(panelObject.transform);
        builtLayoutVersion = LayoutVersion;
    }

    void RebuildUiFromScratch()
    {
        if (overlayObject != null)
            Destroy(overlayObject);

        overlayObject = null;
        panelObject = null;
        recipeListRoot = null;
        artisanSlotRoot = null;
        inventoryGridRoot = null;
        selectedOutputRoot = null;
        selectedNameText = null;
        selectedRequirementText = null;
        quantityText = null;
        statusText = null;
        inventoryGoldText = null;
        prepareButton = null;
        minusButton = null;
        plusButton = null;
        maxButton = null;
    }

    void BuildRecipesSection(Transform parent)
    {
        GameObject section = CreateSection("Receitas", parent, 380f);
        CreateSearchBar(section.transform, "Buscar...", "Nivel  1  a  1");
        CreateSmallFilter(section.transform, "Todas as categorias", "Nivel mais elevado");
        CreateSmallFilter(section.transform, "Faltam todos os ingredientes", string.Empty);

        GameObject list = new GameObject("RecipeList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(section.transform, false);
        list.AddComponent<LayoutElement>().flexibleHeight = 1f;
        VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 8);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        recipeListRoot = list.transform;
    }

    void BuildArtisanSection(Transform parent)
    {
        GameObject section = CreateSection("Artesao", parent, 480f);

        GameObject slotGrid = new GameObject("ArtisanSlots", typeof(RectTransform), typeof(GridLayoutGroup));
        slotGrid.transform.SetParent(section.transform, false);
        slotGrid.AddComponent<LayoutElement>().preferredHeight = 112f;
        GridLayoutGroup grid = slotGrid.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 9, 7);
        grid.cellSize = new Vector2(52f, 34f);
        grid.spacing = new Vector2(8f, 7f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        artisanSlotRoot = slotGrid.transform;

        GameObject details = CreatePanel("ArtisanDetails", section.transform, new Color(0.34f, 0.34f, 0.34f, 0.98f));
        details.AddComponent<LayoutElement>().flexibleHeight = 1f;
        VerticalLayoutGroup detailsLayout = details.AddComponent<VerticalLayoutGroup>();
        detailsLayout.padding = new RectOffset(12, 12, 10, 10);
        detailsLayout.spacing = 7f;
        detailsLayout.childControlWidth = true;
        detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;

        GameObject selectedLine = new GameObject("SelectedLine", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        selectedLine.transform.SetParent(details.transform, false);
        selectedLine.AddComponent<LayoutElement>().preferredHeight = 48f;
        HorizontalLayoutGroup selectedLayout = selectedLine.GetComponent<HorizontalLayoutGroup>();
        selectedLayout.spacing = 9f;
        selectedLayout.childControlWidth = true;
        selectedLayout.childControlHeight = true;
        selectedLayout.childAlignment = TextAnchor.MiddleCenter;

        selectedOutputRoot = CreatePlainSlot(selectedLine.transform, 48f, 40f, false).transform;

        GameObject selectedTextBox = new GameObject("SelectedText", typeof(RectTransform), typeof(VerticalLayoutGroup));
        selectedTextBox.transform.SetParent(selectedLine.transform, false);
        selectedTextBox.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup textLayout = selectedTextBox.GetComponent<VerticalLayoutGroup>();
        textLayout.spacing = 2f;
        selectedNameText = CreateText("SelectedName", selectedTextBox.transform, 16, FontStyles.Bold, TextAlignmentOptions.Left);
        selectedRequirementText = CreateText("SelectedRequirement", selectedTextBox.transform, 11, FontStyles.Normal, TextAlignmentOptions.Left);

        GameObject quantityLine = new GameObject("QuantityLine", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        quantityLine.transform.SetParent(details.transform, false);
        quantityLine.AddComponent<LayoutElement>().preferredHeight = 28f;
        HorizontalLayoutGroup quantityLayout = quantityLine.GetComponent<HorizontalLayoutGroup>();
        quantityLayout.spacing = 6f;
        quantityLayout.childControlWidth = true;
        quantityLayout.childControlHeight = true;
        quantityLayout.childAlignment = TextAnchor.MiddleCenter;

        Button minButton = CreateButton(quantityLine.transform, "MIN", () => { craftAmount = 1; RefreshArtisan(currentBench.GetAvailableCraftCount(currentInventory)); }, disabledColor, 46f, 26f, 11f);
        minusButton = CreateButton(quantityLine.transform, "-", () => ChangeCraftAmount(-1), disabledColor, 34f, 26f, 15f);
        quantityText = CreateBoxText(quantityLine.transform, "1", 70f, 26f, 14f);
        plusButton = CreateButton(quantityLine.transform, "+", () => ChangeCraftAmount(1), disabledColor, 34f, 26f, 15f);
        maxButton = CreateButton(quantityLine.transform, "MAX", SetMaxCraftAmount, disabledColor, 46f, 26f, 11f);
        _ = minButton;

        prepareButton = CreateButton(details.transform, "PREPARAR", CraftSelectedRecipe, prepareColor, 160f, 30f, 14f);
        RectTransform prepareRect = prepareButton.GetComponent<RectTransform>();
        prepareRect.anchorMin = new Vector2(0.5f, 0f);
        prepareRect.anchorMax = new Vector2(0.5f, 0f);

        statusText = CreateText("Status", details.transform, 12, FontStyles.Bold, TextAlignmentOptions.Center);
        statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
    }

    void BuildInventorySection(Transform parent)
    {
        GameObject section = CreateSection("Inventario", parent, 360f);
        CreateSearchBar(section.transform, "Buscar...", string.Empty);

        GameObject toggleLine = new GameObject("UsefulFilter", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        toggleLine.transform.SetParent(section.transform, false);
        toggleLine.AddComponent<LayoutElement>().preferredHeight = 18f;
        HorizontalLayoutGroup toggleLayout = toggleLine.GetComponent<HorizontalLayoutGroup>();
        toggleLayout.padding = new RectOffset(16, 8, 0, 0);
        toggleLayout.spacing = 6f;
        toggleLayout.childControlWidth = true;
        toggleLayout.childControlHeight = true;
        CreateCheckBox(toggleLine.transform);
        CreateText("UsefulLabel", toggleLine.transform, 9, FontStyles.Normal, TextAlignmentOptions.Left, "Ingredientes uteis")
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;

        GameObject grid = new GameObject("InventoryGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(section.transform, false);
        grid.AddComponent<LayoutElement>().flexibleHeight = 1f;
        GridLayoutGroup gridLayout = grid.GetComponent<GridLayoutGroup>();
        gridLayout.padding = new RectOffset(20, 16, 7, 7);
        gridLayout.cellSize = new Vector2(44f, 38f);
        gridLayout.spacing = new Vector2(42f, 7f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 3;
        inventoryGridRoot = grid.transform;

        inventoryGoldText = CreateText("GoldAmount", section.transform, 15, FontStyles.Bold, TextAlignmentOptions.Right);
        inventoryGoldText.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
    }

    GameObject CreateSection(string title, Transform parent, float width)
    {
        GameObject section = CreatePanel($"{title}Section", parent, sectionColor);
        LayoutElement layout = section.AddComponent<LayoutElement>();
        layout.preferredWidth = width;

        VerticalLayoutGroup vertical = section.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 0f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        GameObject header = CreatePanel($"{title}Header", section.transform, headerColor);
        SetLayoutHeight(header, 40f);
        TextMeshProUGUI label = CreateText("HeaderLabel", header.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center, title);
        StretchText(label, 4f);
        return section;
    }

    void CreateSearchBar(Transform parent, string leftText, string rightText)
    {
        GameObject row = new GameObject("SearchRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        SetLayoutHeight(row, 24f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 0, 0);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        CreateBoxText(row.transform, leftText, 136f, 20f, 12f, TextAlignmentOptions.Left);
        if (!string.IsNullOrWhiteSpace(rightText))
            CreateBoxText(row.transform, rightText, 224f, 20f, 12f, TextAlignmentOptions.Center);
    }

    void CreateSmallFilter(Transform parent, string leftText, string rightText)
    {
        GameObject row = new GameObject("FilterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        SetLayoutHeight(row, 24f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 0, 0);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        CreateBoxText(row.transform, leftText, string.IsNullOrWhiteSpace(rightText) ? 354f : 170f, 20f, 12f, TextAlignmentOptions.Left);
        if (!string.IsNullOrWhiteSpace(rightText))
            CreateBoxText(row.transform, rightText, 170f, 20f, 12f, TextAlignmentOptions.Left);
    }

    void CreateRecipeRow(int availableCrafts)
    {
        bool missingIngredients = availableCrafts <= 0;
        Button rowButton = CreateRecipeButton(recipeListRoot, missingIngredients);
        rowButton.onClick.AddListener(() =>
        {
            craftAmount = 1;
            RefreshArtisan(currentBench.GetAvailableCraftCount(currentInventory));
        });
    }

    Button CreateRecipeButton(Transform parent, bool missingIngredients)
    {
        GameObject row = CreatePanel("Recipe_Gravetos", parent, selectedRowColor);
        SetLayoutHeight(row, 25f);
        Button button = row.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = selectedRowColor;
        colors.highlightedColor = Color.Lerp(selectedRowColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(selectedRowColor, Color.black, 0.22f);
        button.colors = colors;

        GameObject outputSlot = CreatePlainSlot(row.transform, 66f, 72f, false);
        SetTopLeftRect(outputSlot, 8f, 8f, 66f, 72f);
        CreateSlotContent(outputSlot.transform, currentBench.GetOutputIcon(), string.Empty, false);
        CreateCenteredSlotLabel(outputSlot.transform, $"x{currentBench.GetOutputAmount()}", 11f);

        TextMeshProUGUI nameText = CreateText("Name", row.transform, 15, FontStyles.Bold, TextAlignmentOptions.Left, "Gravetos");
        SetTopLeftRect(nameText.gameObject, 84f, 10f, 166f, 19f);

        TextMeshProUGUI levelText = CreateText("Level", row.transform, 10, FontStyles.Normal, TextAlignmentOptions.Left, "Niv. 1");
        SetTopLeftRect(levelText.gameObject, 84f, 30f, 110f, 15f);

        TextMeshProUGUI xp = CreateText("XP", row.transform, 10, FontStyles.Bold, TextAlignmentOptions.Right, "20 XP");
        SetTopLeftRect(xp.gameObject, 302f, 10f, 54f, 16f);

        GameObject ingredientsPanel = CreatePanel("RecipeIngredients", row.transform, new Color(0f, 0f, 0f, 0.14f));
        SetTopLeftRect(ingredientsPanel, 84f, 54f, 50f, 30f);
        HorizontalLayoutGroup ingredientsLayout = ingredientsPanel.AddComponent<HorizontalLayoutGroup>();
        ingredientsLayout.padding = new RectOffset(4, 4, 3, 3);
        ingredientsLayout.spacing = 6f;
        ingredientsLayout.childControlWidth = true;
        ingredientsLayout.childControlHeight = true;
        ingredientsLayout.childForceExpandWidth = false;
        ingredientsLayout.childForceExpandHeight = false;

        GameObject ingredient = CreatePlainSlot(ingredientsPanel.transform, 42f, 24f, missingIngredients);
        CreateSlotContent(ingredient.transform, currentBench.GetInputIcon(currentInventory), currentBench.GetInputAmount().ToString(), missingIngredients);
        return button;
    }

    void CreateRecipePlaceholder(Transform parent)
    {
        GameObject placeholder = CreatePanel("RecipePlaceholder", parent, new Color(1f, 0.84f, 0.22f, 0.96f));
        SetLayoutHeight(placeholder, 30f);
    }

    GameObject CreatePlainSlot(Transform parent, float width, float height, bool missing)
    {
        GameObject slot = CreatePanel("Slot", parent, missing ? missingColor : slotColor);
        LayoutElement layout = slot.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;

        Outline outline = slot.AddComponent<Outline>();
        outline.effectColor = missing ? new Color(1f, 0.2f, 0.2f, 0.95f) : new Color(0.75f, 0.75f, 0.75f, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);
        return slot;
    }

    void SetLayoutHeight(GameObject target, float height)
    {
        if (target == null)
            return;

        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();

        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    void SetTopLeftRect(GameObject target, float x, float y, float width, float height)
    {
        if (target == null)
            return;

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    void CreateCenteredSlotLabel(Transform slot, string label, float fontSize)
    {
        if (slot == null || string.IsNullOrWhiteSpace(label))
            return;

        TextMeshProUGUI labelText = CreateText("CenteredAmount", slot, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, label);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0.28f);
        labelRect.offsetMin = new Vector2(2f, 1f);
        labelRect.offsetMax = new Vector2(-2f, 0f);
    }

    void CreateCraftSlot(Transform parent, Sprite icon, string label, bool missing)
    {
        GameObject slot = CreatePlainSlot(parent, 52f, 34f, missing);
        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = slotBorderColor;
        CreateSlotContent(slot.transform, icon, label, missing);
    }

    void CreateInventorySlot(Transform parent, InventoryItem item)
    {
        GameObject slot = CreatePlainSlot(parent, 44f, 38f, false);
        if (item == null)
            return;

        CreateSlotContent(slot.transform, item.GetDisplayIcon(), item.quantity.ToString(), false);
    }

    void CreateSlotContent(Transform slot, Sprite icon, string label, bool missing)
    {
        if (icon != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(slot, false);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.15f, 0.18f);
            iconRect.anchorMax = new Vector2(0.85f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            TextMeshProUGUI labelText = CreateText("Amount", slot, 8, FontStyles.Bold, TextAlignmentOptions.Right, label);
            labelText.color = missing ? Color.white : textColor;
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.42f);
            labelRect.offsetMin = new Vector2(4f, 1f);
            labelRect.offsetMax = new Vector2(-4f, 0f);
        }
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color color, float width, float height, float fontSize)
    {
        GameObject buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        if (action != null)
            button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.disabledColor = new Color(0.28f, 0.29f, 0.3f, 0.9f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, label);
        StretchText(text, 3f);
        return button;
    }

    TextMeshProUGUI CreateBoxText(Transform parent, string text, float width, float height, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject box = CreatePanel("BoxText", parent, new Color(0.08f, 0.08f, 0.08f, 0.86f));
        LayoutElement layout = box.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
        TextMeshProUGUI label = CreateText("Label", box.transform, fontSize, FontStyles.Normal, alignment, text);
        label.color = new Color(0.86f, 0.86f, 0.86f, 1f);
        StretchText(label, 3f);
        return label;
    }

    void CreateCheckBox(Transform parent)
    {
        GameObject box = CreatePanel("CheckBox", parent, new Color(0.12f, 0.12f, 0.12f, 1f));
        LayoutElement layout = box.AddComponent<LayoutElement>();
        layout.preferredWidth = 10f;
        layout.preferredHeight = 10f;
        Outline outline = box.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);
    }

    TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment, string text = "")
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        textComponent.color = textColor;
        textComponent.enableWordWrapping = true;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
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

    void ConfigureCraftCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    void StretchText(TextMeshProUGUI text, float padding)
    {
        RectTransform rect = text.GetComponent<RectTransform>();
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
