using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerProgression : MonoBehaviour
{
    public int currentLevel = 1;
    public int currentXp;
    public int maxLevel = 100;
    public int silverLoadoutLevel = 3;

    [Header("XP")]
    public int levelTwoXpThreshold = 100;
    public int levelThreeXpThreshold = 1000;
    public int xpPerLevelAfterThree = 1000;

    [Header("Bonus por nivel")]
    public float healthBonusPerLevel = 25f;
    public float staminaBonusPerLevel = 15f;

    PlayerMovement playerMovement;
    float baseMaxHealth;
    float baseMaxStamina;
    bool baseStatsInitialized;
    bool silverLoadoutGranted;
    bool pendingRewardValidation;
    bool pendingRewardMessage;
    public bool HasSilverLoadoutGranted => silverLoadoutGranted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null)
            return;

        if (player.GetComponent<PlayerProgression>() == null)
            player.gameObject.AddComponent<PlayerProgression>();

        if (player.GetComponent<StarterQuestTracker>() == null)
            player.gameObject.AddComponent<StarterQuestTracker>();
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        EnsureBaseStats();
        currentXp = Mathf.Max(0, currentXp);
        currentLevel = Mathf.Clamp(GetLevelForXp(currentXp), 1, maxLevel);
        ApplyLevelBonuses(false);
        QueueLevelRewards(false);

        if (GetComponent<StarterQuestTracker>() == null)
            gameObject.AddComponent<StarterQuestTracker>();
    }

    void Update()
    {
        if (!pendingRewardValidation)
            return;

        if (TryGrantSilverLoadout(pendingRewardMessage))
        {
            pendingRewardValidation = false;
            pendingRewardMessage = false;
        }
    }

    public void AddExperience(int amount, string sourceName = null)
    {
        if (amount <= 0)
            return;

        currentXp += amount;
        string sourceSuffix = string.IsNullOrWhiteSpace(sourceName) ? string.Empty : $" - {sourceName}";
        MessageSystem.Instance?.ShowMessage($"+{amount} XP{sourceSuffix}");

        int targetLevel = Mathf.Clamp(GetLevelForXp(currentXp), 1, maxLevel);
        while (currentLevel < targetLevel)
        {
            LevelUp();
        }
    }

    public int GetXpRequiredForNextLevel()
    {
        if (currentLevel >= maxLevel)
            return 0;

        return GetXpThresholdForLevel(currentLevel + 1);
    }

    public int GetCurrentLevelXpFloor()
    {
        return GetXpThresholdForLevel(currentLevel);
    }

    public int GetXpThresholdForLevel(int level)
    {
        level = Mathf.Max(1, level);

        if (level <= 1)
            return 0;

        if (level == 2)
            return Mathf.Max(1, levelTwoXpThreshold);

        if (level == 3)
            return Mathf.Max(levelTwoXpThreshold + 1, levelThreeXpThreshold);

        return Mathf.Max(levelThreeXpThreshold, levelThreeXpThreshold + (level - 3) * Mathf.Max(1, xpPerLevelAfterThree));
    }

    public int GetLevelForXp(int totalXp)
    {
        totalXp = Mathf.Max(0, totalXp);

        if (totalXp < levelTwoXpThreshold)
            return 1;

        if (totalXp < levelThreeXpThreshold)
            return Mathf.Min(2, maxLevel);

        int extraLevels = 1 + ((totalXp - levelThreeXpThreshold) / Mathf.Max(1, xpPerLevelAfterThree));
        return Mathf.Clamp(2 + extraLevels, 1, maxLevel);
    }

    void LevelUp()
    {
        currentLevel++;
        ApplyLevelBonuses(true);
        QueueLevelRewards(true);

        MessageSystem.Instance?.ShowMessage($"Subiu para o nivel {currentLevel}!");
    }

    public void LoadProgress(int totalXp, bool hasSilverLoadoutGranted = false)
    {
        EnsureBaseStats();
        currentXp = Mathf.Max(0, totalXp);
        currentLevel = Mathf.Clamp(GetLevelForXp(currentXp), 1, maxLevel);
        silverLoadoutGranted = hasSilverLoadoutGranted;
        ApplyLevelBonuses(false);
        QueueLevelRewards(false);
    }

    void EnsureBaseStats()
    {
        if (baseStatsInitialized || playerMovement == null)
            return;

        baseMaxHealth = playerMovement.maxHealth;
        baseMaxStamina = playerMovement.maxStamina;
        baseStatsInitialized = true;
    }

    void ApplyLevelBonuses(bool refillResources)
    {
        if (playerMovement == null)
            return;

        EnsureBaseStats();

        int bonusLevels = Mathf.Max(0, currentLevel - 1);
        playerMovement.maxHealth = baseMaxHealth + bonusLevels * healthBonusPerLevel;
        playerMovement.maxStamina = baseMaxStamina + bonusLevels * staminaBonusPerLevel;

        if (refillResources)
        {
            playerMovement.currentHealth = playerMovement.maxHealth;
            playerMovement.currentStamina = playerMovement.maxStamina;
        }
        else
        {
            playerMovement.currentHealth = Mathf.Clamp(playerMovement.currentHealth, 0f, playerMovement.maxHealth);
            playerMovement.currentStamina = Mathf.Clamp(playerMovement.currentStamina, 0f, playerMovement.maxStamina);
        }
    }

    void QueueLevelRewards(bool showMessage)
    {
        if (silverLoadoutGranted || currentLevel < silverLoadoutLevel)
            return;

        pendingRewardValidation = true;
        pendingRewardMessage |= showMessage;
    }

    bool TryGrantSilverLoadout(bool showMessage)
    {
        PlayerInteraction interaction = GetComponent<PlayerInteraction>();
        Inventory inventory = GetComponent<Inventory>();
        Hotbar hotbar = GetComponent<Hotbar>() ?? SceneObjectCache.Find<Hotbar>(gameObject.scene, true);

        if (interaction == null || inventory == null || hotbar == null)
            return false;

        Item silverAxe = ResolveSilverRewardItem(interaction.silverAxePrefab, "Axe_prata");
        Item silverPickaxe = ResolveSilverRewardItem(interaction.silverPickaxePrefab, "Axepick_prata");

        if (silverAxe == null || silverPickaxe == null)
            return false;

        bool addedAnyItem = false;
        addedAnyItem |= EnsureRewardItem(inventory, hotbar, silverAxe);
        addedAnyItem |= EnsureRewardItem(inventory, hotbar, silverPickaxe);

        silverLoadoutGranted = true;

        if (showMessage && addedAnyItem)
            MessageSystem.Instance?.ShowMessage("Nivel 3: kit prata desbloqueado!");

        return true;
    }

    Item ResolveSilverRewardItem(GameObject configuredPrefab, string prefabName)
    {
        if (configuredPrefab != null)
        {
            Item configuredItem = configuredPrefab.GetComponent<Item>();
            if (configuredItem != null)
                return configuredItem;
        }

        return LoadItemByName(prefabName);
    }

    Item LoadItemByName(string prefabName)
    {
        GameObject prefab = Resources.Load<GameObject>($"Weapons/{prefabName}");

        if (prefab == null)
        {
            Debug.LogError($"Prefab não encontrado: {prefabName}");
            return null;
        }

        Item item = prefab.GetComponent<Item>();

        if (item == null)
        {
            Debug.LogError($"Prefab {prefabName} não tem componente Item!");
            return null;
        }

        return item;
    }

    bool EnsureRewardItem(Inventory inventory, Hotbar hotbar, Item item)
    {
        if (inventory == null || hotbar == null || item == null)
            return false;

        if (OwnsItem(inventory, hotbar, item.itemName))
            return false;

        if (!inventory.AddItem(item.itemName, 1, item))
        {
            MessageSystem.Instance?.ShowMessage("Inventario cheio para receber recompensa");
            return false;
        }

        hotbar.AddItem(item.itemName, item.icon, item);
        return true;
    }

    bool OwnsItem(Inventory inventory, Hotbar hotbar, string itemName)
    {
        if (inventory != null && inventory.GetItem(itemName) != null)
            return true;

        if (hotbar != null && hotbar.slots != null)
        {
            for (int i = 0; i < hotbar.slots.Length; i++)
            {
                HotbarSlot slot = hotbar.slots[i];
                if (slot != null && !slot.IsEmpty() && slot.ItemName == itemName)
                    return true;
            }
        }

        return false;
    }
}

