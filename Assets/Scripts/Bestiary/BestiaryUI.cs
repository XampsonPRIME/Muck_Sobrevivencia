using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BestiaryUI : MonoBehaviour
{
    const int CanvasSortingOrder = 135;

    public static BestiaryUI Instance { get; private set; }

    readonly Color overlayColor = new Color(0.03f, 0.025f, 0.02f, 0.72f);
    readonly Color bookColor = new Color(0.61f, 0.45f, 0.28f, 0.98f);
    readonly Color pageColor = new Color(0.86f, 0.76f, 0.58f, 1f);
    readonly Color pageDarkColor = new Color(0.74f, 0.58f, 0.36f, 1f);
    readonly Color inkColor = new Color(0.14f, 0.09f, 0.05f, 1f);
    readonly Color mutedInkColor = new Color(0.33f, 0.24f, 0.16f, 1f);
    readonly Color selectedColor = new Color(0.43f, 0.25f, 0.11f, 1f);
    readonly Color buttonColor = new Color(0.51f, 0.34f, 0.17f, 0.96f);

    InputAction toggleAction;
    InputAction closeAction;
    Canvas canvas;
    GameObject overlayObject;
    RectTransform listContent;
    RectTransform detailRoot;
    TextMeshProUGUI statsText;
    TextMeshProUGUI sortButtonText;
    TextMeshProUGUI rarityButtonText;
    TMP_InputField searchInput;
    PlayerMovement currentPlayerMovement;
    PlayerInteraction currentPlayerInteraction;
    BestiaryService subscribedService;

    BestiaryBiome currentBiomeFilter = BestiaryBiome.All;
    BestiaryRarity currentRarityFilter = BestiaryRarity.All;
    BestiarySortMode currentSortMode = BestiarySortMode.Recent;
    string selectedCreatureId;
    bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<BestiaryUI>() != null)
            return;

        GameObject uiObject = new GameObject("BestiaryUI");
        uiObject.AddComponent<BestiaryUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        toggleAction = new InputAction("ToggleBestiary", binding: "<Keyboard>/b");
        closeAction = new InputAction("CloseBestiary", binding: "<Keyboard>/escape");
    }

    void OnEnable()
    {
        toggleAction.Enable();
        closeAction.Enable();
    }

    void OnDisable()
    {
        toggleAction.Disable();
        closeAction.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (subscribedService != null)
            subscribedService.OnProgressChanged -= Refresh;

        GameState.IsBestiaryOpen = false;
    }

    void Start()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
        {
            Destroy(gameObject);
            return;
        }

        UIEventSystemUtility.EnsureSingleEventSystem();
        BuildUi();
        SetVisible(false);
    }

    void Update()
    {
        EnsureServiceSubscription();

        if (isOpen && (GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsPowerSelectionOpen || GameState.IsPlayerDead || GameState.IsPaused))
        {
            Close();
            return;
        }

        if (toggleAction.WasPressedThisFrame())
        {
            if (isOpen)
                Close();
            else if (CanOpen())
                Open();
        }

        if (isOpen && closeAction.WasPressedThisFrame())
            Close();
    }

    public void Open()
    {
        if (!CanOpen())
            return;

        ResolvePlayer();
        UIEventSystemUtility.EnsureSingleEventSystem();
        BuildUi();

        GameState.IsBestiaryOpen = true;
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
        GameState.IsBestiaryOpen = false;
        GameState.LastUiCloseFrame = Time.frameCount;
        SetVisible(false);

        if (currentPlayerMovement != null && !GameState.IsPlayerDead && !GameState.IsPaused && !GameState.IsInLobby && !GameState.IsWorldLoading)
            currentPlayerMovement.enabled = true;

        if (currentPlayerInteraction != null && !GameState.IsPlayerDead && !GameState.IsPaused && !GameState.IsInLobby && !GameState.IsWorldLoading)
            currentPlayerInteraction.enabled = true;

        if (!GameState.IsPaused && !GameState.IsWorldLoading && !GameState.IsInventoryOpen && !GameState.IsVendorOpen && !GameState.IsCraftingOpen && !GameState.IsQuestJournalOpen && !GameState.IsInLobby)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Refresh()
    {
        if (!isOpen)
            return;

        RefreshStats();
        RefreshList();
        RefreshDetails();
    }

    bool CanOpen()
    {
        return !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPowerSelectionOpen &&
               !GameState.IsPlayerDead &&
               !GameState.IsPaused &&
               !GameState.IsInventoryOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen &&
               !GameState.IsQuestJournalOpen &&
               !GameState.IsDebugChatOpen;
    }

    void RefreshStats()
    {
        BestiaryService service = BestiaryService.Instance;
        int total = BestiaryDatabase.Creatures.Count;
        int discovered = service != null ? service.CountDiscovered() : 0;
        int totalDefeats = service != null ? service.CountTotalDefeats() : 0;
        int completion = service != null ? service.GetCompletionPercent() : 0;

        if (statsText != null)
            statsText.text = $"Descobertos {discovered}/{total}    Derrotas {totalDefeats}    Conclusao {completion}%";

        if (sortButtonText != null)
            sortButtonText.text = $"Ordem: {GetSortLabel(currentSortMode)}";

        if (rarityButtonText != null)
            rarityButtonText.text = $"Raridade: {GetRarityLabel(currentRarityFilter)}";
    }

    void RefreshList()
    {
        if (listContent == null)
            return;

        ClearChildren(listContent);
        List<BestiaryCreatureData> filtered = BuildFilteredCreatureList();

        if ((string.IsNullOrWhiteSpace(selectedCreatureId) || !ContainsCreature(filtered, selectedCreatureId)) && filtered.Count > 0)
            selectedCreatureId = filtered[0].creatureId;

        for (int i = 0; i < filtered.Count; i++)
            CreateCreatureRow(filtered[i]);

        if (filtered.Count == 0)
        {
            TextMeshProUGUI emptyText = CreateText("EmptyText", listContent, "Nenhuma criatura encontrada.", 17f, mutedInkColor, TextAlignmentOptions.Center);
            LayoutElement layout = emptyText.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 64f;
        }
    }

    void RefreshDetails()
    {
        if (detailRoot == null)
            return;

        ClearChildren(detailRoot);

        BestiaryCreatureData data = BestiaryDatabase.Get(selectedCreatureId);
        if (data == null)
        {
            CreateText("NoSelection", detailRoot, "Selecione uma criatura.", 22f, inkColor, TextAlignmentOptions.Center);
            return;
        }

        BestiaryService service = BestiaryService.Instance;
        BestiaryDiscoveryState state = service != null ? service.GetState(data.creatureId) : BestiaryDiscoveryState.NotDiscovered;
        SaveBestiaryEntryData progress = service != null ? service.GetProgress(data.creatureId) : null;

        GameObject header = CreateUiObject("DetailHeader", detailRoot);
        HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 16f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = false;

        Image portrait = CreateImage("Portrait", header.transform, data.illustration, pageDarkColor);
        LayoutElement portraitLayout = portrait.gameObject.AddComponent<LayoutElement>();
        portraitLayout.preferredWidth = 220f;
        portraitLayout.preferredHeight = 220f;
        portrait.preserveAspect = true;
        portrait.color = state == BestiaryDiscoveryState.NotDiscovered ? new Color(0f, 0f, 0f, 0.86f) : Color.white;

        GameObject headerTextRoot = CreateUiObject("DetailHeaderText", header.transform);
        VerticalLayoutGroup headerTextLayout = headerTextRoot.AddComponent<VerticalLayoutGroup>();
        headerTextLayout.spacing = 8f;
        headerTextLayout.childControlHeight = true;
        headerTextLayout.childControlWidth = true;
        LayoutElement headerTextRootLayout = headerTextRoot.AddComponent<LayoutElement>();
        headerTextRootLayout.preferredWidth = 320f;
        headerTextRootLayout.preferredHeight = 220f;

        string displayName = state == BestiaryDiscoveryState.NotDiscovered ? "???" : data.displayName;
        CreateText("CreatureName", headerTextRoot.transform, displayName, 34f, inkColor, TextAlignmentOptions.Left);
        CreateText("CreatureState", headerTextRoot.transform, GetStateLabel(state), 18f, mutedInkColor, TextAlignmentOptions.Left);

        if (progress != null && progress.defeatCount > 0)
            CreateText("CreatureKills", headerTextRoot.transform, $"Derrotadas: {progress.defeatCount}", 18f, mutedInkColor, TextAlignmentOptions.Left);

        AddSectionTitle("Descricao");
        if (state == BestiaryDiscoveryState.NotDiscovered)
            AddParagraph("Criatura ainda nao encontrada.");
        else if (state == BestiaryDiscoveryState.Discovered)
            AddParagraph($"{data.description}\n\nDerrote esta criatura para revelar estatisticas completas e tabela de drops.");
        else
            AddParagraph(data.description);

        AddSectionTitle("Dados Gerais");
        if (state == BestiaryDiscoveryState.NotDiscovered)
            AddParagraph("Tipo: ???\nBioma: ???\nElemento: ???\nHostilidade: ???");
        else
            AddParagraph($"Tipo: {GetTypeLabel(data.creatureType)}\nBioma: {FormatBiomes(data)}\nElemento: {GetElementLabel(data.element)}\nHostilidade: {GetHostilityLabel(data.hostility)}\nComportamento: {data.behavior}");

        AddSectionTitle("Estatisticas");
        if (state == BestiaryDiscoveryState.Defeated)
            AddParagraph($"Vida: {data.maxHealth}\nDano: {FormatNumber(data.damage)}\nVelocidade: {FormatNumber(data.speed)} m/s\nPrimeira descoberta: {FormatTimestamp(progress?.firstDiscoveredAt)}\nPrimeira derrota: {FormatTimestamp(progress?.firstDefeatedAt)}");
        else
            AddParagraph("Vida: ???\nDano: ???\nVelocidade: ???");

        AddSectionTitle("Drops");
        if (state == BestiaryDiscoveryState.Defeated)
            AddDropRows(data);
        else
            AddParagraph("Drops ocultos ate a primeira derrota.");
    }

    void AddSectionTitle(string title)
    {
        TextMeshProUGUI text = CreateText($"Section_{title}", detailRoot, title, 22f, selectedColor, TextAlignmentOptions.Left);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 32f;
    }

    void AddParagraph(string content)
    {
        TextMeshProUGUI text = CreateText("Paragraph", detailRoot, content, 17f, inkColor, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = Mathf.Max(58f, 24f * (content.Split('\n').Length + 1));
    }

    void AddDropRows(BestiaryCreatureData data)
    {
        if (data.drops == null || data.drops.Count == 0)
        {
            AddParagraph("Nenhum drop registrado.");
            return;
        }

        for (int i = 0; i < data.drops.Count; i++)
        {
            BestiaryDropData drop = data.drops[i];
            GameObject row = CreateUiObject($"Drop_{i}", detailRoot);
            Image rowImage = row.AddComponent<Image>();
            rowImage.color = new Color(0.45f, 0.31f, 0.16f, 0.18f);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 60f;

            Sprite icon = drop.icon != null ? drop.icon : BestiaryDatabase.ResolveItemSprite(drop.itemName);
            Image iconImage = CreateImage("DropIcon", row.transform, icon, new Color(0.2f, 0.14f, 0.08f, 0.3f));
            LayoutElement iconLayout = iconImage.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 48f;
            iconLayout.preferredHeight = 48f;
            iconImage.preserveAspect = true;

            string amount = string.IsNullOrWhiteSpace(drop.amountLabel) ? string.Empty : $" x{drop.amountLabel}";
            string chance = $"{Mathf.RoundToInt(drop.dropChance * 100f)}%";
            CreateText("DropText", row.transform, $"{drop.itemName}{amount} - {chance} - {GetRarityLabel(drop.rarity)}", 17f, inkColor, TextAlignmentOptions.Left);
        }
    }

    List<BestiaryCreatureData> BuildFilteredCreatureList()
    {
        string query = searchInput != null ? searchInput.text : string.Empty;
        query = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim().ToLowerInvariant();

        List<BestiaryCreatureData> results = new List<BestiaryCreatureData>();
        BestiaryService service = BestiaryService.Instance;

        foreach (BestiaryCreatureData data in BestiaryDatabase.Creatures)
        {
            if (data == null)
                continue;

            if (currentBiomeFilter != BestiaryBiome.All && !data.HasBiome(currentBiomeFilter))
                continue;

            if (currentRarityFilter != BestiaryRarity.All && data.rarity != currentRarityFilter)
                continue;

            BestiaryDiscoveryState state = service != null ? service.GetState(data.creatureId) : BestiaryDiscoveryState.NotDiscovered;
            string searchableName = state == BestiaryDiscoveryState.NotDiscovered ? "???" : data.displayName;
            if (!string.IsNullOrWhiteSpace(query) && searchableName.ToLowerInvariant().IndexOf(query, System.StringComparison.Ordinal) < 0)
                continue;

            results.Add(data);
        }

        SortCreatures(results);
        return results;
    }

    void SortCreatures(List<BestiaryCreatureData> list)
    {
        BestiaryService service = BestiaryService.Instance;
        list.Sort((a, b) =>
        {
            if (currentSortMode == BestiarySortMode.Alphabetical)
                return string.Compare(GetVisibleName(a, service), GetVisibleName(b, service), System.StringComparison.OrdinalIgnoreCase);

            if (currentSortMode == BestiarySortMode.MostDefeated)
            {
                int aKills = service?.GetProgress(a.creatureId)?.defeatCount ?? 0;
                int bKills = service?.GetProgress(b.creatureId)?.defeatCount ?? 0;
                return bKills.CompareTo(aKills);
            }

            string aDate = service?.GetProgress(a.creatureId)?.firstDiscoveredAt ?? string.Empty;
            string bDate = service?.GetProgress(b.creatureId)?.firstDiscoveredAt ?? string.Empty;
            int dateCompare = string.Compare(bDate, aDate, System.StringComparison.Ordinal);
            if (dateCompare != 0)
                return dateCompare;

            return string.Compare(GetVisibleName(a, service), GetVisibleName(b, service), System.StringComparison.OrdinalIgnoreCase);
        });
    }

    void CreateCreatureRow(BestiaryCreatureData data)
    {
        BestiaryService service = BestiaryService.Instance;
        BestiaryDiscoveryState state = service != null ? service.GetState(data.creatureId) : BestiaryDiscoveryState.NotDiscovered;
        SaveBestiaryEntryData progress = service != null ? service.GetProgress(data.creatureId) : null;

        GameObject row = CreateUiObject($"CreatureRow_{data.creatureId}", listContent);
        Image background = row.AddComponent<Image>();
        background.color = data.creatureId == selectedCreatureId ? new Color(0.42f, 0.26f, 0.12f, 0.82f) : new Color(0.32f, 0.2f, 0.1f, 0.34f);
        Button button = row.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() =>
        {
            selectedCreatureId = data.creatureId;
            RefreshList();
            RefreshDetails();
        });

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 78f;

        Image icon = CreateImage("CreatureIcon", row.transform, data.illustration, pageDarkColor);
        icon.preserveAspect = true;
        icon.color = state == BestiaryDiscoveryState.NotDiscovered ? new Color(0f, 0f, 0f, 0.88f) : Color.white;
        LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 62f;
        iconLayout.preferredHeight = 62f;

        GameObject textRoot = CreateUiObject("CreatureRowText", row.transform);
        VerticalLayoutGroup textLayout = textRoot.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 0f;
        textLayout.childControlHeight = true;
        textLayout.childControlWidth = true;
        LayoutElement textRootLayout = textRoot.AddComponent<LayoutElement>();
        textRootLayout.preferredWidth = 310f;
        textRootLayout.preferredHeight = 60f;

        string name = state == BestiaryDiscoveryState.NotDiscovered ? "???" : data.displayName;
        CreateText("CreatureRowName", textRoot.transform, name, 19f, data.creatureId == selectedCreatureId ? Color.white : inkColor, TextAlignmentOptions.Left);

        int kills = progress != null ? Mathf.Max(0, progress.defeatCount) : 0;
        string sub = state == BestiaryDiscoveryState.NotDiscovered ? "Nao descoberto" : $"{GetStateLabel(state)} - {kills} derrotas";
        CreateText("CreatureRowState", textRoot.transform, sub, 15f, data.creatureId == selectedCreatureId ? new Color(1f, 0.92f, 0.75f) : mutedInkColor, TextAlignmentOptions.Left);
    }

    bool ContainsCreature(List<BestiaryCreatureData> list, string creatureId)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].creatureId == creatureId)
                return true;
        }

        return false;
    }

    string GetVisibleName(BestiaryCreatureData data, BestiaryService service)
    {
        if (data == null)
            return string.Empty;

        BestiaryDiscoveryState state = service != null ? service.GetState(data.creatureId) : BestiaryDiscoveryState.NotDiscovered;
        return state == BestiaryDiscoveryState.NotDiscovered ? "???" : data.displayName;
    }

    void CycleSortMode()
    {
        currentSortMode = currentSortMode == BestiarySortMode.Recent
            ? BestiarySortMode.Alphabetical
            : currentSortMode == BestiarySortMode.Alphabetical
                ? BestiarySortMode.MostDefeated
                : BestiarySortMode.Recent;

        Refresh();
    }

    void CycleRarityFilter()
    {
        currentRarityFilter = currentRarityFilter == BestiaryRarity.All
            ? BestiaryRarity.Common
            : currentRarityFilter == BestiaryRarity.Common
                ? BestiaryRarity.Uncommon
                : currentRarityFilter == BestiaryRarity.Uncommon
                    ? BestiaryRarity.Rare
                    : currentRarityFilter == BestiaryRarity.Rare
                        ? BestiaryRarity.Epic
                        : currentRarityFilter == BestiaryRarity.Epic
                            ? BestiaryRarity.Legendary
                            : BestiaryRarity.All;

        Refresh();
    }

    void BuildUi()
    {
        if (overlayObject != null)
            return;

        GameObject canvasObject = new GameObject("BestiaryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        DisplaySettingsManager.ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>());

        overlayObject = CreateUiObject("BestiaryOverlay", canvasObject.transform);
        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = overlayColor;
        SetStretch(overlayObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject book = CreateUiObject("OpenBook", overlayObject.transform);
        Image bookImage = book.AddComponent<Image>();
        bookImage.color = bookColor;
        RectTransform bookRect = book.GetComponent<RectTransform>();
        bookRect.anchorMin = new Vector2(0.5f, 0.5f);
        bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.sizeDelta = new Vector2(1260f, 760f);
        bookRect.anchoredPosition = Vector2.zero;

        GameObject leftPage = CreatePage("LeftPage", book.transform, new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.95f));
        GameObject rightPage = CreatePage("RightPage", book.transform, new Vector2(0.51f, 0.05f), new Vector2(0.98f, 0.95f));

        GameObject spine = CreateUiObject("BookSpine", book.transform);
        Image spineImage = spine.AddComponent<Image>();
        spineImage.color = new Color(0.29f, 0.16f, 0.08f, 0.75f);
        RectTransform spineRect = spine.GetComponent<RectTransform>();
        spineRect.anchorMin = new Vector2(0.492f, 0.04f);
        spineRect.anchorMax = new Vector2(0.508f, 0.96f);
        spineRect.offsetMin = Vector2.zero;
        spineRect.offsetMax = Vector2.zero;

        BuildLeftPage(leftPage.transform);
        BuildRightPage(rightPage.transform);
    }

    GameObject CreatePage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject page = CreateUiObject(name, parent);
        Image image = page.AddComponent<Image>();
        image.color = pageColor;
        RectTransform rect = page.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return page;
    }

    void BuildLeftPage(Transform parent)
    {
        TextMeshProUGUI title = CreateText("BestiaryTitle", parent, "Bestiario", 34f, inkColor, TextAlignmentOptions.Center);
        SetTop(title.rectTransform, 18f, 18f, 560f, 46f);

        statsText = CreateText("BestiaryStats", parent, string.Empty, 16f, mutedInkColor, TextAlignmentOptions.Center);
        SetTop(statsText.rectTransform, 60f, 20f, 554f, 30f);

        GameObject categoryRoot = CreateUiObject("CategoryButtons", parent);
        RectTransform categoryRect = categoryRoot.GetComponent<RectTransform>();
        SetTop(categoryRect, 96f, 18f, 560f, 80f);
        GridLayoutGroup categoryGrid = categoryRoot.AddComponent<GridLayoutGroup>();
        categoryGrid.cellSize = new Vector2(132f, 32f);
        categoryGrid.spacing = new Vector2(7f, 7f);
        categoryGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        categoryGrid.constraintCount = 4;

        CreateCategoryButton(categoryRoot.transform, "Todos", BestiaryBiome.All);
        CreateCategoryButton(categoryRoot.transform, "Floresta", BestiaryBiome.Forest);
        CreateCategoryButton(categoryRoot.transform, "Planicie", BestiaryBiome.Plains);
        CreateCategoryButton(categoryRoot.transform, "Pantano", BestiaryBiome.Swamp);
        CreateCategoryButton(categoryRoot.transform, "Montanha", BestiaryBiome.Mountain);
        CreateCategoryButton(categoryRoot.transform, "Caverna", BestiaryBiome.Cave);
        CreateCategoryButton(categoryRoot.transform, "Deserto", BestiaryBiome.Desert);

        searchInput = CreateSearchInput(parent);
        SetTop(searchInput.GetComponent<RectTransform>(), 188f, 26f, 540f, 38f);

        Button sortButton = CreateTextButton("SortButton", parent, "Ordem: Recentes", 16f, buttonColor);
        sortButtonText = sortButton.GetComponentInChildren<TextMeshProUGUI>();
        sortButton.onClick.AddListener(CycleSortMode);
        SetTop(sortButton.GetComponent<RectTransform>(), 236f, 26f, 260f, 38f);

        Button rarityButton = CreateTextButton("RarityButton", parent, "Raridade: Todos", 16f, buttonColor);
        rarityButtonText = rarityButton.GetComponentInChildren<TextMeshProUGUI>();
        rarityButton.onClick.AddListener(CycleRarityFilter);
        SetTop(rarityButton.GetComponent<RectTransform>(), 236f, 306f, 260f, 38f);

        GameObject scrollObject = CreateUiObject("CreatureScroll", parent);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetTop(scrollRectTransform, 286f, 26f, 540f, 370f);
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0.18f, 0.1f, 0.04f, 0.08f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        SetStretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject content = CreateUiObject("Content", viewport.transform);
        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = new Vector2(0f, 0f);
        listContent.offsetMax = new Vector2(0f, 0f);
        VerticalLayoutGroup listLayout = content.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 7f;
        listLayout.padding = new RectOffset(4, 10, 4, 4);
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = listContent;
    }

    void BuildRightPage(Transform parent)
    {
        TextMeshProUGUI title = CreateText("DetailTitle", parent, "Registro da Criatura", 30f, inkColor, TextAlignmentOptions.Center);
        SetTop(title.rectTransform, 20f, 20f, 560f, 40f);

        TextMeshProUGUI hint = CreateText("CloseHint", parent, "B ou Esc para fechar", 15f, mutedInkColor, TextAlignmentOptions.Center);
        SetTop(hint.rectTransform, 60f, 20f, 560f, 26f);

        GameObject detailScroll = CreateUiObject("DetailScroll", parent);
        RectTransform detailScrollRect = detailScroll.GetComponent<RectTransform>();
        SetTop(detailScrollRect, 96f, 24f, 552f, 560f);
        ScrollRect scroll = detailScroll.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = CreateUiObject("DetailViewport", detailScroll.transform);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0.18f, 0.1f, 0.04f, 0.04f);
        SetStretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject content = CreateUiObject("DetailContent", viewport.transform);
        detailRoot = content.GetComponent<RectTransform>();
        detailRoot.anchorMin = new Vector2(0f, 1f);
        detailRoot.anchorMax = new Vector2(1f, 1f);
        detailRoot.pivot = new Vector2(0.5f, 1f);
        detailRoot.offsetMin = new Vector2(0f, 0f);
        detailRoot.offsetMax = new Vector2(0f, 0f);
        VerticalLayoutGroup detailLayout = content.AddComponent<VerticalLayoutGroup>();
        detailLayout.spacing = 12f;
        detailLayout.padding = new RectOffset(16, 16, 10, 10);
        detailLayout.childControlWidth = true;
        detailLayout.childControlHeight = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = detailRoot;
    }

    TMP_InputField CreateSearchInput(Transform parent)
    {
        GameObject inputObject = CreateUiObject("SearchInput", parent);
        Image image = inputObject.AddComponent<Image>();
        image.color = new Color(0.95f, 0.84f, 0.62f, 0.92f);
        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Text", inputObject.transform, string.Empty, 17f, inkColor, TextAlignmentOptions.Left);
        SetStretch(text.rectTransform, new Vector2(12f, 4f), new Vector2(12f, 4f));
        TextMeshProUGUI placeholder = CreateText("Placeholder", inputObject.transform, "Buscar criatura...", 17f, new Color(0.33f, 0.24f, 0.16f, 0.62f), TextAlignmentOptions.Left);
        SetStretch(placeholder.rectTransform, new Vector2(12f, 4f), new Vector2(12f, 4f));

        input.textComponent = text;
        input.placeholder = placeholder;
        input.onValueChanged.AddListener(_ => Refresh());
        return input;
    }

    void CreateCategoryButton(Transform parent, string label, BestiaryBiome biome)
    {
        Button button = CreateTextButton($"Category_{biome}", parent, label, 15f, biome == currentBiomeFilter ? selectedColor : buttonColor);
        button.onClick.AddListener(() =>
        {
            currentBiomeFilter = biome;
            RebuildCategoryButtons();
            Refresh();
        });
    }

    void RebuildCategoryButtons()
    {
        if (overlayObject == null)
            return;

        Transform categoryRoot = overlayObject.transform.Find("OpenBook/LeftPage/CategoryButtons");
        if (categoryRoot == null)
            return;

        for (int i = 0; i < categoryRoot.childCount; i++)
        {
            Button button = categoryRoot.GetChild(i).GetComponent<Button>();
            Image image = categoryRoot.GetChild(i).GetComponent<Image>();
            if (button == null || image == null)
                continue;

            string objectName = categoryRoot.GetChild(i).name;
            image.color = objectName.EndsWith(currentBiomeFilter.ToString()) ? selectedColor : buttonColor;
        }
    }

    Button CreateTextButton(string name, Transform parent, string label, float fontSize, Color color)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, fontSize, Color.white, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    void SetVisible(bool visible)
    {
        isOpen = visible;

        if (overlayObject != null)
            overlayObject.SetActive(visible);
    }

    void ResolvePlayer()
    {
        if (currentPlayerMovement == null || !currentPlayerMovement.gameObject.activeInHierarchy)
            currentPlayerMovement = LanMultiplayerManager.FindGameplayPlayer();

        if (currentPlayerInteraction == null && currentPlayerMovement != null)
            currentPlayerInteraction = currentPlayerMovement.GetComponent<PlayerInteraction>();
    }

    void EnsureServiceSubscription()
    {
        BestiaryService service = BestiaryService.Instance;
        if (service == null || subscribedService == service)
            return;

        if (subscribedService != null)
            subscribedService.OnProgressChanged -= Refresh;

        subscribedService = service;
        subscribedService.OnProgressChanged += Refresh;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite, Color fallbackColor)
    {
        GameObject imageObject = CreateUiObject(name, parent);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        return image;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    static void SetTop(RectTransform rect, float top, float left, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    static string FormatBiomes(BestiaryCreatureData data)
    {
        if (data.biomes == null || data.biomes.Count == 0)
            return "Desconhecido";

        List<string> labels = new List<string>();
        for (int i = 0; i < data.biomes.Count; i++)
            labels.Add(GetBiomeLabel(data.biomes[i]));

        return string.Join(", ", labels);
    }

    static string GetBiomeLabel(BestiaryBiome biome)
    {
        return biome switch
        {
            BestiaryBiome.Forest => "Floresta",
            BestiaryBiome.Plains => "Planicie",
            BestiaryBiome.Swamp => "Pantano",
            BestiaryBiome.Mountain => "Montanha",
            BestiaryBiome.Cave => "Caverna",
            BestiaryBiome.Desert => "Deserto",
            _ => "Todos"
        };
    }

    static string GetRarityLabel(BestiaryRarity rarity)
    {
        return rarity switch
        {
            BestiaryRarity.Common => "Comum",
            BestiaryRarity.Uncommon => "Incomum",
            BestiaryRarity.Rare => "Raro",
            BestiaryRarity.Epic => "Epico",
            BestiaryRarity.Legendary => "Lendario",
            _ => "Todos"
        };
    }

    static string GetSortLabel(BestiarySortMode mode)
    {
        return mode switch
        {
            BestiarySortMode.Alphabetical => "A-Z",
            BestiarySortMode.MostDefeated => "Mais derrotados",
            _ => "Recentes"
        };
    }

    static string GetStateLabel(BestiaryDiscoveryState state)
    {
        return state switch
        {
            BestiaryDiscoveryState.Defeated => "Derrotado",
            BestiaryDiscoveryState.Discovered => "Descoberto",
            _ => "Nao descoberto"
        };
    }

    static string GetTypeLabel(BestiaryCreatureType type)
    {
        return type switch
        {
            BestiaryCreatureType.Elemental => "Elemental",
            BestiaryCreatureType.Monster => "Monstro",
            BestiaryCreatureType.Boss => "Boss",
            _ => "Animal"
        };
    }

    static string GetElementLabel(BestiaryElement element)
    {
        return element switch
        {
            BestiaryElement.Nature => "Natureza",
            BestiaryElement.Earth => "Terra",
            BestiaryElement.Shadow => "Sombra",
            BestiaryElement.Fire => "Fogo",
            BestiaryElement.Poison => "Veneno",
            _ => "Nenhum"
        };
    }

    static string GetHostilityLabel(BestiaryHostility hostility)
    {
        return hostility switch
        {
            BestiaryHostility.Defensive => "Defensivo",
            BestiaryHostility.Hostile => "Hostil",
            _ => "Passivo"
        };
    }

    static string FormatNumber(float value)
    {
        return value % 1f == 0f ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
    }

    static string FormatTimestamp(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
