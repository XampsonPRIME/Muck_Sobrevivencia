using UnityEngine;

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
        if (player == null || player.GetComponent<PlayerProgression>() != null)
            return;

        player.gameObject.AddComponent<PlayerProgression>();
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        EnsureBaseStats();
        currentXp = Mathf.Max(0, currentXp);
        currentLevel = Mathf.Clamp(GetLevelForXp(currentXp), 1, maxLevel);
        ApplyLevelBonuses(false);
        QueueLevelRewards(false);
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
        MessageSystem.Instance?.ShowMessage($"+{amount} XP");

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
        Hotbar hotbar = GetComponent<Hotbar>() ?? FindFirstObjectByType<Hotbar>();

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

        inventory.AddItem(item.itemName, 1, item);
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