public class StarterQuestTracker : MonoBehaviour
{
    const int ObjectiveCount = 9;

    [Header("Objetivos iniciais")]
    [SerializeField] int currentObjectiveIndex;
    [SerializeField] bool openedBestiary;
    [SerializeField] bool completed;
    [SerializeField] bool hudPinned = true;
    [SerializeField] float refreshInterval = 0.25f;

    Inventory inventory;
    Hotbar hotbar;
    PlayerProgression progression;
    Canvas questCanvas;
    TextMeshProUGUI titleText;
    TextMeshProUGUI bodyText;
    TextMeshProUGUI rewardText;
    RectTransform progressFill;
    float refreshTimer;
    bool hudBuilt;

    public int CurrentObjectiveIndex => Mathf.Clamp(currentObjectiveIndex, 0, ObjectiveCount);
    public bool HasOpenedBestiary => openedBestiary;
    public bool IsHudPinned => hudPinned;
    public bool IsCompleted => completed || currentObjectiveIndex >= ObjectiveCount;
    public int TotalObjectiveCount => ObjectiveCount;

    void Awake()
    {
        ResolveReferences();
    }

    void Start()
    {
        EnsureHud();
        RefreshHud();
    }

    void Update()
    {
        if (GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsPlayerDead)
        {
            SetHudVisible(false);
            return;
        }

        ResolveReferences();
        EnsureHud();
        SetHudVisible(hudPinned && !GameState.IsQuestJournalOpen);

        if (GameState.IsBestiaryOpen)
            openedBestiary = true;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = Mathf.Max(0.05f, refreshInterval);

        if (!IsCompleted && IsObjectiveComplete(currentObjectiveIndex))
            CompleteCurrentObjective();

        RefreshHud();
    }

