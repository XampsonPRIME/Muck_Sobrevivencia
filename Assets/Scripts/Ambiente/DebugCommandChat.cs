using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class DebugCommandChat : MonoBehaviour
{
    const int MaxHistoryLines = 8;
    const float SubmittedHistoryVisibleSeconds = 4f;

    Canvas canvas;
    GameObject panel;
    GameObject inputRoot;
    TMP_InputField inputField;
    TextMeshProUGUI historyText;
    Coroutine hideHistoryRoutine;
    PlayerMovement player;
    Inventory inventory;
    Hotbar hotbar;
    readonly List<string> history = new List<string>();
    readonly Dictionary<string, Item> runtimeItems = new Dictionary<string, Item>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<DebugCommandChat>() != null)
            return;

        GameObject chatObject = new GameObject("DebugCommandChat");
        DontDestroyOnLoad(chatObject);
        chatObject.AddComponent<DebugCommandChat>();
    }

    void Awake()
    {
        BuildUI();
        SetOpen(false);
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!GameState.IsDebugChatOpen)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame && CanOpenChat())
                SetOpen(true);

            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetOpen(false);
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
            SubmitInput();
    }

    bool CanOpenChat()
    {
        return !GameState.IsPaused &&
               !GameState.IsInLobby &&
               !GameState.IsPlayerDead &&
               !GameState.IsInventoryOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen;
    }

    void SetOpen(bool open)
    {
        GameState.IsDebugChatOpen = open;

        if (panel != null)
            panel.SetActive(open);

        if (open)
        {
            if (hideHistoryRoutine != null)
            {
                StopCoroutine(hideHistoryRoutine);
                hideHistoryRoutine = null;
            }

            ResolveReferences();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (inputRoot != null)
                inputRoot.SetActive(true);
            inputField.text = string.Empty;
            inputField.interactable = true;
            inputField.ActivateInputField();
            EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
            AddHistory("Digite /help para ver os comandos.");
        }
        else
        {
            if (inputField != null)
            {
                inputField.DeactivateInputField();
                inputField.interactable = false;
            }

            if (inputRoot != null)
                inputRoot.SetActive(false);

            if (!GameState.IsInventoryOpen && !GameState.IsVendorOpen && !GameState.IsCraftingOpen && !GameState.IsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void SubmitInput()
    {
        string command = inputField != null ? inputField.text.Trim() : string.Empty;
        inputField.text = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            inputField.ActivateInputField();
            return;
        }

        AddHistory($"> {command}");
        ExecuteCommand(command);
        CloseAfterSubmit();
    }

    void CloseAfterSubmit()
    {
        GameState.IsDebugChatOpen = false;

        if (inputField != null)
        {
            inputField.DeactivateInputField();
            inputField.interactable = false;
        }

        if (inputRoot != null)
            inputRoot.SetActive(false);

        if (!GameState.IsInventoryOpen && !GameState.IsVendorOpen && !GameState.IsCraftingOpen && !GameState.IsPaused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (hideHistoryRoutine != null)
            StopCoroutine(hideHistoryRoutine);

        hideHistoryRoutine = StartCoroutine(HideHistoryAfterDelay());
    }

    IEnumerator HideHistoryAfterDelay()
    {
        yield return new WaitForSecondsRealtime(SubmittedHistoryVisibleSeconds);

        if (!GameState.IsDebugChatOpen && panel != null)
            panel.SetActive(false);

        hideHistoryRoutine = null;
    }

    void ExecuteCommand(string rawCommand)
    {
        if (!rawCommand.StartsWith("/", StringComparison.Ordinal))
        {
            AddHistory("Comandos precisam comecar com /.");
            return;
        }

        string body = rawCommand.Substring(1).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            AddHistory("Digite /help para ver os comandos.");
            return;
        }

        string[] parts = body.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string command = Normalize(parts[0]);

        switch (command)
        {
            case "help":
            case "ajuda":
                ShowHelp();
                break;
            case "give":
            case "item":
                HandleGive(body.Substring(parts[0].Length).Trim());
                break;
            case "heal":
            case "curar":
                HandleHeal();
                break;
            case "full":
            case "cheio":
                HandleFull();
                break;
            case "time":
            case "hora":
                HandleTime(parts);
                break;
            case "tp":
            case "teleport":
                HandleTeleport(parts);
                break;
            case "clearinventory":
            case "clearinv":
            case "limparinventario":
                HandleClearInventory();
                break;
            case "pos":
            case "position":
                HandlePosition();
                break;
            case "close":
            case "fechar":
                SetOpen(false);
                break;
            default:
                AddHistory($"Comando nao encontrado: /{parts[0]}");
                break;
        }
    }

    void ShowHelp()
    {
        AddHistory("/give Ferro Bruto 10 | /give Trigo 4 | /give Corda 1");
        AddHistory("/heal | /full | /time day | /time night | /time 14");
        AddHistory("/tp village | /clearinventory | /pos | /close");
    }

    void HandleGive(string arguments)
    {
        ResolveReferences();

        if (inventory == null)
        {
            AddHistory("Inventario do player nao encontrado.");
            return;
        }

        if (string.IsNullOrWhiteSpace(arguments))
        {
            AddHistory("Uso: /give Nome do Item quantidade");
            return;
        }

        string itemName = arguments.Trim();
        int amount = 1;
        int lastSpace = itemName.LastIndexOf(' ');
        if (lastSpace > 0 && int.TryParse(itemName.Substring(lastSpace + 1), out int parsedAmount))
        {
            amount = Mathf.Max(1, parsedAmount);
            itemName = itemName.Substring(0, lastSpace).Trim();
        }

        Item item = ResolveItem(itemName);
        if (item == null)
        {
            AddHistory($"Item nao encontrado: {itemName}");
            return;
        }

        inventory.AddItem(item.itemName, amount, item);
        hotbar?.TryAddInventoryItem(new InventoryItem(item.itemName, amount, item));
        RefreshInventoryUI();

        string message = $"+{amount} {item.itemName}";
        MessageSystem.Instance?.ShowMessage(message);
        AddHistory(message);
    }

    void HandleHeal()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        player.Heal(player.maxHealth);
        AddHistory("Vida restaurada.");
    }

    void HandleFull()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        player.currentHealth = player.maxHealth;
        player.currentStamina = player.maxStamina;
        player.currentHunger = player.maxHunger;
        player.currentThirst = player.maxThirst;
        AddHistory("Vida, stamina, fome e sede restauradas.");
    }

    void HandleTime(string[] parts)
    {
        DayNightCycle cycle = DayNightCycle.Instance ?? FindFirstObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            AddHistory("Ciclo de dia/noite nao encontrado.");
            return;
        }

        if (parts.Length < 2)
        {
            AddHistory("Uso: /time day, /time night ou /time 14");
            return;
        }

        string value = Normalize(parts[1]);
        float hour;

        if (value == "day" || value == "dia")
            hour = 8f;
        else if (value == "night" || value == "noite")
            hour = 20f;
        else if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out hour))
        {
            AddHistory("Hora invalida. Exemplo: /time 14");
            return;
        }

        hour = Mathf.Repeat(hour, 24f);
        cycle.LoadState(cycle.CurrentDay, hour / 24f);
        AddHistory($"Hora ajustada para {hour:00}:00.");
    }

    void HandleTeleport(string[] parts)
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        if (parts.Length < 2)
        {
            AddHistory("Uso: /tp village");
            return;
        }

        string destination = Normalize(parts[1]);
        if (destination != "village" && destination != "vila")
        {
            AddHistory($"Destino nao conhecido: {parts[1]}");
            return;
        }

        Transform target = FindVillageTarget();
        if (target == null)
        {
            AddHistory("Vila nao encontrada nessa cena.");
            return;
        }

        Vector3 desiredPosition = target.position + Vector3.up * 0.5f + target.right * 2f;
        if (!player.WarpToSafePosition(desiredPosition, player.transform.rotation))
        {
            AddHistory("Nao consegui encontrar um ponto seguro perto da vila.");
            return;
        }

        AddHistory("Teleportado para a vila.");
    }

    void HandleClearInventory()
    {
        ResolveReferences();

        inventory?.ClearAll();
        hotbar?.ClearAll();
        RefreshInventoryUI();
        AddHistory("Inventario limpo.");
    }

    void HandlePosition()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        Vector3 pos = player.transform.position;
        AddHistory($"Posicao: {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}");
    }

    Item ResolveItem(string requestedName)
    {
        string key = Normalize(requestedName);

        switch (key)
        {
            case "gold":
            case "ouro":
                return GoldItemRegistry.GetOrCreate();
            case "metal enferrujado":
            case "metal":
                return RustyMetalItemRegistry.GetOrCreate();
            case "ferro bruto":
            case "ferro":
                return IronItemRegistry.GetOrCreate();
            case "ferro refinado":
            case "barra de ferro":
                return RefinedIronItemRegistry.GetOrCreate();
            case "fornalha":
                return FurnaceItemRegistry.GetOrCreate();
            case "espada":
            case "espada enferrujada":
                return RustySwordItemRegistry.GetOrCreate();
            case "escudo":
            case "shield":
                return ShieldItemRegistry.GetOrCreate();
            case "trigo":
            case "wheat":
                return WheatItemRegistry.GetOrCreate();
            case "corda":
            case "rope":
                return RopeItemRegistry.GetOrCreate();
            case "machado":
            case "axe":
                return LoadResourceItem("VendorItems/Axe") ?? LoadResourceItem("Weapons/Axe") ?? GetRuntimeItem("Machado", ItemType.Tool, ToolType.Axe, 2);
            case "picareta":
            case "pickaxe":
            case "axepick":
                return LoadResourceItem("VendorItems/Axepick") ?? LoadResourceItem("Weapons/Axepick") ?? GetRuntimeItem("Picareta", ItemType.Tool, ToolType.Pickaxe, 2);
            case "graveto":
            case "gravetos":
                return GetRuntimeItem("Graveto", ItemType.Resource, ToolType.None, 0);
            case "pedra":
            case "pedras":
                return GetRuntimeItem("Pedras", ItemType.Resource, ToolType.None, 0);
            case "madeira":
            case "madeiras":
                return GetRuntimeItem("Madeira", ItemType.Resource, ToolType.None, 0);
            case "carvao":
            case "carvao vegetal":
                return GetRuntimeItem("Carvao", ItemType.Resource, ToolType.None, 0);
            default:
                return FindExistingItem(requestedName) ?? GetRuntimeItem(requestedName, ItemType.Resource, ToolType.None, 0);
        }
    }

    Item LoadResourceItem(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        return prefab != null ? prefab.GetComponent<Item>() : null;
    }

    Item FindExistingItem(string itemName)
    {
        Item[] sceneItems = FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneItems.Length; i++)
        {
            Item item = sceneItems[i];
            if (item != null && string.Equals(Normalize(item.itemName), Normalize(itemName), StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    Item GetRuntimeItem(string itemName, ItemType itemType, ToolType toolType, int toolDamage)
    {
        string key = Normalize(itemName);
        if (runtimeItems.TryGetValue(key, out Item cachedItem) && cachedItem != null)
            return cachedItem;

        GameObject itemObject = new GameObject($"{SanitizeName(itemName)}DebugItemData");
        DontDestroyOnLoad(itemObject);

        Item item = itemObject.AddComponent<Item>();
        item.itemName = itemName;
        item.itemType = itemType;
        item.toolType = toolType;
        item.toolDamage = toolDamage;
        item.icon = CreateFallbackSprite(itemName);
        runtimeItems[key] = item;
        return item;
    }

    Sprite CreateFallbackSprite(string itemName)
    {
        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color color = ColorForItem(itemName);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool inside = x >= 4 && x <= 19 && y >= 5 && y <= 18;
                texture.SetPixel(x, y, inside ? color : clear);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        sprite.name = $"{SanitizeName(itemName)}DebugSprite";
        return sprite;
    }

    Color ColorForItem(string itemName)
    {
        string key = Normalize(itemName);
        if (key.Contains("pedra") || key.Contains("ferro"))
            return new Color(0.45f, 0.47f, 0.48f, 1f);
        if (key.Contains("madeira") || key.Contains("graveto"))
            return new Color(0.58f, 0.34f, 0.16f, 1f);
        if (key.Contains("carvao"))
            return new Color(0.08f, 0.08f, 0.08f, 1f);

        return new Color(0.85f, 0.68f, 0.24f, 1f);
    }

    Transform FindVillageTarget()
    {
        VillageCraftingSetup village = FindFirstObjectByType<VillageCraftingSetup>();
        if (village != null)
        {
            Transform chest = village.transform.Find("StarterRustyMetalChest");
            if (chest != null)
                return chest;

            Transform bench = village.transform.Find("CraftingBench");
            if (bench != null)
                return bench;

            return village.transform;
        }

        VillageChest chestComponent = FindFirstObjectByType<VillageChest>();
        if (chestComponent != null)
            return chestComponent.transform;

        CraftingBench benchComponent = FindFirstObjectByType<CraftingBench>();
        return benchComponent != null ? benchComponent.transform : null;
    }

    void ResolveReferences()
    {
        if (player == null)
            player = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();

        if (player != null)
        {
            if (inventory == null)
                inventory = player.GetComponent<Inventory>();

            if (hotbar == null)
                hotbar = player.GetComponent<Hotbar>();
        }

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();
    }

    void RefreshInventoryUI()
    {
        InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (inventoryUI != null)
            inventoryUI.Refresh();
    }

    void AddHistory(string message)
    {
        if (history.Count >= MaxHistoryLines)
            history.RemoveAt(0);

        history.Add(message);

        if (historyText != null)
            historyText.text = string.Join("\n", history);
    }

    string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(normalized[i]);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(normalized[i]);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    string SanitizeName(string value)
    {
        string normalized = Normalize(value);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.Length > 0 ? builder.ToString() : "Item";
    }

    void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("DebugCommandChatCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = CreatePanel("Panel", canvasObject.transform, new Color(0.04f, 0.05f, 0.055f, 0.9f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(42f, 42f);
        panelRect.sizeDelta = new Vector2(920f, 230f);

        historyText = CreateText("History", panel.transform, 18, FontStyles.Normal, TextAlignmentOptions.BottomLeft, string.Empty);
        RectTransform historyRect = historyText.GetComponent<RectTransform>();
        historyRect.anchorMin = new Vector2(0f, 0f);
        historyRect.anchorMax = new Vector2(1f, 1f);
        historyRect.offsetMin = new Vector2(18f, 58f);
        historyRect.offsetMax = new Vector2(-18f, -18f);
        historyText.color = new Color(0.88f, 0.92f, 0.9f, 1f);

        inputRoot = CreatePanel("InputRoot", panel.transform, new Color(0.11f, 0.12f, 0.13f, 1f));
        RectTransform inputRect = inputRoot.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(16f, 14f);
        inputRect.offsetMax = new Vector2(-16f, 50f);

        inputField = inputRoot.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.textViewport = inputRoot.GetComponent<RectTransform>();

        TextMeshProUGUI text = CreateText("Text", inputRoot.transform, 20, FontStyles.Normal, TextAlignmentOptions.Left, string.Empty);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 2f);
        textRect.offsetMax = new Vector2(-14f, -2f);
        text.color = Color.white;

        TextMeshProUGUI placeholder = CreateText("Placeholder", inputRoot.transform, 20, FontStyles.Italic, TextAlignmentOptions.Left, "Digite /help");
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = textRect.offsetMin;
        placeholderRect.offsetMax = textRect.offsetMax;
        placeholder.color = new Color(0.7f, 0.72f, 0.72f, 0.8f);

        inputField.textComponent = text;
        inputField.placeholder = placeholder;
    }

    GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;

        return panelObject;
    }

    TextMeshProUGUI CreateText(string objectName, Transform parent, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, string text)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
