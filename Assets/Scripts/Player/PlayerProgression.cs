using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerProgression : MonoBehaviour
{
    public int currentLevel = 1;
    public int currentXp;
    public int maxLevel = 100;

    [Header("XP")]
    public int levelTwoXpThreshold = 100;
    public int levelThreeXpThreshold = 1000;
    public int xpPerLevelAfterThree = 1000;

    [Header("Bonus por nivel")]
    public float healthBonusPerLevel = 10f;
    public float staminaBonusPerLevel = 5f;

    PlayerMovement playerMovement;
    float baseMaxHealth;
    float baseMaxStamina;
    bool baseStatsInitialized;

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

        MessageSystem.Instance?.ShowMessage($"Subiu para o nivel {currentLevel}!");
    }

    public void LoadProgress(int totalXp)
    {
        EnsureBaseStats();
        currentXp = Mathf.Max(0, totalXp);
        currentLevel = Mathf.Clamp(GetLevelForXp(currentXp), 1, maxLevel);
        ApplyLevelBonuses(false);
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
}