    public void LoadProgress(int objectiveIndex, bool hasOpenedBestiary, bool isCompleted, bool shouldPinHud = true)
    {
        currentObjectiveIndex = Mathf.Clamp(objectiveIndex, 0, ObjectiveCount);
        openedBestiary = hasOpenedBestiary;
        completed = isCompleted || currentObjectiveIndex >= ObjectiveCount;
        hudPinned = shouldPinHud;
        RefreshHud();
    }

    public void ResetProgress()
    {
        currentObjectiveIndex = 0;
        openedBestiary = false;
        completed = false;
        hudPinned = true;
        RefreshHud();
    }

    public void SetHudPinned(bool isPinned)
    {
        hudPinned = isPinned;
        SetHudVisible(hudPinned && !GameState.IsQuestJournalOpen && !GameState.IsInLobby && !GameState.IsWorldLoading && !GameState.IsPowerSelectionOpen && !GameState.IsPlayerDead);
        RefreshHud();
    }

    public bool IsObjectiveCompletedByIndex(int index)
    {
        if (index < 0 || index >= ObjectiveCount)
            return false;

        return IsCompleted || index < currentObjectiveIndex;
    }

    public bool IsObjectiveActiveByIndex(int index)
    {
        return !IsCompleted && index == currentObjectiveIndex;
    }

    public string GetQuestTitle(int index)
    {
        return GetObjectiveTitle(index);
    }

    public string GetQuestDescription(int index)
    {
        switch (index)
        {
            case 0:
                return "Colete madeira para iniciar sua base de recursos. Madeira sustenta crafting, ferramentas e construcoes futuras.";
            case 1:
                return "Transforme madeira em gravetos para abrir receitas simples e preparar flechas, ferramentas e combustivel.";
            case 2:
                return "Construa ou posicione uma fornalha para liberar cozimento e refinamento de materiais.";
            case 3:
                return "Cace uma galinha selvagem para obter carne e penas, dando inicio ao ciclo de caca.";
            case 4:
                return "Use a fornalha para preparar carne cozida e melhorar sua sobrevivencia durante a exploracao.";
            case 5:
                return "Fabrique um arco simples para entrar no combate a distancia e cacar com mais seguranca.";
            case 6:
                return "Fabrique flechas usando gravetos, pedras e penas. Elas serao consumidas ao disparar.";
            case 7:
                return "Derrote um javali selvagem para provar seu combate contra inimigos agressivos e obter materiais melhores.";
            case 8:
                return "Abra o bestiario para consultar criaturas descobertas, derrotas, biomas e drops.";
            default:
                return "Continue explorando o mundo.";
        }
    }

    public string GetQuestCategory(int index)
    {
        switch (index)
        {
            case 0:
                return "Coleta";
            case 1:
            case 2:
            case 5:
            case 6:
                return "Crafting";
            case 3:
            case 7:
                return "Caca";
            case 4:
                return "Cozinha";
            case 8:
                return "Exploracao";
            default:
                return "Jornada";
        }
    }

    public string GetQuestProgress(int index)
    {
        return GetObjectiveProgress(index);
    }

    public int GetQuestCurrent(int index)
    {
        return GetObjectiveCurrent(index);
    }

    public int GetQuestTarget(int index)
    {
        return GetObjectiveTarget(index);
    }

    public int GetQuestXpReward(int index)
    {
        return GetXpReward(index);
    }

    public int GetQuestGoldReward(int index)
    {
        return GetGoldReward(index);
    }

    public float GetQuestProgressNormalized(int index)
    {
        return Mathf.Clamp01(GetQuestCurrent(index) / (float)Mathf.Max(1, GetQuestTarget(index)));
    }

    void CompleteCurrentObjective()
    {
        int completedIndex = currentObjectiveIndex;
        int xpReward = GetXpReward(completedIndex);
        int goldReward = GetGoldReward(completedIndex);

        currentObjectiveIndex++;
        completed = currentObjectiveIndex >= ObjectiveCount;

        if (progression != null && xpReward > 0)
            progression.AddExperience(xpReward, "Objetivo");

        if (inventory != null && goldReward > 0)
        {
            inventory.AddGold(goldReward);
            SceneObjectCache.Find<GoldHUD>(gameObject.scene, true)?.Refresh();
        }

        string rewardMessage = goldReward > 0 ? $" +{goldReward} Gold" : string.Empty;
        MessageSystem.Instance?.ShowMessage($"Objetivo concluido: {GetObjectiveTitle(completedIndex)}{rewardMessage}");

        if (completed)
            MessageSystem.Instance?.ShowMessage("Jornada inicial concluida!");
    }

