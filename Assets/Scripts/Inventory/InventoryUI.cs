using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    const string OpenInventoryClipPath = "Audio/UI/Abrindo_inventario";
    const string CloseInventoryClipPath = "Audio/UI/Fechando_inventario";
    const float RuntimeInventorySoundVolume = 0.08f;
    const float OpenInventorySoundStartOffset = 0.9f;
    const float CloseInventorySoundStartOffset = 1.2f;

    static readonly InventoryCategory[] CategoryOrder =
    {
        InventoryCategory.All,
        InventoryCategory.Resources,
        InventoryCategory.Food,
        InventoryCategory.Tools,
        InventoryCategory.Weapons,
        InventoryCategory.Armor,
        InventoryCategory.Consumables,
        InventoryCategory.Special,
        InventoryCategory.Other
    };

    static readonly EquipmentSlotType[] EquipmentSlots =
    {
        EquipmentSlotType.Head,
        EquipmentSlotType.Chest,
        EquipmentSlotType.Legs,
        EquipmentSlotType.Boots,
        EquipmentSlotType.MainHand,
        EquipmentSlotType.Shield,
        EquipmentSlotType.Accessory
    };

    public GameObject panel;
    public Transform content;
    public GameObject slotPrefab;
    public AudioClip openInventorySound;
    public AudioClip closeInventorySound;
    [Range(0f, 1f)] public float inventorySoundVolume = RuntimeInventorySoundVolume;

    PlayerInteraction playerInteraction;
    PlayerMovement playerMovement;
    PlayerProgression playerProgression;
    PlayerEquipment playerEquipment;
    Inventory inventory;
    Hotbar hotbar;
    InputAction toggleInventoryAction;
    AudioSource uiAudioSource;

    InventoryCategory selectedCategory = InventoryCategory.All;
    InventoryItem selectedItem;
    string searchQuery = "";

    TextMeshProUGUI summaryText;
    TextMeshProUGUI playerStatsText;
    TextMeshProUGUI weightText;
    TextMeshProUGUI detailsNameText;
    TextMeshProUGUI detailsDescriptionText;
    TextMeshProUGUI detailsStatsText;
    TextMeshProUGUI hotbarText;
    Image detailsIcon;
    TMP_InputField searchInput;
    Button useButton;
    Button equipButton;
    Button splitButton;
    Button discardButton;
    GameObject runtimePanel;
    readonly Dictionary<InventoryCategory, Button> categoryButtons = new Dictionary<InventoryCategory, Button>();
    readonly Dictionary<EquipmentSlotType, TextMeshProUGUI> equipmentSlotTexts = new Dictionary<EquipmentSlotType, TextMeshProUGUI>();

    void Awake()
    {
        toggleInventoryAction = new InputAction("ToggleInventory", binding: "<Keyboard>/tab");
        inventorySoundVolume = RuntimeInventorySoundVolume;
        EnsureUiAudioSource();
        LoadDefaultSoundsIfNeeded();
        PreloadInventorySounds();
        BuildRuntimeUi();
    }

    void OnEnable()
    {
        toggleInventoryAction.Enable();
    }

    void OnDisable()
    {
        toggleInventoryAction.Disable();
    }

    void Start()
    {
        ResolveReferences();
        LoadDefaultSoundsIfNeeded();
        BuildRuntimeUi();

        if (panel != null)
            panel.SetActive(false);

        inventorySoundVolume = RuntimeInventorySoundVolume;
    }

    void Update()
    {
        ResolveReferences();

        if (GameState.IsPaused || GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsPowerSelectionOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen || GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen)
            return;

        if (toggleInventoryAction.WasPressedThisFrame())
            Toggle();
    }

    void Toggle()
    {
        BuildRuntimeUi();

        if (panel == null || GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen)
            return;

        ResolveReferences();

        bool isOpen = !panel.activeSelf;
        panel.SetActive(isOpen);

        GameState.IsInventoryOpen = isOpen;
        PlayToggleSound(isOpen);

        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerMovement != null)
                playerMovement.enabled = false;

            Refresh();
        }
        else
        {
            GameState.LastUiCloseFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerMovement != null)
                playerMovement.enabled = true;
        }
    }

    public void Refresh()
    {
        ResolveReferences();
        BuildRuntimeUi();

        if (inventory == null || content == null)
            return;

        if (selectedItem != null && !inventory.items.Contains(selectedItem))
            selectedItem = null;

        RefreshSummary();
        RefreshPlayerStats();
        RefreshEquipmentSlots();
        RefreshCategoryButtons();
        RefreshGrid();
        RefreshDetails();
        RefreshHotbarPreview();
    }

    public void SelectItem(InventoryItem item)
    {
        selectedItem = item;
        RefreshDetails();
    }

    void RefreshSummary()
    {
        if (summaryText == null || inventory == null)
            return;

        int totalItems = inventory.items.Sum(item => item != null ? Mathf.Max(0, item.quantity) : 0);
        summaryText.text = $"Slots {inventory.UsedSlots}/{inventory.maxSlots}  |  Itens {totalItems}";
    }

    void RefreshPlayerStats()
    {
        if (playerStatsText == null)
            return;

        float health = playerMovement != null ? playerMovement.currentHealth : 0f;
        float maxHealth = playerMovement != null ? playerMovement.maxHealth : 0f;
        float stamina = playerMovement != null ? playerMovement.currentStamina : 0f;
        float maxStamina = playerMovement != null ? playerMovement.maxStamina : 0f;
        float hunger = playerMovement != null ? playerMovement.currentHunger : 0f;
        float maxHunger = playerMovement != null ? playerMovement.maxHunger : 0f;
        int level = playerProgression != null ? playerProgression.currentLevel : 1;
        int xp = playerProgression != null ? playerProgression.currentXp : 0;
        int nextXp = playerProgression != null ? playerProgression.GetXpRequiredForNextLevel() : 0;
        int defense = playerEquipment != null ? playerEquipment.GetTotalDefense() : 0;
        float speedBonus = playerEquipment != null ? playerEquipment.GetMoveSpeedBonus() : 0f;
        int goldAmount = 0;
        InventoryItem goldItem = inventory != null ? inventory.GetItem("Gold") : null;
        if (goldItem != null)
            goldAmount = goldItem.quantity;

        DayNightCycle cycle = DayNightCycle.Instance ?? SceneObjectCache.Find<DayNightCycle>(gameObject.scene, true);
        string dayLine = cycle != null
            ? $"Dia {cycle.CurrentDay}  Hora {cycle.CurrentTimeFormatted}"
            : "Dia --  Hora --:--";

        playerStatsText.text =
            $"{dayLine}\n" +
            $"Gold {goldAmount}\n" +
            $"Nivel {level}  XP {xp}/{Mathf.Max(xp, nextXp)}\n" +
            $"Vida {health:0}/{maxHealth:0}\n" +
            $"Stamina {stamina:0}/{maxStamina:0}\n" +
            $"Fome {hunger:0}/{maxHunger:0}\n" +
            $"Defesa {defense}\n" +
            $"Velocidade {(1f + speedBonus) * 100f:0}%";

        if (weightText == null || inventory == null)
            return;

        float carriedWeight = inventory.GetTotalWeight();
        float weightPercent = inventory.GetWeightPercent();
        string status = "Normal";
        Color color = new Color(0.94f, 0.88f, 0.76f, 1f);

        if (weightPercent >= Inventory.WeightRunBlockPercent)
        {
            status = "Sem corrida";
            color = new Color(1f, 0.42f, 0.32f, 1f);
        }
        else if (weightPercent >= Inventory.WeightSlowPercent)
        {
            status = "Lento";
            color = new Color(1f, 0.68f, 0.28f, 1f);
        }
        else if (weightPercent >= Inventory.WeightWarningPercent)
        {
            status = "Pesado";
            color = new Color(1f, 0.86f, 0.36f, 1f);
        }

        weightText.text = $"Peso {carriedWeight:0.0}/{inventory.maxCarryWeight:0.0}  {status}";
        weightText.color = color;
    }

    void RefreshEquipmentSlots()
    {
        foreach (EquipmentSlotType slot in EquipmentSlots)
        {
            if (!equipmentSlotTexts.TryGetValue(slot, out TextMeshProUGUI text) || text == null)
                continue;

            InventoryItem item = playerEquipment != null ? playerEquipment.GetEquipped(slot) : null;
            text.text = item != null ? $"{GetEquipmentLabel(slot)}: {item.itemName}" : $"{GetEquipmentLabel(slot)}: Vazio";
        }
    }

    void RefreshCategoryButtons()
    {
        foreach (KeyValuePair<InventoryCategory, Button> pair in categoryButtons)
        {
            Image image = pair.Value != null ? pair.Value.GetComponent<Image>() : null;
            if (image == null)
                continue;

            image.color = pair.Key == selectedCategory
                ? new Color(0.66f, 0.44f, 0.18f, 1f)
                : new Color(0.24f, 0.17f, 0.09f, 0.95f);
        }
    }

    void RefreshGrid()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        List<InventoryItem> visibleItems = GetVisibleItems();
        int slotCount = Mathf.Max(inventory.maxSlots, visibleItems.Count);

        for (int i = 0; i < slotCount; i++)
        {
            InventoryItem item = i < visibleItems.Count ? visibleItems[i] : null;
            InventorySlotUI slot = CreateInventorySlot(content, item, i);
            slot.Setup(item);
        }
    }

    List<InventoryItem> GetVisibleItems()
    {
        IEnumerable<InventoryItem> query = inventory.items.Where(item => item != null && item.quantity > 0);

        if (selectedCategory != InventoryCategory.All)
            query = query.Where(item => item.category == selectedCategory);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string term = searchQuery.Trim();
            query = query.Where(item =>
                item.itemName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrWhiteSpace(item.description) && item.description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        return query
            .OrderBy(item => item.category)
            .ThenBy(item => item.itemName)
            .ToList();
    }

    void RefreshDetails()
    {
        if (selectedItem == null)
        {
            if (detailsIcon != null)
            {
                detailsIcon.sprite = null;
                detailsIcon.enabled = false;
            }

            if (detailsNameText != null)
                detailsNameText.text = "Nenhum item selecionado";

            if (detailsDescriptionText != null)
                detailsDescriptionText.text = "Selecione um slot para ver detalhes, usar, equipar, dividir ou descartar.";

            if (detailsStatsText != null)
                detailsStatsText.text = "";

            SetActionButtons(false, false, false, false);
            return;
        }

        if (detailsIcon != null)
        {
            detailsIcon.sprite = selectedItem.GetDisplayIcon();
            detailsIcon.enabled = detailsIcon.sprite != null;
            detailsIcon.preserveAspect = true;
        }

        if (detailsNameText != null)
            detailsNameText.text = $"{selectedItem.itemName} x{selectedItem.quantity}";

        if (detailsDescriptionText != null)
            detailsDescriptionText.text = selectedItem.description;

        if (detailsStatsText != null)
        {
            List<string> lines = new List<string>
            {
                $"Categoria: {GetCategoryLabel(selectedItem.category)}",
                $"Raridade: {GetRarityLabel(selectedItem.rarity)}",
                $"Peso: {selectedItem.weight:0.##} cada",
                $"Stack: {selectedItem.quantity}/{selectedItem.GetMaxStack()}"
            };

            if (selectedItem.isConsumable)
                lines.Add($"Restaura: Vida {selectedItem.healthRestore:0}, Fome {selectedItem.hungerRestore:0}, Sede {selectedItem.thirstRestore:0}");

            if (selectedItem.IsEquipment())
            {
                lines.Add($"Slot: {GetEquipmentLabel(selectedItem.equipmentSlot)}");
                lines.Add($"Dano: {(selectedItem.itemData != null ? selectedItem.itemData.toolDamage : 0)}");
                lines.Add($"Defesa: {selectedItem.defense}");
                lines.Add($"Durabilidade: {selectedItem.durability}");
            }

            detailsStatsText.text = string.Join("\n", lines);
        }

        SetActionButtons(
            selectedItem.isConsumable && (selectedItem.healthRestore > 0f || selectedItem.hungerRestore > 0f || selectedItem.thirstRestore > 0f),
            selectedItem.IsEquipment(),
            selectedItem.quantity > 1 && inventory != null && inventory.FreeSlots > 0,
            true);
    }

    void SetActionButtons(bool canUse, bool canEquip, bool canSplit, bool canDiscard)
    {
        if (useButton != null)
            useButton.interactable = canUse;

        if (equipButton != null)
            equipButton.interactable = canEquip;

        if (splitButton != null)
            splitButton.interactable = canSplit;

        if (discardButton != null)
            discardButton.interactable = canDiscard;
    }

    void RefreshHotbarPreview()
    {
        if (hotbarText == null)
            return;

        if (hotbar == null || hotbar.slots == null)
        {
            hotbarText.text = "Hotbar: vazia";
            return;
        }

        List<string> slots = new List<string>();
        for (int i = 0; i < hotbar.slots.Length; i++)
        {
            HotbarSlot slot = hotbar.slots[i];
            string label = slot != null && !slot.IsEmpty() ? slot.ItemName : "-";
            slots.Add($"{i + 1}: {label}");
        }

        hotbarText.text = string.Join("  |  ", slots);
    }

    void UseSelected()
    {
        if (selectedItem == null || inventory == null || playerMovement == null)
            return;

        if (!selectedItem.isConsumable)
            return;

        playerMovement.Heal(selectedItem.healthRestore);
        playerMovement.RestoreHunger(selectedItem.hungerRestore);
        playerMovement.RestoreThirst(selectedItem.thirstRestore);
        inventory.RemoveItem(selectedItem, 1);
        hotbar?.RemoveInventoryItem(selectedItem, 1);
        MessageSystem.Instance?.ShowMessage($"{selectedItem.itemName} usado");
        Refresh();
    }

    void EquipSelected()
    {
        if (selectedItem == null || inventory == null || playerEquipment == null)
            return;

        if (playerEquipment.EquipFromInventory(inventory, selectedItem))
            Refresh();
    }

    void SplitSelected()
    {
        if (selectedItem == null || inventory == null)
            return;

        if (inventory.SplitStack(selectedItem))
            Refresh();
    }

    void DiscardSelected()
    {
        if (selectedItem == null || inventory == null)
            return;

        string name = selectedItem.itemName;
        inventory.DiscardItem(selectedItem, selectedItem.quantity);
        selectedItem = null;
        MessageSystem.Instance?.ShowMessage($"{name} descartado");
        Refresh();
    }

    void SortInventory()
    {
        if (inventory == null)
            return;

        inventory.SortByCategoryAndName();
        Refresh();
    }

    void UnequipSlot(EquipmentSlotType slot)
    {
        if (playerEquipment == null || inventory == null)
            return;

        if (playerEquipment.UnequipToInventory(inventory, slot))
            Refresh();
    }

    void PlayToggleSound(bool isOpen)
    {
        LoadDefaultSoundsIfNeeded();
        EnsureUiAudioSource();

        if (uiAudioSource == null)
            return;

        AudioClip clip = isOpen ? openInventorySound : closeInventorySound;
        if (clip == null)
            return;

        uiAudioSource.Stop();
        uiAudioSource.clip = clip;
        uiAudioSource.volume = RuntimeInventorySoundVolume;
        float startOffset = isOpen ? OpenInventorySoundStartOffset : CloseInventorySoundStartOffset;
        uiAudioSource.time = Mathf.Min(startOffset, Mathf.Max(0f, clip.length - 0.01f));
        uiAudioSource.Play();
    }

    void ResolveReferences()
    {
        PlayerMovement resolvedPlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (resolvedPlayer == null)
            return;

        playerMovement = resolvedPlayer;
        inventory = playerMovement.GetComponent<Inventory>();
        playerInteraction = playerMovement.GetComponent<PlayerInteraction>();
        playerProgression = playerMovement.GetComponent<PlayerProgression>();
        playerEquipment = playerMovement.GetComponent<PlayerEquipment>() ?? playerMovement.gameObject.AddComponent<PlayerEquipment>();
        hotbar = playerMovement.GetComponent<Hotbar>() ?? SceneObjectCache.Find<Hotbar>(playerMovement.gameObject.scene, true);
    }

    void BuildRuntimeUi()
    {
        if (runtimePanel != null && content != null && summaryText != null)
        {
            panel = runtimePanel;
            return;
        }

        if (panel != null && panel != runtimePanel)
            panel.SetActive(false);

        UIEventSystemUtility.EnsureSingleEventSystem();

        GameObject canvasObject = new GameObject("InventoryRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 135;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        runtimePanel = canvasObject;
        panel = runtimePanel;

        GameObject overlay = CreatePanel("Overlay", canvasObject.transform, new Color(0.02f, 0.018f, 0.014f, 0.78f));
        overlay.GetComponent<Image>().raycastTarget = false;
        Stretch(overlay.GetComponent<RectTransform>());

        GameObject window = CreatePanel("InventoryWindow", overlay.transform, new Color(0.12f, 0.09f, 0.055f, 0.98f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1540f, 890f);
        windowRect.anchoredPosition = Vector2.zero;
        AddOutline(window, new Color(0.66f, 0.45f, 0.18f, 1f), new Vector2(3f, -3f));

        VerticalLayoutGroup rootLayout = window.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(24, 24, 20, 20);
        rootLayout.spacing = 12f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        BuildHeader(window.transform);
        BuildBody(window.transform);
        BuildFooter(window.transform);

        panel.SetActive(false);
    }

    void BuildHeader(Transform parent)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(parent, false);
        LayoutElement layout = header.AddComponent<LayoutElement>();
        layout.preferredHeight = 72f;

        HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 16f;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;

        TextMeshProUGUI title = CreateText("Title", header.transform, "Inventario", 38f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = new Color(1f, 0.86f, 0.48f, 1f);
        title.GetComponent<LayoutElement>().preferredWidth = 320f;

        summaryText = CreateText("Summary", header.transform, "", 22f, FontStyles.Bold, TextAlignmentOptions.Left);
        summaryText.GetComponent<LayoutElement>().flexibleWidth = 1f;

        weightText = CreateText("Weight", header.transform, "", 22f, FontStyles.Bold, TextAlignmentOptions.Right);
        weightText.GetComponent<LayoutElement>().preferredWidth = 420f;

        CreateButton("CloseButton", header.transform, "TAB", () => Toggle(), 82f, 50f);
    }

    void BuildBody(Transform parent)
    {
        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(parent, false);
        LayoutElement bodyLayoutElement = body.AddComponent<LayoutElement>();
        bodyLayoutElement.flexibleHeight = 1f;
        bodyLayoutElement.preferredHeight = 700f;

        HorizontalLayoutGroup bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 14f;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;

        BuildPlayerColumn(body.transform);
        BuildInventoryColumn(body.transform);
        BuildDetailsColumn(body.transform);
    }

    void BuildPlayerColumn(Transform parent)
    {
        GameObject column = CreatePanel("PlayerColumn", parent, new Color(0.18f, 0.13f, 0.075f, 0.96f));
        LayoutElement layout = column.AddComponent<LayoutElement>();
        layout.preferredWidth = 300f;
        layout.flexibleHeight = 1f;

        VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.padding = new RectOffset(14, 14, 14, 14);
        columnLayout.spacing = 12f;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;

        CreateText("PlayerTitle", column.transform, "Personagem", 25f, FontStyles.Bold, TextAlignmentOptions.Center);
        playerStatsText = CreateText("PlayerStats", column.transform, "", 19f, FontStyles.Normal, TextAlignmentOptions.Left);
        playerStatsText.GetComponent<LayoutElement>().preferredHeight = 235f;

        CreateText("EquipmentTitle", column.transform, "Equipamento", 24f, FontStyles.Bold, TextAlignmentOptions.Center);

        foreach (EquipmentSlotType slot in EquipmentSlots)
        {
            Button rowButton = CreateButton($"{slot}Slot", column.transform, "", () => UnequipSlot(slot), 0f, 44f);
            rowButton.GetComponent<LayoutElement>().preferredHeight = 44f;
            TextMeshProUGUI label = rowButton.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 17f;
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(10f, 0f, 6f, 0f);
            equipmentSlotTexts[slot] = label;
        }
    }

    void BuildInventoryColumn(Transform parent)
    {
        GameObject column = CreatePanel("InventoryColumn", parent, new Color(0.16f, 0.115f, 0.07f, 0.96f));
        LayoutElement layout = column.AddComponent<LayoutElement>();
        layout.preferredWidth = 770f;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 1f;

        VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.padding = new RectOffset(14, 14, 14, 14);
        columnLayout.spacing = 12f;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;

        BuildSearchAndSort(column.transform);
        BuildCategoryBar(column.transform);

        GameObject scrollObject = CreatePanel("InventoryScroll", column.transform, new Color(0.06f, 0.048f, 0.036f, 0.82f));
        LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.preferredHeight = 520f;

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = CreatePanel("Viewport", scrollObject.transform, new Color(0f, 0f, 0f, 0f));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = viewportRect;

        GameObject gridObject = new GameObject("Content", typeof(RectTransform));
        gridObject.transform.SetParent(viewport.transform, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 1f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(96f, 96f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;

        ContentSizeFitter fitter = gridObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        content = gridObject.transform;
        scroll.content = gridRect;
    }

    void BuildSearchAndSort(Transform parent)
    {
        GameObject row = new GameObject("SearchRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement rowLayoutElement = row.AddComponent<LayoutElement>();
        rowLayoutElement.preferredHeight = 52f;

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        searchInput = CreateInput(row.transform);
        searchInput.GetComponent<LayoutElement>().flexibleWidth = 1f;
        searchInput.onValueChanged.AddListener(value =>
        {
            searchQuery = value;
            Refresh();
        });

        CreateButton("SortButton", row.transform, "Ordenar", SortInventory, 140f, 48f);
    }

    void BuildCategoryBar(Transform parent)
    {
        GameObject categoryBar = new GameObject("CategoryBar", typeof(RectTransform));
        categoryBar.transform.SetParent(parent, false);
        LayoutElement layout = categoryBar.AddComponent<LayoutElement>();
        layout.preferredHeight = 48f;

        HorizontalLayoutGroup barLayout = categoryBar.AddComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 7f;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = false;
        barLayout.childForceExpandHeight = true;

        foreach (InventoryCategory category in CategoryOrder)
        {
            Button button = CreateButton(
                $"{category}Filter",
                categoryBar.transform,
                GetCategoryLabel(category),
                () =>
                {
                    selectedCategory = category;
                    Refresh();
                },
                0f,
                42f);

            button.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 16f;
            categoryButtons[category] = button;
        }
    }

    void BuildDetailsColumn(Transform parent)
    {
        GameObject column = CreatePanel("DetailsColumn", parent, new Color(0.18f, 0.13f, 0.075f, 0.96f));
        LayoutElement layout = column.AddComponent<LayoutElement>();
        layout.preferredWidth = 410f;
        layout.flexibleHeight = 1f;

        VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.padding = new RectOffset(16, 16, 16, 16);
        columnLayout.spacing = 8f;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;

        GameObject iconFrame = CreatePanel("DetailIconFrame", column.transform, new Color(0.08f, 0.064f, 0.045f, 1f));
        LayoutElement iconLayout = iconFrame.AddComponent<LayoutElement>();
        iconLayout.preferredHeight = 168f;
        detailsIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        detailsIcon.transform.SetParent(iconFrame.transform, false);
        Stretch(detailsIcon.GetComponent<RectTransform>(), new Vector2(10f, 10f), new Vector2(-10f, -10f));
        detailsIcon.raycastTarget = false;

        detailsNameText = CreateText("DetailsName", column.transform, "", 25f, FontStyles.Bold, TextAlignmentOptions.Center);
        detailsNameText.GetComponent<LayoutElement>().preferredHeight = 56f;

        detailsDescriptionText = CreateText("DetailsDescription", column.transform, "", 18f, FontStyles.Normal, TextAlignmentOptions.Left);
        detailsDescriptionText.GetComponent<LayoutElement>().preferredHeight = 96f;

        detailsStatsText = CreateText("DetailsStats", column.transform, "", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
        detailsStatsText.GetComponent<LayoutElement>().preferredHeight = 132f;

        useButton = CreateButton("UseButton", column.transform, "Usar", UseSelected, 0f, 44f);
        equipButton = CreateButton("EquipButton", column.transform, "Equipar", EquipSelected, 0f, 44f);
        splitButton = CreateButton("SplitButton", column.transform, "Dividir", SplitSelected, 0f, 44f);
        discardButton = CreateButton("DiscardButton", column.transform, "Descartar", DiscardSelected, 0f, 44f);
    }

    void BuildFooter(Transform parent)
    {
        GameObject footer = CreatePanel("Footer", parent, new Color(0.1f, 0.075f, 0.046f, 0.92f));
        LayoutElement layout = footer.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;

        HorizontalLayoutGroup footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
        footerLayout.padding = new RectOffset(14, 14, 8, 8);
        footerLayout.spacing = 10f;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = true;

        hotbarText = CreateText("HotbarText", footer.transform, "", 16f, FontStyles.Normal, TextAlignmentOptions.Left);
        hotbarText.textWrappingMode = TextWrappingModes.NoWrap;
        hotbarText.overflowMode = TextOverflowModes.Ellipsis;
    }

    InventorySlotUI CreateInventorySlot(Transform parent, InventoryItem item, int index)
    {
        GameObject slotObject = CreatePanel($"Slot_{index + 1}", parent, new Color(0.1f, 0.075f, 0.046f, item != null ? 1f : 0.68f));
        AddOutline(slotObject, item == selectedItem ? new Color(0.95f, 0.76f, 0.24f, 1f) : new Color(0.42f, 0.29f, 0.12f, 0.9f), new Vector2(1f, -1f));

        Image icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(slotObject.transform, false);
        Stretch(icon.GetComponent<RectTransform>(), new Vector2(5f, 5f), new Vector2(-5f, -9f));
        icon.raycastTarget = false;

        TextMeshProUGUI amount = CreateText("Amount", slotObject.transform, "", 21f, FontStyles.Bold, TextAlignmentOptions.BottomRight);
        Stretch(amount.GetComponent<RectTransform>(), new Vector2(4f, 4f), new Vector2(-6f, -4f));
        amount.raycastTarget = false;

        InventorySlotUI slot = slotObject.AddComponent<InventorySlotUI>();
        slot.icon = icon;
        slot.amountText = amount;
        return slot;
    }

    GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }

    Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction action, float width, float height)
    {
        GameObject buttonObject = CreatePanel(name, parent, new Color(0.34f, 0.22f, 0.09f, 1f));
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        if (width > 0f)
            layout.preferredWidth = width;
        if (height > 0f)
            layout.preferredHeight = height;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.34f, 0.22f, 0.09f, 1f);
        colors.highlightedColor = new Color(0.56f, 0.37f, 0.14f, 1f);
        colors.pressedColor = new Color(0.22f, 0.13f, 0.055f, 1f);
        colors.disabledColor = new Color(0.18f, 0.15f, 0.11f, 0.65f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.GetComponent<RectTransform>(), new Vector2(6f, 2f), new Vector2(-6f, -2f));
        text.raycastTarget = false;
        return button;
    }

    TMP_InputField CreateInput(Transform parent)
    {
        GameObject inputObject = CreatePanel("SearchInput", parent, new Color(0.07f, 0.058f, 0.042f, 1f));
        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 48f;

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.targetGraphic = inputObject.GetComponent<Image>();

        TextMeshProUGUI text = CreateText("Text", inputObject.transform, "", 19f, FontStyles.Normal, TextAlignmentOptions.Left);
        Stretch(text.GetComponent<RectTransform>(), new Vector2(14f, 4f), new Vector2(-14f, -4f));
        input.textComponent = text;

        TextMeshProUGUI placeholder = CreateText("Placeholder", inputObject.transform, "Buscar item", 19f, FontStyles.Italic, TextAlignmentOptions.Left);
        placeholder.color = new Color(0.86f, 0.78f, 0.64f, 0.55f);
        Stretch(placeholder.GetComponent<RectTransform>(), new Vector2(14f, 4f), new Vector2(-14f, -4f));
        input.placeholder = placeholder;

        return input;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.minHeight = Mathf.Max(24f, fontSize + 8f);

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        textComponent.color = new Color(0.96f, 0.89f, 0.76f, 1f);
        textComponent.enableAutoSizing = true;
        textComponent.fontSizeMin = Mathf.Max(10f, fontSize * 0.68f);
        textComponent.fontSizeMax = fontSize;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        return textComponent;
    }

    void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    void LoadDefaultSoundsIfNeeded()
    {
        if (openInventorySound == null)
            openInventorySound = Resources.Load<AudioClip>(OpenInventoryClipPath);

        if (closeInventorySound == null)
            closeInventorySound = Resources.Load<AudioClip>(CloseInventoryClipPath);
    }

    void PreloadInventorySounds()
    {
        if (openInventorySound != null)
            openInventorySound.LoadAudioData();

        if (closeInventorySound != null)
            closeInventorySound.LoadAudioData();
    }

    void EnsureUiAudioSource()
    {
        if (uiAudioSource != null)
            return;

        uiAudioSource = GetComponent<AudioSource>();
        if (uiAudioSource == null)
            uiAudioSource = gameObject.AddComponent<AudioSource>();

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
    }

    static string GetCategoryLabel(InventoryCategory category)
    {
        switch (category)
        {
            case InventoryCategory.All:
                return "Todos";
            case InventoryCategory.Resources:
                return "Recursos";
            case InventoryCategory.Food:
                return "Comida";
            case InventoryCategory.Tools:
                return "Ferramentas";
            case InventoryCategory.Weapons:
                return "Armas";
            case InventoryCategory.Armor:
                return "Armaduras";
            case InventoryCategory.Consumables:
                return "Consumiveis";
            case InventoryCategory.Special:
                return "Especiais";
            default:
                return "Outros";
        }
    }

    static string GetRarityLabel(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return "Incomum";
            case ItemRarity.Rare:
                return "Raro";
            case ItemRarity.Epic:
                return "Epico";
            case ItemRarity.Legendary:
                return "Lendario";
            default:
                return "Comum";
        }
    }

    static string GetEquipmentLabel(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Head:
                return "Cabeca";
            case EquipmentSlotType.Chest:
                return "Peito";
            case EquipmentSlotType.Legs:
                return "Pernas";
            case EquipmentSlotType.Boots:
                return "Botas";
            case EquipmentSlotType.MainHand:
                return "Mao";
            case EquipmentSlotType.Shield:
                return "Escudo";
            case EquipmentSlotType.Accessory:
                return "Acessorio";
            default:
                return "Nenhum";
        }
    }
}