    bool IsObjectiveComplete(int index)
    {
        switch (index)
        {
            case 0:
                return CountWood() >= 5;
            case 1:
                return CountItem("Graveto") >= 4;
            case 2:
                return CountItem(FurnaceItemRegistry.ItemName) >= 1 || SceneObjectCache.Find<PlacedFurnace>(true) != null;
            case 3:
                return HasDefeated(BestiaryDatabase.WildChickenId) ||
                       CountItem(FeatherItemRegistry.ItemName) > 0 ||
                       CountItem(RawChickenMeatItemRegistry.ItemName) > 0;
            case 4:
                return CountCookedMeat() >= 1;
            case 5:
                return CountItem(SimpleBowItemRegistry.ItemName) >= 1;
            case 6:
                return CountItem(ArrowItemRegistry.ItemName) >= 2;
            case 7:
                return HasDefeated(BestiaryDatabase.WildBoarId) ||
                       CountItem(ThickLeatherItemRegistry.ItemName) > 0 ||
                       CountItem(SharpTuskItemRegistry.ItemName) > 0 ||
                       CountItem(BoarMeatItemRegistry.ItemName) > 0 ||
                       CountItem(CookedBoarMeatItemRegistry.ItemName) > 0;
            case 8:
                return openedBestiary;
            default:
                return true;
        }
    }

    string GetObjectiveTitle(int index)
    {
        switch (index)
        {
            case 0: return "Colete madeira";
            case 1: return "Fabrique gravetos";
            case 2: return "Prepare uma fornalha";
            case 3: return "Cace uma galinha";
            case 4: return "Cozinhe carne";
            case 5: return "Fabrique um arco";
            case 6: return "Fabrique flechas";
            case 7: return "Derrote um javali";
            case 8: return "Abra o bestiario";
            default: return "Jornada inicial";
        }
    }

    string GetObjectiveProgress(int index)
    {
        switch (index)
        {
            case 0:
                return $"{Mathf.Min(CountWood(), 5)}/5 Madeira";
            case 1:
                return $"{Mathf.Min(CountItem("Graveto"), 4)}/4 Gravetos";
            case 2:
                return IsObjectiveComplete(index) ? "Fornalha pronta" : "0/1 Fornalha";
            case 3:
                return IsObjectiveComplete(index) ? "Galinha abatida" : "0/1 Galinha";
            case 4:
                return $"{Mathf.Min(CountCookedMeat(), 1)}/1 Carne cozida";
            case 5:
                return IsObjectiveComplete(index) ? "Arco pronto" : "0/1 Arco simples";
            case 6:
                return $"{Mathf.Min(CountItem(ArrowItemRegistry.ItemName), 2)}/2 Flechas";
            case 7:
                return IsObjectiveComplete(index) ? "Javali derrotado" : "0/1 Javali";
            case 8:
                return openedBestiary ? "Bestiario consultado" : "0/1 Bestiario";
            default:
                return "Tudo pronto";
        }
    }

    int GetObjectiveTarget(int index)
    {
        switch (index)
        {
            case 0: return 5;
            case 1: return 4;
            case 6: return 2;
            default: return 1;
        }
    }

    int GetObjectiveCurrent(int index)
    {
        switch (index)
        {
            case 0: return CountWood();
            case 1: return CountItem("Graveto");
            case 2:
            case 3:
            case 5:
            case 7:
            case 8:
                return IsObjectiveComplete(index) ? 1 : 0;
            case 4: return CountCookedMeat();
            case 6: return CountItem(ArrowItemRegistry.ItemName);
            default: return 1;
        }
    }

    int GetXpReward(int index)
    {
        switch (index)
        {
            case 0:
            case 1:
                return 10;
            case 2:
            case 3:
            case 4:
                return 20;
            case 5:
            case 6:
                return 25;
            case 7:
                return 40;
            case 8:
                return 15;
            default:
                return 0;
        }
    }

    int GetGoldReward(int index)
    {
        switch (index)
        {
            case 2:
            case 4:
                return 3;
            case 5:
            case 7:
                return 5;
            default:
                return 0;
        }
    }

    void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (hotbar == null)
            hotbar = GetComponent<Hotbar>() ?? SceneObjectCache.Find<Hotbar>(gameObject.scene, true);

        if (progression == null)
            progression = GetComponent<PlayerProgression>();
    }

    int CountWood()
    {
        int total = 0;

        if (inventory != null && inventory.items != null)
        {
            for (int i = 0; i < inventory.items.Count; i++)
            {
                InventoryItem item = inventory.items[i];
                if (item != null && IsWoodItem(item.itemName))
                    total += Mathf.Max(0, item.quantity);
            }
        }

        if (hotbar != null && hotbar.slots != null)
        {
            for (int i = 0; i < hotbar.slots.Length; i++)
            {
                HotbarSlot slot = hotbar.slots[i];
                if (slot != null && !slot.IsEmpty() && IsWoodItem(slot.ItemName))
                    total += Mathf.Max(0, slot.GetAmount());
            }
        }

        return total;
    }

    int CountItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return 0;

        int total = inventory != null ? inventory.GetItemQuantity(itemName) : 0;

        if (hotbar != null && hotbar.slots != null)
        {
            for (int i = 0; i < hotbar.slots.Length; i++)
            {
                HotbarSlot slot = hotbar.slots[i];
                if (slot != null && !slot.IsEmpty() && string.Equals(slot.ItemName, itemName, System.StringComparison.OrdinalIgnoreCase))
                    total += Mathf.Max(0, slot.GetAmount());
            }
        }

        return total;
    }

    int CountCookedMeat()
    {
        return CountItem(CookedMeatItemRegistry.ItemName) +
               CountItem(CookedChickenMeatItemRegistry.ItemName) +
               CountItem(CookedBoarMeatItemRegistry.ItemName) +
               CountItem(CookedCowMeatItemRegistry.ItemName);
    }

    bool HasDefeated(string creatureId)
    {
        SaveBestiaryEntryData progress = BestiaryService.Instance != null ? BestiaryService.Instance.GetProgress(creatureId) : null;
        return progress != null && progress.defeatCount > 0;
    }

    bool IsWoodItem(string itemName)
    {
        return !string.IsNullOrWhiteSpace(itemName) &&
               itemName.Trim().StartsWith("Madeira", System.StringComparison.OrdinalIgnoreCase);
    }

    void EnsureHud()
    {
        if (hudBuilt)
            return;

        GameObject canvasObject = new GameObject("StarterQuestHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        questCanvas = canvasObject.GetComponent<Canvas>();
        questCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        questCanvas.sortingOrder = 32;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Outline));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(28f, -310f);
        panelRect.sizeDelta = new Vector2(460f, 96f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.08f, 0.03f, 0.72f);
        panelImage.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.86f, 0.58f, 0.16f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        titleText = CreateText("Title", panel.transform, new Vector2(24f, -12f), new Vector2(412f, 25f), 18f, FontStyles.Bold, new Color(1f, 0.82f, 0.36f, 1f));
        bodyText = CreateText("Body", panel.transform, new Vector2(24f, -37f), new Vector2(412f, 24f), 17f, FontStyles.Normal, Color.white);
        rewardText = CreateText("Reward", panel.transform, new Vector2(24f, -63f), new Vector2(412f, 18f), 13f, FontStyles.Normal, new Color(0.82f, 0.72f, 0.55f, 1f));

        GameObject progressBack = new GameObject("ProgressBack", typeof(RectTransform), typeof(Image));
        progressBack.transform.SetParent(panel.transform, false);
        RectTransform backRect = progressBack.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.offsetMin = new Vector2(24f, 11f);
        backRect.offsetMax = new Vector2(-24f, 17f);
        Image backImage = progressBack.GetComponent<Image>();
        backImage.color = new Color(0.04f, 0.025f, 0.01f, 0.75f);
        backImage.raycastTarget = false;

        GameObject progress = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        progress.transform.SetParent(progressBack.transform, false);
        progressFill = progress.GetComponent<RectTransform>();
        progressFill.anchorMin = new Vector2(0f, 0f);
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        Image fillImage = progress.GetComponent<Image>();
        fillImage.color = new Color(0.96f, 0.62f, 0.18f, 0.95f);
        fillImage.raycastTarget = false;

        hudBuilt = true;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    void RefreshHud()
    {
        if (!hudBuilt || titleText == null || bodyText == null || rewardText == null)
            return;

        if (IsCompleted)
        {
            titleText.text = "Jornada inicial concluida";
            bodyText.text = "Explore, cace, cozinhe e evolua seus equipamentos.";
            rewardText.text = "Novos desafios podem surgir pelo mundo.";
            SetProgress(1f);
            return;
        }

        int objective = Mathf.Clamp(currentObjectiveIndex, 0, ObjectiveCount - 1);
        int current = Mathf.Clamp(GetObjectiveCurrent(objective), 0, GetObjectiveTarget(objective));
        int target = Mathf.Max(1, GetObjectiveTarget(objective));

        titleText.text = $"Objetivo {objective + 1}/{ObjectiveCount}: {GetObjectiveTitle(objective)}";
        bodyText.text = GetObjectiveProgress(objective);
        rewardText.text = $"Recompensa: +{GetXpReward(objective)} XP" + (GetGoldReward(objective) > 0 ? $"  +{GetGoldReward(objective)} Gold" : string.Empty);
        SetProgress(current / (float)target);
    }

    void SetProgress(float normalized)
    {
        if (progressFill == null)
            return;

        normalized = Mathf.Clamp01(normalized);
        RectTransform parent = progressFill.parent as RectTransform;
        float width = parent != null ? Mathf.Max(0f, parent.rect.width) : 0f;
        progressFill.sizeDelta = new Vector2(width * normalized, 0f);
    }

    void SetHudVisible(bool visible)
    {
        if (questCanvas != null && questCanvas.gameObject.activeSelf != visible)
            questCanvas.gameObject.SetActive(visible);
    }
}

public class QuestJournalUI : MonoBehaviour
{
    const int CanvasSortingOrder = 136;

    public static QuestJournalUI Instance { get; private set; }

    readonly Color overlayColor = new Color(0.03f, 0.025f, 0.02f, 0.68f);
    readonly Color bookColor = new Color(0.52f, 0.34f, 0.16f, 0.98f);
    readonly Color pageColor = new Color(0.84f, 0.72f, 0.52f, 1f);
    readonly Color panelColor = new Color(0.2f, 0.11f, 0.04f, 0.16f);
    readonly Color inkColor = new Color(0.13f, 0.08f, 0.04f, 1f);
    readonly Color mutedInkColor = new Color(0.36f, 0.25f, 0.15f, 1f);
    readonly Color buttonColor = new Color(0.46f, 0.28f, 0.12f, 0.98f);
    readonly Color selectedButtonColor = new Color(0.68f, 0.42f, 0.15f, 1f);

    InputAction toggleAction;
    InputAction closeAction;
    Canvas canvas;
    GameObject overlayObject;
    RectTransform listContent;
    RectTransform detailRoot;
    TextMeshProUGUI statsText;
    TextMeshProUGUI activeTabText;
    TextMeshProUGUI completedTabText;
    Button pinButton;
    TextMeshProUGUI pinButtonText;
    PlayerMovement currentPlayerMovement;
    PlayerInteraction currentPlayerInteraction;
    StarterQuestTracker tracker;
    bool isOpen;
    bool showingCompleted;
    int selectedObjectiveIndex = -1;
    float refreshTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<QuestJournalUI>() != null)
            return;

        GameObject uiObject = new GameObject("QuestJournalUI");
        uiObject.AddComponent<QuestJournalUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        toggleAction = new InputAction("ToggleQuestJournal", binding: "<Keyboard>/j");
        closeAction = new InputAction("CloseQuestJournal", binding: "<Keyboard>/escape");
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

        GameState.IsQuestJournalOpen = false;
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
        if (isOpen && (GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsPlayerDead || GameState.IsPaused))
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

        if (!isOpen)
            return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.25f;
            Refresh();
        }
    }

    public void Open()
    {
        if (!CanOpen())
            return;

        ResolvePlayer();
        UIEventSystemUtility.EnsureSingleEventSystem();
        BuildUi();

        GameState.IsQuestJournalOpen = true;
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
        GameState.IsQuestJournalOpen = false;
        GameState.LastUiCloseFrame = Time.frameCount;
        SetVisible(false);

        if (currentPlayerMovement != null && !GameState.IsPlayerDead && !GameState.IsPaused && !GameState.IsInLobby && !GameState.IsWorldLoading)
            currentPlayerMovement.enabled = true;

        if (currentPlayerInteraction != null && !GameState.IsPlayerDead && !GameState.IsPaused && !GameState.IsInLobby && !GameState.IsWorldLoading)
            currentPlayerInteraction.enabled = true;

        if (!GameState.IsPaused &&
            !GameState.IsInventoryOpen &&
            !GameState.IsVendorOpen &&
            !GameState.IsCraftingOpen &&
            !GameState.IsBestiaryOpen &&
            !GameState.IsInLobby &&
            !GameState.IsWorldLoading)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    bool CanOpen()
    {
        return !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPowerSelectionOpen &&
               LanMultiplayerManager.FindGameplayPlayer() != null &&
               !GameState.IsPlayerDead &&
               !GameState.IsPaused &&
               !GameState.IsInventoryOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen &&
               !GameState.IsDebugChatOpen &&
               !GameState.IsBestiaryOpen;
    }

    void Refresh()
    {
        ResolvePlayer();

        if (tracker == null)
            return;

        RefreshStats();
        RefreshTabs();
        RefreshList();
        RefreshDetails();
    }

    void RefreshStats()
    {
        if (statsText == null || tracker == null)
            return;

        int completedCount = CountCompletedObjectives();
        int activeCount = tracker.IsCompleted ? 0 : 1;
        int completion = Mathf.RoundToInt(completedCount / (float)Mathf.Max(1, tracker.TotalObjectiveCount) * 100f);
        statsText.text = $"Ativas {activeCount}    Concluidas {completedCount}/{tracker.TotalObjectiveCount}    Progresso {completion}%";
    }

    void RefreshTabs()
    {
        if (activeTabText != null)
            activeTabText.text = showingCompleted ? "Ativas" : "> Ativas";

        if (completedTabText != null)
            completedTabText.text = showingCompleted ? "> Concluidas" : "Concluidas";
    }

    void RefreshList()
    {
        if (listContent == null || tracker == null)
            return;

        ClearChildren(listContent);

        List<int> indices = BuildVisibleObjectiveList();
        if ((selectedObjectiveIndex < 0 || !indices.Contains(selectedObjectiveIndex)) && indices.Count > 0)
            selectedObjectiveIndex = indices[0];

        for (int i = 0; i < indices.Count; i++)
            CreateQuestRow(indices[i]);

        if (indices.Count == 0)
        {
            string message = showingCompleted ? "Nenhuma missao concluida ainda." : "Nenhuma missao ativa.";
            TextMeshProUGUI empty = CreateText("EmptyMissions", listContent, message, 18f, mutedInkColor, TextAlignmentOptions.Center);
            LayoutElement layout = empty.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 90f;
            selectedObjectiveIndex = -1;
        }
    }

    void RefreshDetails()
    {
        if (detailRoot == null || tracker == null)
            return;

        ClearChildren(detailRoot);
        pinButton = null;
        pinButtonText = null;

        if (selectedObjectiveIndex < 0)
        {
            CreateText("NoMission", detailRoot, "Selecione uma missao.", 22f, inkColor, TextAlignmentOptions.Center);
            return;
        }

        int index = selectedObjectiveIndex;
        bool isActive = tracker.IsObjectiveActiveByIndex(index);
        bool isCompleted = tracker.IsObjectiveCompletedByIndex(index);

        CreateText("MissionTitle", detailRoot, tracker.GetQuestTitle(index), 30f, inkColor, TextAlignmentOptions.Left);
        CreateText("MissionState", detailRoot, $"{GetStateLabel(index)}  |  {tracker.GetQuestCategory(index)}", 16f, mutedInkColor, TextAlignmentOptions.Left);

        AddSectionTitle("Descricao");
        CreateText("MissionDescription", detailRoot, tracker.GetQuestDescription(index), 17f, inkColor, TextAlignmentOptions.Left);

        AddSectionTitle("Progresso");
        CreateProgressBlock(index);

        AddSectionTitle("Recompensas");
        CreateText("Rewards", detailRoot, FormatReward(index), 17f, inkColor, TextAlignmentOptions.Left);

        if (isActive)
        {
            pinButton = CreateTextButton("PinButton", detailRoot, tracker.IsHudPinned ? "Desfixar do HUD" : "Fixar no HUD", 16f, tracker.IsHudPinned ? selectedButtonColor : buttonColor);
            pinButtonText = pinButton.GetComponentInChildren<TextMeshProUGUI>();
            pinButton.onClick.AddListener(TogglePinnedObjective);
            LayoutElement pinLayout = pinButton.gameObject.AddComponent<LayoutElement>();
            pinLayout.preferredHeight = 42f;
        }
        else if (isCompleted)
        {
            CreateText("CompletedHint", detailRoot, "Missao concluida e recompensa recebida.", 16f, mutedInkColor, TextAlignmentOptions.Left);
        }
    }

    void CreateProgressBlock(int index)
    {
        GameObject root = CreateUiObject("ProgressBlock", detailRoot);
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        CreateText("ProgressText", root.transform, tracker.GetQuestProgress(index), 17f, inkColor, TextAlignmentOptions.Left);

        GameObject back = CreateUiObject("ProgressBack", root.transform);
        Image backImage = back.AddComponent<Image>();
        backImage.color = new Color(0.15f, 0.08f, 0.03f, 0.65f);
        LayoutElement backLayout = back.AddComponent<LayoutElement>();
        backLayout.preferredHeight = 12f;

        GameObject fill = CreateUiObject("ProgressFill", back.transform);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.88f, 0.52f, 0.16f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(tracker.GetQuestProgressNormalized(index), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    void TogglePinnedObjective()
    {
        if (tracker == null)
            return;

        tracker.SetHudPinned(!tracker.IsHudPinned);

        if (pinButton != null)
            pinButton.GetComponent<Image>().color = tracker.IsHudPinned ? selectedButtonColor : buttonColor;

        if (pinButtonText != null)
            pinButtonText.text = tracker.IsHudPinned ? "Desfixar do HUD" : "Fixar no HUD";
    }

    List<int> BuildVisibleObjectiveList()
    {
        List<int> indices = new List<int>();

        if (tracker == null)
            return indices;

        for (int i = 0; i < tracker.TotalObjectiveCount; i++)
        {
            if (showingCompleted)
            {
                if (tracker.IsObjectiveCompletedByIndex(i))
                    indices.Add(i);
            }
            else if (tracker.IsObjectiveActiveByIndex(i))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    void CreateQuestRow(int index)
    {
        bool selected = index == selectedObjectiveIndex;
        Button row = CreateTextButton($"Quest_{index}", listContent, string.Empty, 15f, selected ? selectedButtonColor : buttonColor);
        row.onClick.AddListener(() =>
        {
            selectedObjectiveIndex = index;
            Refresh();
        });

        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 74f;

        TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Left;
        label.margin = new Vector4(12f, 7f, 12f, 5f);
        label.text = $"{tracker.GetQuestTitle(index)}\n<size=78%>{GetStateLabel(index)} - {tracker.GetQuestProgress(index)}</size>";
    }

    int CountCompletedObjectives()
    {
        if (tracker == null)
            return 0;

        int count = 0;
        for (int i = 0; i < tracker.TotalObjectiveCount; i++)
        {
            if (tracker.IsObjectiveCompletedByIndex(i))
                count++;
        }

        return count;
    }

    string GetStateLabel(int index)
    {
        if (tracker == null)
            return "Indisponivel";

        if (tracker.IsObjectiveCompletedByIndex(index))
            return "Concluida";

        if (tracker.IsObjectiveActiveByIndex(index))
            return "Ativa";

        return "Bloqueada";
    }

    string FormatReward(int index)
    {
        if (tracker == null)
            return "-";

        int xp = tracker.GetQuestXpReward(index);
        int gold = tracker.GetQuestGoldReward(index);
        return gold > 0 ? $"+{xp} XP  |  +{gold} Gold" : $"+{xp} XP";
    }

    void ResolvePlayer()
    {
        if (currentPlayerMovement == null || !currentPlayerMovement.gameObject.activeInHierarchy)
            currentPlayerMovement = LanMultiplayerManager.FindGameplayPlayer();

        if (currentPlayerInteraction == null && currentPlayerMovement != null)
            currentPlayerInteraction = currentPlayerMovement.GetComponent<PlayerInteraction>();

        if (tracker == null && currentPlayerMovement != null)
            tracker = currentPlayerMovement.GetComponent<StarterQuestTracker>() ?? currentPlayerMovement.gameObject.AddComponent<StarterQuestTracker>();
    }

    void BuildUi()
    {
        if (overlayObject != null)
            return;

        GameObject canvasObject = new GameObject("QuestJournalCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        DisplaySettingsManager.ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>());

        overlayObject = CreateUiObject("QuestJournalOverlay", canvasObject.transform);
        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = overlayColor;
        SetStretch(overlayObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject book = CreateUiObject("MissionBook", overlayObject.transform);
        Image bookImage = book.AddComponent<Image>();
        bookImage.color = bookColor;
        RectTransform bookRect = book.GetComponent<RectTransform>();
        bookRect.anchorMin = new Vector2(0.5f, 0.5f);
        bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.sizeDelta = new Vector2(1040f, 620f);
        bookRect.anchoredPosition = Vector2.zero;

        GameObject leftPage = CreatePage("LeftPage", book.transform, new Vector2(0.03f, 0.06f), new Vector2(0.49f, 0.94f));
        GameObject rightPage = CreatePage("RightPage", book.transform, new Vector2(0.51f, 0.06f), new Vector2(0.97f, 0.94f));

        GameObject spine = CreateUiObject("BookSpine", book.transform);
        Image spineImage = spine.AddComponent<Image>();
        spineImage.color = new Color(0.25f, 0.13f, 0.06f, 0.76f);
        RectTransform spineRect = spine.GetComponent<RectTransform>();
        spineRect.anchorMin = new Vector2(0.493f, 0.04f);
        spineRect.anchorMax = new Vector2(0.507f, 0.96f);
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
        TextMeshProUGUI title = CreateText("JournalTitle", parent, "Diario de Missoes", 32f, inkColor, TextAlignmentOptions.Center);
        SetTop(title.rectTransform, 18f, 18f, 440f, 42f);

        statsText = CreateText("JournalStats", parent, string.Empty, 15f, mutedInkColor, TextAlignmentOptions.Center);
        SetTop(statsText.rectTransform, 56f, 20f, 434f, 28f);

        GameObject tabs = CreateUiObject("Tabs", parent);
        RectTransform tabsRect = tabs.GetComponent<RectTransform>();
        SetTop(tabsRect, 94f, 22f, 430f, 38f);
        HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 10f;
        tabLayout.childControlHeight = true;
        tabLayout.childControlWidth = true;

        Button activeButton = CreateTextButton("ActiveTab", tabs.transform, "> Ativas", 16f, selectedButtonColor);
        activeTabText = activeButton.GetComponentInChildren<TextMeshProUGUI>();
        activeButton.onClick.AddListener(() =>
        {
            showingCompleted = false;
            selectedObjectiveIndex = -1;
            Refresh();
        });

        Button completedButton = CreateTextButton("CompletedTab", tabs.transform, "Concluidas", 16f, buttonColor);
        completedTabText = completedButton.GetComponentInChildren<TextMeshProUGUI>();
        completedButton.onClick.AddListener(() =>
        {
            showingCompleted = true;
            selectedObjectiveIndex = -1;
            Refresh();
        });

        GameObject scrollObject = CreateUiObject("QuestScroll", parent);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        SetTop(scrollRect, 146f, 24f, 430f, 390f);
        Image scrollBack = scrollObject.AddComponent<Image>();
        scrollBack.color = panelColor;
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0.18f, 0.1f, 0.04f, 0.04f);
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
        listLayout.spacing = 8f;
        listLayout.padding = new RectOffset(8, 12, 8, 8);
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = listContent;
    }

    void BuildRightPage(Transform parent)
    {
        TextMeshProUGUI title = CreateText("DetailsTitle", parent, "Registro da Missao", 28f, inkColor, TextAlignmentOptions.Center);
        SetTop(title.rectTransform, 20f, 20f, 440f, 36f);

        TextMeshProUGUI hint = CreateText("CloseHint", parent, "J ou Esc para fechar", 14f, mutedInkColor, TextAlignmentOptions.Center);
        SetTop(hint.rectTransform, 56f, 20f, 440f, 24f);

        GameObject detailScroll = CreateUiObject("DetailScroll", parent);
        RectTransform detailRect = detailScroll.GetComponent<RectTransform>();
        SetTop(detailRect, 96f, 24f, 430f, 438f);
        Image detailBack = detailScroll.AddComponent<Image>();
        detailBack.color = panelColor;
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
        detailLayout.spacing = 10f;
        detailLayout.padding = new RectOffset(14, 14, 12, 12);
        detailLayout.childControlWidth = true;
        detailLayout.childControlHeight = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = detailRoot;
    }

    void AddSectionTitle(string label)
    {
        TextMeshProUGUI text = CreateText($"Section_{label}", detailRoot, label, 18f, mutedInkColor, TextAlignmentOptions.Left);
        text.fontStyle = FontStyles.Bold;
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

    TextMeshProUGUI CreateText(string name, Transform parent, string textValue, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = textValue;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    void SetTop(RectTransform rect, float top, float left, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    void SetVisible(bool visible)
    {
        isOpen = visible;

        if (overlayObject != null)
            overlayObject.SetActive(visible);
    }

}
