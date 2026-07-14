using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SaveInventoryItemData
{
    public string itemName;
    public string prefabName;
    public int quantity;
    public bool isBottle;
    public bool bottleIsFilled;
}

[Serializable]
public class SaveHotbarSlotData
{
    public string itemName;
    public string prefabName;
    public int quantity;
    public bool isBottle;
    public bool bottleIsFilled;
}

[Serializable]
public class SaveEquipmentSlotData
{
    public EquipmentSlotType slot;
    public string itemName;
    public string prefabName;
    public int quantity;
    public bool isBottle;
    public bool bottleIsFilled;
}

[Serializable]
public class SaveGameData
{
    public string sceneName;
    public int worldSeed;
    public float playerPosX;
    public float playerPosY;
    public float playerPosZ;
    public float playerRotY;
    public bool thirdPerson;
    public float health;
    public float stamina;
    public float hunger;
    public float thirst;
    public bool hasUnlockedAreaMagic;
    public bool hasSilverLoadoutGranted;
    public int currentXp;
    public int starterQuestIndex;
    public bool starterQuestBestiaryOpened;
    public bool starterQuestCompleted;
    public bool starterQuestHudPinned = true;
    public bool starterQuestHudPinPreferenceSaved;
    public int currentDay;
    public float normalizedTimeOfDay;
    public int selectedHotbarIndex;
    public List<SaveInventoryItemData> inventory = new List<SaveInventoryItemData>();
    public List<SaveHotbarSlotData> hotbar = new List<SaveHotbarSlotData>();
    public List<SaveEquipmentSlotData> equipment = new List<SaveEquipmentSlotData>();
    public List<SaveBestiaryEntryData> bestiary = new List<SaveBestiaryEntryData>();
    public List<string> activeAncestralPowers = new List<string>();
    public string bearerPowerId;
    public bool bearerPowerSelectionCompleted;
}

[Serializable]
public class MultiplayerSessionSaveData
{
    public string sceneName;
    public string sceneSetId;
    public string activeSceneName;
    public List<string> sceneNames = new List<string>();
    public int worldSeed;
    public float playerPosX;
    public float playerPosY;
    public float playerPosZ;
    public float playerRotY;
    public bool thirdPerson;
    public float health;
    public float stamina;
    public float hunger;
    public float thirst;
    public bool hasUnlockedAreaMagic;
    public bool hasSilverLoadoutGranted;
    public int currentXp;
    public int starterQuestIndex;
    public bool starterQuestBestiaryOpened;
    public bool starterQuestCompleted;
    public bool starterQuestHudPinned = true;
    public bool starterQuestHudPinPreferenceSaved;
    public int currentDay;
    public float normalizedTimeOfDay;
    public int selectedHotbarIndex;
    public List<SaveInventoryItemData> inventory = new List<SaveInventoryItemData>();
    public List<SaveHotbarSlotData> hotbar = new List<SaveHotbarSlotData>();
    public List<SaveEquipmentSlotData> equipment = new List<SaveEquipmentSlotData>();
    public List<SaveBestiaryEntryData> bestiary = new List<SaveBestiaryEntryData>();
    public List<string> activeAncestralPowers = new List<string>();
    public string bearerPowerId;
    public bool bearerPowerSelectionCompleted;
    public List<string> bearerPowerOwnerPowerIds = new List<string>();
    public List<string> bearerPowerOwnerPlayerIds = new List<string>();
    public List<LanSavedEntityState> worldEntities = new List<LanSavedEntityState>();
}

public class SaveGameManager : MonoBehaviour
{
    const string LegacyProductDirectoryName = "Muck_Survivo";
    const float EarlyStartPortalRepairRadius = 80f;
    const float EarlyStartVillageRepairRadius = 340f;
    const float EarlyStartBossRepairRadius = 280f;
    const float EarlyStartLegacyPlayerRepairRadius = 240f;
    const int EarlyStartMaxRepairXp = 75;
    const int EarlyStartMaxRepairQuestIndex = 1;
    static readonly Vector3 PowerCaveReturnFallbackPosition = new Vector3(-181.58424f, 0f, 113.626854f);

    public static SaveGameManager Instance { get; private set; }

    public float autoSaveInterval = 20f;

    PlayerMovement playerMovement;
    PlayerInteraction playerInteraction;
    Inventory inventory;
    Hotbar hotbar;
    PlayerEquipment playerEquipment;
    DayNightCycle dayNightCycle;
    PlayerProgression progression;
    PlayerMagic playerMagic;
    StarterQuestTracker starterQuest;
    AncestralPowerService ancestralPowers;
    BearerPowerService bearerPower;
    InventoryUI inventoryUI;

    float autoSaveTimer;
    MultiplayerSessionSaveData pendingMultiplayerSessionLoad;
    MultiplayerSessionSaveData pendingClientSessionLoad;
    string pendingClientSessionSaveKey;
    string lastAppliedClientSessionSaveKey;

    static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    static string MultiplayerSessionSavePath => Path.Combine(Application.persistentDataPath, "multiplayer_session.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void MigrateLegacySaves()
    {
        try
        {
            string currentDirectory = Application.persistentDataPath;
            DirectoryInfo companyDirectory = Directory.GetParent(currentDirectory);
            if (companyDirectory == null)
                return;

            string legacyDirectory = Path.Combine(companyDirectory.FullName, LegacyProductDirectoryName);
            if (!Directory.Exists(legacyDirectory) ||
                string.Equals(legacyDirectory, currentDirectory, StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(currentDirectory);

            int migratedFileCount = 0;
            string[] legacySaveFiles = Directory.GetFiles(legacyDirectory, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < legacySaveFiles.Length; i++)
            {
                string targetPath = Path.Combine(currentDirectory, Path.GetFileName(legacySaveFiles[i]));
                if (File.Exists(targetPath))
                    continue;

                File.Copy(legacySaveFiles[i], targetPath, false);
                migratedFileCount++;
            }

            if (migratedFileCount > 0)
                Debug.Log($"Migrados {migratedFileCount} save(s) de {LegacyProductDirectoryName} para Elarion.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Nao foi possivel migrar os saves antigos para Elarion: {exception.Message}");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<SaveGameManager>() != null)
            return;

        if (LanMultiplayerManager.FindGameplayPlayer() == null)
            return;

        GameObject managerObject = new GameObject("SaveGameManager");
        managerObject.AddComponent<SaveGameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        ResolveReferences();
    }

    void Update()
    {
        ResolveReferences();

        if (pendingMultiplayerSessionLoad != null)
            TryApplyPendingMultiplayerSessionLoad();

        if (pendingClientSessionLoad != null)
            TryApplyPendingClientSessionLoad();

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager == null || manager.Mode != LanMultiplayerManager.SessionMode.Client || !manager.IsSessionReady)
        {
            pendingClientSessionLoad = null;
            pendingClientSessionSaveKey = null;
            lastAppliedClientSessionSaveKey = null;
        }

        if (manager != null && manager.IsMultiplayerActive)
        {
            if (manager.Mode == LanMultiplayerManager.SessionMode.Host && manager.IsSessionReady)
            {
                if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || playerMovement == null)
                    return;

                autoSaveTimer += Time.deltaTime;
                if (autoSaveTimer < autoSaveInterval)
                    return;

                autoSaveTimer = 0f;
                SaveMultiplayerSession();
                return;
            }

            if (manager.Mode == LanMultiplayerManager.SessionMode.Client && manager.IsSessionReady)
            {
                QueueClientSessionLoadIfAvailable(manager);

                if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || playerMovement == null)
                    return;

                autoSaveTimer += Time.deltaTime;
                if (autoSaveTimer < autoSaveInterval)
                    return;

                autoSaveTimer = 0f;
                SaveClientSession();
                return;
            }

            return;
        }

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || playerMovement == null)
            return;

        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer < autoSaveInterval)
            return;

        autoSaveTimer = 0f;
        SaveGame();
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public bool HasMultiplayerSessionSave()
    {
        return File.Exists(MultiplayerSessionSavePath);
    }

    public bool HasClientSessionSave(string address, int port)
    {
        return File.Exists(GetClientSessionSavePath(address, port));
    }

    public void StartNewGame()
    {
        ResolveReferences();
        DeleteSave();
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
        GameState.IsVendorOpen = false;
        GameState.IsCraftingOpen = false;
        GameState.IsDebugChatOpen = false;
        GameState.IsBestiaryOpen = false;
        GameState.IsQuestJournalOpen = false;
        GameState.IsMapOpen = false;
        GameState.IsPowerSelectionOpen = false;
        GameState.IsDemoGuideOpen = false;
        GameState.IsPaused = false;
        ResetPlayerForNewGame();
        ExitLobby();
    }

    void ResetPlayerForNewGame()
    {
        if (playerMovement == null)
            return;

        progression ??= playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
        playerMagic ??= playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
        starterQuest ??= playerMovement.GetComponent<StarterQuestTracker>() ?? playerMovement.gameObject.AddComponent<StarterQuestTracker>();
        ancestralPowers ??= playerMovement.GetComponent<AncestralPowerService>() ?? playerMovement.gameObject.AddComponent<AncestralPowerService>();
        bearerPower ??= playerMovement.GetComponent<BearerPowerService>() ?? playerMovement.gameObject.AddComponent<BearerPowerService>();

        inventory?.ClearAll();
        hotbar?.ClearAll();
        playerEquipment?.ClearAll();
        progression.LoadProgress(0, false);
        playerMagic.LoadState(false);
        starterQuest.ResetProgress();
        BestiaryService.Instance?.ResetProgress();
        ancestralPowers.ClearPowers(false);
        BearerShadowClone.DestroyAllForOwner(playerMovement);
        bearerPower.ResetForNewAdventure();
        DemoWorldProgression.ApplyLandmarkLayout(gameObject.scene);
        playerMovement.PrepareFreshStartForWorldGeneration();
        playerInteraction?.ResetStarterLoadout();

        int selectedIndex = hotbar != null && hotbar.slots != null && hotbar.slots.Length > 0 ? 0 : -1;
        if (selectedIndex >= 0)
        {
            hotbar.SetSelectedIndex(selectedIndex);
            playerInteraction?.SelectSlotIndex(selectedIndex);
        }

        if (dayNightCycle != null)
            dayNightCycle.LoadState(1, Mathf.Repeat(dayNightCycle.startHour / 24f, 1f));

        inventoryUI?.Refresh();
        SceneObjectCache.Find<GoldHUD>(gameObject.scene, true)?.Refresh();
        SceneObjectCache.Find<LevelHUD>(gameObject.scene, true)?.Refresh();
        autoSaveTimer = 0f;
    }

    public bool ContinueFromSave()
    {
        bool loaded = LoadGame();

        if (loaded)
        {
            ExitLobby();
            MessageSystem.Instance?.ShowMessage("Jogo carregado");
        }
        else
        {
            MessageSystem.Instance?.ShowMessage("Nenhum save valido encontrado");
        }

        return loaded;
    }

    public bool ContinueMultiplayerSession(int port)
    {
        ResolveReferences();

        if (LanMultiplayerManager.Instance == null || !HasMultiplayerSessionSave())
            return false;

        MultiplayerSessionSaveData data = JsonUtility.FromJson<MultiplayerSessionSaveData>(File.ReadAllText(MultiplayerSessionSavePath));
        if (data == null)
            return false;

        MultiplayerSceneSetState startupSceneSet = MultiplayerSceneSetCatalog.GetDefaultStartupState();
        if (startupSceneSet == null)
            return false;

        if (!LanMultiplayerManager.Instance.StartHost(port, data.worldSeed))
            return false;

        pendingMultiplayerSessionLoad = data;
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
        GameState.IsBestiaryOpen = false;
        GameState.IsQuestJournalOpen = false;
        GameState.IsPaused = false;
        ExitLobby();

        MultiplayerSceneSetCatalog.ApplyToRuntime(startupSceneSet);

        TryApplyPendingMultiplayerSessionLoad();
        return true;
    }

    public bool SaveGame(bool showMessage = false)
    {
        ResolveReferences();

        if (LanMultiplayerManager.Instance != null && LanMultiplayerManager.Instance.IsMultiplayerActive)
            return false;

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || GameState.IsPowerSelectionOpen)
            return false;

        if (playerMovement == null || inventory == null || hotbar == null || progression == null)
            return false;

        SaveGameData data = new SaveGameData
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            worldSeed = LanMultiplayerManager.Instance != null
                ? LanMultiplayerManager.Instance.WorldSeed
                : DemoWorldProgression.ResolveWorldSeed(),
            playerPosX = playerMovement.transform.position.x,
            playerPosY = playerMovement.transform.position.y,
            playerPosZ = playerMovement.transform.position.z,
            playerRotY = playerMovement.transform.eulerAngles.y,
            thirdPerson = playerMovement.thirdPerson,
            health = playerMovement.currentHealth,
            stamina = playerMovement.currentStamina,
            hunger = playerMovement.currentHunger,
            thirst = playerMovement.currentThirst,
            hasUnlockedAreaMagic = playerMagic != null && playerMagic.hasUnlockedAreaMagic,
            hasSilverLoadoutGranted = progression.HasSilverLoadoutGranted,
            currentXp = progression.currentXp,
            starterQuestIndex = starterQuest != null ? starterQuest.CurrentObjectiveIndex : 0,
            starterQuestBestiaryOpened = starterQuest != null && starterQuest.HasOpenedBestiary,
            starterQuestCompleted = starterQuest != null && starterQuest.IsCompleted,
            starterQuestHudPinned = starterQuest == null || starterQuest.IsHudPinned,
            starterQuestHudPinPreferenceSaved = true,
            currentDay = dayNightCycle != null ? dayNightCycle.CurrentDay : 1,
            normalizedTimeOfDay = dayNightCycle != null ? dayNightCycle.CurrentNormalizedTime : 0f,
            selectedHotbarIndex = hotbar.SelectedIndex,
            equipment = playerEquipment != null ? playerEquipment.CaptureEquipment() : new List<SaveEquipmentSlotData>(),
            bestiary = BestiaryService.Instance != null ? BestiaryService.Instance.CaptureProgress() : new List<SaveBestiaryEntryData>(),
            activeAncestralPowers = ancestralPowers != null ? ancestralPowers.CapturePowerIds() : new List<string>(),
            bearerPowerId = bearerPower != null ? bearerPower.CurrentPowerId : string.Empty,
            bearerPowerSelectionCompleted = bearerPower != null && bearerPower.HasCompletedSelection
        };

        foreach (InventoryItem item in inventory.items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemName) || item.quantity <= 0)
                continue;

            data.inventory.Add(new SaveInventoryItemData
            {
                itemName = item.itemName,
                prefabName = item.prefabName,
                quantity = item.quantity,
                isBottle = item.isBottle,
                bottleIsFilled = item.bottleIsFilled
            });
        }

        if (hotbar.slots != null)
        {
            foreach (HotbarSlot slot in hotbar.slots)
            {
                if (slot == null || slot.IsEmpty() || slot.GetItemData() == null || slot.GetAmount() <= 0)
                {
                    data.hotbar.Add(new SaveHotbarSlotData());
                    continue;
                }

                data.hotbar.Add(new SaveHotbarSlotData
                {
                    itemName = slot.ItemName,
                    prefabName = slot.prefabName,
                    quantity = slot.GetAmount(),
                    isBottle = slot.isBottle,
                    bottleIsFilled = slot.bottleIsFilled
                });
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);

        if (showMessage)
            MessageSystem.Instance?.ShowMessage("Jogo salvo");

        return true;
    }

    public bool SaveMultiplayerSession(bool showMessage = false)
    {
        ResolveReferences();

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager == null || manager.Mode != LanMultiplayerManager.SessionMode.Host || !manager.IsSessionReady)
            return false;

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || GameState.IsPowerSelectionOpen)
            return false;

        if (playerMovement == null || inventory == null || hotbar == null || progression == null)
            return false;

        MultiplayerSceneSetState currentSceneSet = manager.CaptureCurrentSceneSet();

        MultiplayerSessionSaveData data = new MultiplayerSessionSaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            sceneSetId = currentSceneSet?.sceneSetId,
            activeSceneName = currentSceneSet?.activeSceneName,
            worldSeed = manager.WorldSeed,
            playerPosX = playerMovement.transform.position.x,
            playerPosY = playerMovement.transform.position.y,
            playerPosZ = playerMovement.transform.position.z,
            playerRotY = playerMovement.transform.eulerAngles.y,
            thirdPerson = playerMovement.thirdPerson,
            health = playerMovement.currentHealth,
            stamina = playerMovement.currentStamina,
            hunger = playerMovement.currentHunger,
            thirst = playerMovement.currentThirst,
            hasUnlockedAreaMagic = playerMagic != null && playerMagic.hasUnlockedAreaMagic,
            hasSilverLoadoutGranted = progression.HasSilverLoadoutGranted,
            currentXp = progression.currentXp,
            starterQuestIndex = starterQuest != null ? starterQuest.CurrentObjectiveIndex : 0,
            starterQuestBestiaryOpened = starterQuest != null && starterQuest.HasOpenedBestiary,
            starterQuestCompleted = starterQuest != null && starterQuest.IsCompleted,
            starterQuestHudPinned = starterQuest == null || starterQuest.IsHudPinned,
            starterQuestHudPinPreferenceSaved = true,
            currentDay = dayNightCycle != null ? dayNightCycle.CurrentDay : 1,
            normalizedTimeOfDay = dayNightCycle != null ? dayNightCycle.CurrentNormalizedTime : 0f,
            selectedHotbarIndex = hotbar.SelectedIndex,
            equipment = playerEquipment != null ? playerEquipment.CaptureEquipment() : new List<SaveEquipmentSlotData>(),
            bestiary = BestiaryService.Instance != null ? BestiaryService.Instance.CaptureProgress() : new List<SaveBestiaryEntryData>(),
            activeAncestralPowers = ancestralPowers != null ? ancestralPowers.CapturePowerIds() : new List<string>(),
            bearerPowerId = bearerPower != null ? bearerPower.CurrentPowerId : string.Empty,
            bearerPowerSelectionCompleted = bearerPower != null && bearerPower.HasCompletedSelection,
            worldEntities = manager.CaptureSavedWorldEntities()
        };

        manager.CaptureBearerPowerOwnership(data.bearerPowerOwnerPowerIds, data.bearerPowerOwnerPlayerIds);

        if (currentSceneSet?.sceneNames != null)
            data.sceneNames.AddRange(currentSceneSet.sceneNames);

        foreach (InventoryItem item in inventory.items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemName) || item.quantity <= 0)
                continue;

            data.inventory.Add(new SaveInventoryItemData
            {
                itemName = item.itemName,
                prefabName = item.prefabName,
                quantity = item.quantity,
                isBottle = item.isBottle,
                bottleIsFilled = item.bottleIsFilled
            });
        }

        if (hotbar.slots != null)
        {
            foreach (HotbarSlot slot in hotbar.slots)
            {
                if (slot == null || slot.IsEmpty() || slot.GetItemData() == null || slot.GetAmount() <= 0)
                {
                    data.hotbar.Add(new SaveHotbarSlotData());
                    continue;
                }

                data.hotbar.Add(new SaveHotbarSlotData
                {
                    itemName = slot.ItemName,
                    prefabName = slot.prefabName,
                    quantity = slot.GetAmount(),
                    isBottle = slot.isBottle,
                    bottleIsFilled = slot.bottleIsFilled
                });
            }
        }

        File.WriteAllText(MultiplayerSessionSavePath, JsonUtility.ToJson(data, true));

        if (showMessage)
            MessageSystem.Instance?.ShowMessage("Sessao multiplayer salva");

        return true;
    }

    public bool SaveClientSession(bool showMessage = false)
    {
        ResolveReferences();

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager == null || manager.Mode != LanMultiplayerManager.SessionMode.Client || !manager.IsSessionReady)
            return false;

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || GameState.IsPowerSelectionOpen)
            return false;

        if (playerMovement == null || inventory == null || hotbar == null || progression == null)
            return false;

        MultiplayerSceneSetState currentSceneSet = manager.CaptureCurrentSceneSet();

        MultiplayerSessionSaveData data = new MultiplayerSessionSaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            sceneSetId = currentSceneSet?.sceneSetId,
            activeSceneName = currentSceneSet?.activeSceneName,
            worldSeed = manager.WorldSeed,
            playerPosX = playerMovement.transform.position.x,
            playerPosY = playerMovement.transform.position.y,
            playerPosZ = playerMovement.transform.position.z,
            playerRotY = playerMovement.transform.eulerAngles.y,
            thirdPerson = playerMovement.thirdPerson,
            health = playerMovement.currentHealth,
            stamina = playerMovement.currentStamina,
            hunger = playerMovement.currentHunger,
            thirst = playerMovement.currentThirst,
            hasUnlockedAreaMagic = playerMagic != null && playerMagic.hasUnlockedAreaMagic,
            hasSilverLoadoutGranted = progression.HasSilverLoadoutGranted,
            currentXp = progression.currentXp,
            starterQuestIndex = starterQuest != null ? starterQuest.CurrentObjectiveIndex : 0,
            starterQuestBestiaryOpened = starterQuest != null && starterQuest.HasOpenedBestiary,
            starterQuestCompleted = starterQuest != null && starterQuest.IsCompleted,
            starterQuestHudPinned = starterQuest == null || starterQuest.IsHudPinned,
            starterQuestHudPinPreferenceSaved = true,
            currentDay = dayNightCycle != null ? dayNightCycle.CurrentDay : 1,
            normalizedTimeOfDay = dayNightCycle != null ? dayNightCycle.CurrentNormalizedTime : 0f,
            selectedHotbarIndex = hotbar.SelectedIndex,
            equipment = playerEquipment != null ? playerEquipment.CaptureEquipment() : new List<SaveEquipmentSlotData>(),
            bestiary = BestiaryService.Instance != null ? BestiaryService.Instance.CaptureProgress() : new List<SaveBestiaryEntryData>(),
            activeAncestralPowers = ancestralPowers != null ? ancestralPowers.CapturePowerIds() : new List<string>(),
            bearerPowerId = bearerPower != null ? bearerPower.CurrentPowerId : string.Empty,
            bearerPowerSelectionCompleted = bearerPower != null && bearerPower.HasCompletedSelection
        };

        if (currentSceneSet?.sceneNames != null)
            data.sceneNames.AddRange(currentSceneSet.sceneNames);

        foreach (InventoryItem item in inventory.items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemName) || item.quantity <= 0)
                continue;

            data.inventory.Add(new SaveInventoryItemData
            {
                itemName = item.itemName,
                prefabName = item.prefabName,
                quantity = item.quantity,
                isBottle = item.isBottle,
                bottleIsFilled = item.bottleIsFilled
            });
        }

        if (hotbar.slots != null)
        {
            foreach (HotbarSlot slot in hotbar.slots)
            {
                if (slot == null || slot.IsEmpty() || slot.GetItemData() == null || slot.GetAmount() <= 0)
                {
                    data.hotbar.Add(new SaveHotbarSlotData());
                    continue;
                }

                data.hotbar.Add(new SaveHotbarSlotData
                {
                    itemName = slot.ItemName,
                    prefabName = slot.prefabName,
                    quantity = slot.GetAmount(),
                    isBottle = slot.isBottle,
                    bottleIsFilled = slot.bottleIsFilled
                });
            }
        }

        string savePath = GetClientSessionSavePath(manager.CurrentAddress, manager.CurrentPort);
        File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        lastAppliedClientSessionSaveKey = BuildClientSessionSaveKey(manager.CurrentAddress, manager.CurrentPort);

        if (showMessage)
            MessageSystem.Instance?.ShowMessage("Progresso do servidor salvo");

        return true;
    }

    public bool LoadGame()
    {
        ResolveReferences();
        if (LanMultiplayerManager.Instance != null && LanMultiplayerManager.Instance.IsMultiplayerActive)
            return false;

        if (!HasSave() || playerMovement == null || inventory == null || hotbar == null)
            return false;

        SaveGameData data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SavePath));
        if (data == null)
            return false;

        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
        GameState.IsBestiaryOpen = false;
        GameState.IsQuestJournalOpen = false;
        GameState.IsPaused = false;
        progression ??= playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
        playerMagic ??= playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
        starterQuest ??= playerMovement.GetComponent<StarterQuestTracker>() ?? playerMovement.gameObject.AddComponent<StarterQuestTracker>();
        ancestralPowers ??= playerMovement.GetComponent<AncestralPowerService>() ?? playerMovement.gameObject.AddComponent<AncestralPowerService>();
        bearerPower ??= playerMovement.GetComponent<BearerPowerService>() ?? playerMovement.gameObject.AddComponent<BearerPowerService>();
        if (data.worldSeed != 0 && LanMultiplayerManager.Instance != null)
            LanMultiplayerManager.Instance.StartSolo(data.worldSeed);

        DemoWorldProgression.ApplyLandmarkLayout(gameObject.scene);
        progression.LoadProgress(data.currentXp, data.hasSilverLoadoutGranted);
        playerMagic.LoadState(data.hasUnlockedAreaMagic);
        starterQuest.LoadProgress(data.starterQuestIndex, data.starterQuestBestiaryOpened, data.starterQuestCompleted, ResolveStarterQuestHudPinned(data.starterQuestHudPinned, data.starterQuestHudPinPreferenceSaved));
        BestiaryService.Instance?.LoadProgress(data.bestiary);
        ancestralPowers.LoadPowers(data.activeAncestralPowers);
        bearerPower.LoadState(data.bearerPowerId, data.bearerPowerSelectionCompleted);

        Quaternion rotation = Quaternion.Euler(0f, data.playerRotY, 0f);
        Vector3 savedPosition = new Vector3(data.playerPosX, data.playerPosY, data.playerPosZ);
        ResolveSavedStartTransform(data.sceneName, ref savedPosition, ref rotation);
        bool repairedEarlyStart = TryRepairEarlySoloStart(data, ref savedPosition, ref rotation);
        playerMovement.ApplySavedState(
            savedPosition,
            rotation,
            data.thirdPerson,
            repairedEarlyStart ? playerMovement.maxHealth : data.health,
            repairedEarlyStart ? playerMovement.maxStamina : data.stamina,
            repairedEarlyStart ? playerMovement.maxHunger : data.hunger,
            repairedEarlyStart ? playerMovement.maxThirst : data.thirst
        );

        if (dayNightCycle != null)
            dayNightCycle.LoadState(data.currentDay, data.normalizedTimeOfDay);

        inventory.ClearAll();
        RestoreInventory(data.inventory);
        RestoreHotbar(data.hotbar);
        playerEquipment?.LoadEquipment(data.equipment);

        int selectedIndex = hotbar.slots != null && hotbar.slots.Length > 0
            ? Mathf.Clamp(data.selectedHotbarIndex, 0, hotbar.slots.Length - 1)
            : 0;

        hotbar.SetSelectedIndex(selectedIndex);
        if (playerInteraction != null)
            playerInteraction.SelectSlotIndex(selectedIndex);

        if (inventoryUI != null)
            inventoryUI.Refresh();

        autoSaveTimer = 0f;
        return true;
    }

    public void DeleteSave()
    {
        if (HasSave())
            File.Delete(SavePath);
    }

    public void DeleteMultiplayerSessionSave()
    {
        if (HasMultiplayerSessionSave())
            File.Delete(MultiplayerSessionSavePath);
    }

    public bool ResetCurrentPlayerProgress(bool showMessage = true)
    {
        ResolveReferences();

        if (playerMovement == null || inventory == null || hotbar == null)
            return false;

        progression ??= playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
        playerMagic ??= playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
        starterQuest ??= playerMovement.GetComponent<StarterQuestTracker>() ?? playerMovement.gameObject.AddComponent<StarterQuestTracker>();

        inventory.ClearAll();
        hotbar.ClearAll();
        playerEquipment?.ClearAll();
        progression.LoadProgress(0, false);
        playerMagic.LoadState(false);
        starterQuest.ResetProgress();
        BestiaryService.Instance?.ResetProgress();
        ancestralPowers?.ClearPowers(false);
        BearerShadowClone.DestroyAllForOwner(playerMovement);
        bearerPower?.ResetForNewAdventure();
        playerMovement.ResetToFreshStart();
        playerInteraction?.ResetStarterLoadout();

        int selectedIndex = hotbar.slots != null && hotbar.slots.Length > 0 ? 0 : -1;
        if (selectedIndex >= 0)
        {
            hotbar.SetSelectedIndex(selectedIndex);
            playerInteraction?.SelectSlotIndex(selectedIndex);
        }

        inventoryUI?.Refresh();
        SceneObjectCache.Find<GoldHUD>(gameObject.scene, true)?.Refresh();
        SceneObjectCache.Find<LevelHUD>(gameObject.scene, true)?.Refresh();

        bool wasPaused = GameState.IsPaused;
        if (wasPaused)
            GameState.IsPaused = false;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.Mode == LanMultiplayerManager.SessionMode.Host && manager.IsSessionReady)
            SaveMultiplayerSession(false);
        else if (manager != null && manager.Mode == LanMultiplayerManager.SessionMode.Client && manager.IsSessionReady)
            SaveClientSession(false);
        else if (manager == null || !manager.IsMultiplayerActive)
            SaveGame(false);

        if (wasPaused)
            GameState.IsPaused = true;

        if (showMessage)
            MessageSystem.Instance?.ShowMessage("Personagem reiniciado.");

        return true;
    }

    void RestoreInventory(List<SaveInventoryItemData> savedItems)
    {
        if (savedItems == null)
            return;

        foreach (SaveInventoryItemData savedItem in savedItems)
        {
            Item resolvedItem = ResolveItem(savedItem.itemName, savedItem.prefabName);
            if (resolvedItem == null)
                continue;

            InventoryItem inventoryItem = new InventoryItem(savedItem.itemName, savedItem.quantity, resolvedItem);
            if (savedItem.isBottle)
                inventoryItem.SetBottleState(savedItem.bottleIsFilled);

            inventory.AddInventoryItem(inventoryItem);
        }
    }

    void RestoreHotbar(List<SaveHotbarSlotData> savedSlots)
    {
        hotbar.ClearAll();

        if (hotbar.slots == null || savedSlots == null)
            return;

        int count = Mathf.Min(hotbar.slots.Length, savedSlots.Count);
        for (int i = 0; i < count; i++)
        {
            SaveHotbarSlotData savedSlot = savedSlots[i];
            if (savedSlot == null || string.IsNullOrWhiteSpace(savedSlot.itemName) || savedSlot.quantity <= 0)
                continue;

            Item resolvedItem = ResolveItem(savedSlot.itemName, savedSlot.prefabName);
            if (resolvedItem == null)
                continue;

            hotbar.slots[i].SetItem(savedSlot.itemName, resolvedItem.icon, resolvedItem, savedSlot.quantity);

            if (savedSlot.isBottle)
                hotbar.slots[i].SetBottleState(savedSlot.bottleIsFilled);
        }
    }

    Item ResolveItem(string itemName, string prefabName)
    {
        Item resolvedInventoryItem = InventoryItemResolver.Resolve(itemName, prefabName);
        if (resolvedInventoryItem != null)
            return resolvedInventoryItem;

        if (string.Equals(itemName, "Gold", StringComparison.OrdinalIgnoreCase))
            return GoldItemRegistry.GetOrCreate();

        if (string.Equals(itemName, "Magia Ancestral", StringComparison.OrdinalIgnoreCase))
            return MagicSpellItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RustyMetalItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RustyMetalItemRegistry.GetOrCreate();

        if (string.Equals(itemName, IronItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(itemName, "Ferro", StringComparison.OrdinalIgnoreCase))
            return IronItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RefinedIronItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RefinedIronItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RustySwordItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RustySwordItemRegistry.GetOrCreate();

        if (string.Equals(itemName, FurnaceItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return FurnaceItemRegistry.GetOrCreate();

        if (string.Equals(itemName, SimpleBowItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return SimpleBowItemRegistry.GetOrCreate();

        if (string.Equals(itemName, FeatherItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return FeatherItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RawChickenMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RawChickenMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedChickenMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedChickenMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedBoarMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedBoarMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CowMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CowMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedCowMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedCowMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, ArrowItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return ArrowItemRegistry.GetOrCreate();

        if (string.Equals(itemName, "Machado", StringComparison.OrdinalIgnoreCase))
            return LoadResourceItem("VendorItems/Axe");

        if (string.Equals(itemName, "Picareta", StringComparison.OrdinalIgnoreCase))
            return LoadResourceItem("VendorItems/Axepick");

        GameObject[] prefabs = Resources.FindObjectsOfTypeAll<GameObject>();

        if (!string.IsNullOrWhiteSpace(prefabName))
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null || prefab.name != prefabName)
                    continue;

                Item item = prefab.GetComponent<Item>();
                if (item != null)
                    return item;
            }
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
                continue;

            Item item = prefab.GetComponent<Item>();
            if (item != null && item.itemName == itemName)
                return item;
        }

        return null;
    }

    Item LoadResourceItem(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        return prefab != null ? prefab.GetComponent<Item>() : null;
    }

    void ResolveReferences()
    {
        PlayerMovement activePlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (activePlayer != null && activePlayer != playerMovement)
        {
            playerMovement = activePlayer;
            playerInteraction = null;
            inventory = null;
            playerEquipment = null;
            progression = null;
            playerMagic = null;
            starterQuest = null;
            ancestralPowers = null;
            bearerPower = null;
        }

        if (playerMovement == null)
            playerMovement = activePlayer;

        if (playerMovement != null)
        {
            playerInteraction = playerMovement.GetComponent<PlayerInteraction>() ?? playerInteraction;
            inventory = playerMovement.GetComponent<Inventory>() ?? inventory;
            playerEquipment = playerMovement.GetComponent<PlayerEquipment>() ?? playerMovement.gameObject.AddComponent<PlayerEquipment>();
            progression = playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
            playerMagic = playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
            starterQuest = playerMovement.GetComponent<StarterQuestTracker>() ?? playerMovement.gameObject.AddComponent<StarterQuestTracker>();
            ancestralPowers = playerMovement.GetComponent<AncestralPowerService>() ?? playerMovement.gameObject.AddComponent<AncestralPowerService>();
            bearerPower = playerMovement.GetComponent<BearerPowerService>() ?? playerMovement.gameObject.AddComponent<BearerPowerService>();
        }

        if (playerInteraction == null)
            playerInteraction = playerMovement != null
                ? playerMovement.GetComponent<PlayerInteraction>()
                : SceneObjectCache.Find<PlayerInteraction>(gameObject.scene, true);

        if (inventory == null)
            inventory = playerMovement != null
                ? playerMovement.GetComponent<Inventory>()
                : SceneObjectCache.Find<Inventory>(gameObject.scene, true);

        if (hotbar == null)
            hotbar = SceneObjectCache.Find<Hotbar>(gameObject.scene, true);

        if (dayNightCycle == null)
            dayNightCycle = SceneObjectCache.Find<DayNightCycle>(gameObject.scene, true);

        if (inventoryUI == null)
            inventoryUI = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
    }

    bool ResolveStarterQuestHudPinned(bool savedValue, bool preferenceWasSaved)
    {
        return !preferenceWasSaved || savedValue;
    }

    void ExitLobby()
    {
        GameState.IsInLobby = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        WorldLoadingScreen.BeginLoading();
    }

    void ResolveSavedStartTransform(string savedSceneName, ref Vector3 position, ref Quaternion rotation)
    {
        if (string.Equals(savedSceneName, "Main", StringComparison.Ordinal))
            return;

        if (playerMovement != null)
        {
            position = playerMovement.transform.position;
            rotation = playerMovement.transform.rotation;
            return;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
    }

    bool TryRepairEarlySoloStart(SaveGameData data, ref Vector3 position, ref Quaternion rotation)
    {
        if (data == null)
            return false;

        if (!string.Equals(data.sceneName, "Main", StringComparison.Ordinal))
            return false;

        if (!LooksLikeEarlySoloStart(data))
            return false;

        string repairReason = ResolveEarlySoloStartRepairReason(position);
        if (string.IsNullOrEmpty(repairReason))
            return false;

        Debug.LogWarning(
            $"Save inicial estava {repairReason} ({position}). " +
            "Reposicionando para o inicio seguro da ilha."
        );

        position = PlayerMovement.DefaultFreshStartPosition;
        rotation = PlayerMovement.DefaultFreshStartRotation;
        return true;
    }

    bool LooksLikeEarlySoloStart(SaveGameData data)
    {
        if (data.currentXp > EarlyStartMaxRepairXp ||
            data.starterQuestIndex > EarlyStartMaxRepairQuestIndex ||
            data.starterQuestBestiaryOpened ||
            data.starterQuestCompleted)
        {
            return false;
        }

        return true;
    }

    string ResolveEarlySoloStartRepairReason(Vector3 position)
    {
        Vector3 portalAnchor = ResolvePowerCaveReturnAnchor();
        if (PlanarDistance(position, portalAnchor) <= EarlyStartPortalRepairRadius)
            return "perto do retorno antigo da Caverna dos Portadores";

        if (DemoWorldProgression.IsNearKnownLegacyStart(
            position,
            EarlyStartVillageRepairRadius,
            EarlyStartBossRepairRadius,
            EarlyStartLegacyPlayerRepairRadius))
        {
            return "perto da vila, boss ou spawn antigo";
        }

        if (DemoWorldProgression.IsNearCurrentProgressLandmarks(
            position,
            EarlyStartVillageRepairRadius,
            Mathf.Max(EarlyStartBossRepairRadius, DemoWorldProgression.FinalBossLandmarkRevealDistance)))
        {
            return "perto da vila ou boss da seed atual";
        }

        return string.Empty;
    }

    Vector3 ResolvePowerCaveReturnAnchor()
    {
        BossSpawnPoint bossSpawnPoint = SceneObjectCache.Find<BossSpawnPoint>(gameObject.scene, true);
        return bossSpawnPoint != null
            ? bossSpawnPoint.transform.position
            : PowerCaveReturnFallbackPosition;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    void QueueClientSessionLoadIfAvailable(LanMultiplayerManager manager)
    {
        if (manager == null || manager.Mode != LanMultiplayerManager.SessionMode.Client || !manager.IsSessionReady)
            return;

        string saveKey = BuildClientSessionSaveKey(manager.CurrentAddress, manager.CurrentPort);
        if (pendingClientSessionLoad != null && pendingClientSessionSaveKey == saveKey)
            return;

        if (lastAppliedClientSessionSaveKey == saveKey)
            return;

        string savePath = GetClientSessionSavePath(manager.CurrentAddress, manager.CurrentPort);
        if (!File.Exists(savePath))
            return;

        MultiplayerSessionSaveData data = JsonUtility.FromJson<MultiplayerSessionSaveData>(File.ReadAllText(savePath));
        if (data == null)
            return;

        pendingClientSessionLoad = data;
        pendingClientSessionSaveKey = saveKey;
    }

    void TryApplyPendingMultiplayerSessionLoad()
    {
        if (pendingMultiplayerSessionLoad == null)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager == null || manager.Mode != LanMultiplayerManager.SessionMode.Host || !manager.IsSessionReady)
            return;

        ResolveReferences();
        if (playerMovement == null || inventory == null || hotbar == null)
            return;

        progression ??= playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
        playerMagic ??= playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
        starterQuest ??= playerMovement.GetComponent<StarterQuestTracker>() ?? playerMovement.gameObject.AddComponent<StarterQuestTracker>();
        ancestralPowers ??= playerMovement.GetComponent<AncestralPowerService>() ?? playerMovement.gameObject.AddComponent<AncestralPowerService>();
        bearerPower ??= playerMovement.GetComponent<BearerPowerService>() ?? playerMovement.gameObject.AddComponent<BearerPowerService>();
        progression.LoadProgress(pendingMultiplayerSessionLoad.currentXp, pendingMultiplayerSessionLoad.hasSilverLoadoutGranted);
        playerMagic.LoadState(pendingMultiplayerSessionLoad.hasUnlockedAreaMagic);
        starterQuest.LoadProgress(pendingMultiplayerSessionLoad.starterQuestIndex, pendingMultiplayerSessionLoad.starterQuestBestiaryOpened, pendingMultiplayerSessionLoad.starterQuestCompleted, ResolveStarterQuestHudPinned(pendingMultiplayerSessionLoad.starterQuestHudPinned, pendingMultiplayerSessionLoad.starterQuestHudPinPreferenceSaved));
        BestiaryService.Instance?.LoadProgress(pendingMultiplayerSessionLoad.bestiary);
        ancestralPowers.LoadPowers(pendingMultiplayerSessionLoad.activeAncestralPowers);
        bearerPower.LoadState(pendingMultiplayerSessionLoad.bearerPowerId, pendingMultiplayerSessionLoad.bearerPowerSelectionCompleted);
        manager.RestoreBearerPowerOwnership(
            pendingMultiplayerSessionLoad.bearerPowerOwnerPowerIds,
            pendingMultiplayerSessionLoad.bearerPowerOwnerPlayerIds);

        Quaternion rotation = Quaternion.Euler(0f, pendingMultiplayerSessionLoad.playerRotY, 0f);
        Vector3 savedPosition = new Vector3(
            pendingMultiplayerSessionLoad.playerPosX,
            pendingMultiplayerSessionLoad.playerPosY,
            pendingMultiplayerSessionLoad.playerPosZ
        );
        ResolveSavedStartTransform(pendingMultiplayerSessionLoad.sceneName, ref savedPosition, ref rotation);
        playerMovement.ApplySavedState(
            savedPosition,
            rotation,
            pendingMultiplayerSessionLoad.thirdPerson,
            pendingMultiplayerSessionLoad.health,
            pendingMultiplayerSessionLoad.stamina,
            pendingMultiplayerSessionLoad.hunger,
            pendingMultiplayerSessionLoad.thirst
        );

        if (dayNightCycle != null)
            dayNightCycle.LoadState(pendingMultiplayerSessionLoad.currentDay, pendingMultiplayerSessionLoad.normalizedTimeOfDay);

        inventory.ClearAll();
        RestoreInventory(pendingMultiplayerSessionLoad.inventory);
        RestoreHotbar(pendingMultiplayerSessionLoad.hotbar);
        playerEquipment?.LoadEquipment(pendingMultiplayerSessionLoad.equipment);

        int selectedIndex = hotbar.slots != null && hotbar.slots.Length > 0
            ? Mathf.Clamp(pendingMultiplayerSessionLoad.selectedHotbarIndex, 0, hotbar.slots.Length - 1)
            : 0;

        hotbar.SetSelectedIndex(selectedIndex);
        if (playerInteraction != null)
            playerInteraction.SelectSlotIndex(selectedIndex);

        manager.RestoreSavedWorldEntities(pendingMultiplayerSessionLoad.worldEntities);

        if (inventoryUI != null)
            inventoryUI.Refresh();

        pendingMultiplayerSessionLoad = null;
        autoSaveTimer = 0f;
        MessageSystem.Instance?.ShowMessage("Sessao multiplayer carregada");
    }

    void TryApplyPendingClientSessionLoad()
    {
        if (pendingClientSessionLoad == null)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager == null || manager.Mode != LanMultiplayerManager.SessionMode.Client || !manager.IsSessionReady)
            return;

        ResolveReferences();
        if (playerMovement == null || inventory == null || hotbar == null)
            return;

        progression ??= playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
        playerMagic ??= playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
        starterQuest ??= playerMovement.GetComponent<StarterQuestTracker>() ?? playerMovement.gameObject.AddComponent<StarterQuestTracker>();
        ancestralPowers ??= playerMovement.GetComponent<AncestralPowerService>() ?? playerMovement.gameObject.AddComponent<AncestralPowerService>();
        bearerPower ??= playerMovement.GetComponent<BearerPowerService>() ?? playerMovement.gameObject.AddComponent<BearerPowerService>();
        progression.LoadProgress(pendingClientSessionLoad.currentXp, pendingClientSessionLoad.hasSilverLoadoutGranted);
        playerMagic.LoadState(pendingClientSessionLoad.hasUnlockedAreaMagic);
        starterQuest.LoadProgress(pendingClientSessionLoad.starterQuestIndex, pendingClientSessionLoad.starterQuestBestiaryOpened, pendingClientSessionLoad.starterQuestCompleted, ResolveStarterQuestHudPinned(pendingClientSessionLoad.starterQuestHudPinned, pendingClientSessionLoad.starterQuestHudPinPreferenceSaved));
        BestiaryService.Instance?.LoadProgress(pendingClientSessionLoad.bestiary);
        ancestralPowers.LoadPowers(pendingClientSessionLoad.activeAncestralPowers);
        bearerPower.LoadState(pendingClientSessionLoad.bearerPowerId, pendingClientSessionLoad.bearerPowerSelectionCompleted);

        Quaternion rotation = Quaternion.Euler(0f, pendingClientSessionLoad.playerRotY, 0f);
        Vector3 savedPosition = new Vector3(
            pendingClientSessionLoad.playerPosX,
            pendingClientSessionLoad.playerPosY,
            pendingClientSessionLoad.playerPosZ
        );
        ResolveSavedStartTransform(pendingClientSessionLoad.sceneName, ref savedPosition, ref rotation);
        playerMovement.ApplySavedState(
            savedPosition,
            rotation,
            pendingClientSessionLoad.thirdPerson,
            pendingClientSessionLoad.health,
            pendingClientSessionLoad.stamina,
            pendingClientSessionLoad.hunger,
            pendingClientSessionLoad.thirst
        );

        inventory.ClearAll();
        RestoreInventory(pendingClientSessionLoad.inventory);
        RestoreHotbar(pendingClientSessionLoad.hotbar);
        playerEquipment?.LoadEquipment(pendingClientSessionLoad.equipment);

        int selectedIndex = hotbar.slots != null && hotbar.slots.Length > 0
            ? Mathf.Clamp(pendingClientSessionLoad.selectedHotbarIndex, 0, hotbar.slots.Length - 1)
            : 0;

        hotbar.SetSelectedIndex(selectedIndex);
        if (playerInteraction != null)
            playerInteraction.SelectSlotIndex(selectedIndex);

        if (inventoryUI != null)
            inventoryUI.Refresh();

        lastAppliedClientSessionSaveKey = pendingClientSessionSaveKey;
        pendingClientSessionLoad = null;
        pendingClientSessionSaveKey = null;
        autoSaveTimer = 0f;
        MessageSystem.Instance?.ShowMessage("Progresso do servidor restaurado");
    }

    MultiplayerSceneSetState BuildSavedSceneSet(MultiplayerSessionSaveData data)
    {
        if (data == null)
            return null;

        List<string> sceneNames = new List<string>();
        if (data.sceneNames != null)
        {
            for (int i = 0; i < data.sceneNames.Count; i++)
            {
                string sceneName = data.sceneNames[i];
                if (string.IsNullOrWhiteSpace(sceneName) || sceneNames.Contains(sceneName))
                    continue;

                sceneNames.Add(sceneName);
            }
        }

        if (sceneNames.Count == 0 && !string.IsNullOrWhiteSpace(data.sceneName))
            sceneNames.Add(data.sceneName);

        return MultiplayerSceneSetCatalog.Normalize(new MultiplayerSceneSetState
        {
            sceneSetId = data.sceneSetId,
            activeSceneName = string.IsNullOrWhiteSpace(data.activeSceneName) ? data.sceneName : data.activeSceneName,
            sceneNames = sceneNames.ToArray()
        });
    }

    void OnApplicationQuit()
    {
        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Host &&
            LanMultiplayerManager.Instance.IsSessionReady)
        {
            SaveMultiplayerSession();
            return;
        }

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Client &&
            LanMultiplayerManager.Instance.IsSessionReady)
        {
            SaveClientSession();
            return;
        }

        if (LanMultiplayerManager.Instance == null || !LanMultiplayerManager.Instance.IsMultiplayerActive)
            SaveGame();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            return;

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Host &&
            LanMultiplayerManager.Instance.IsSessionReady)
        {
            SaveMultiplayerSession();
            return;
        }

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Client &&
            LanMultiplayerManager.Instance.IsSessionReady)
        {
            SaveClientSession();
            return;
        }

        if (LanMultiplayerManager.Instance == null || !LanMultiplayerManager.Instance.IsMultiplayerActive)
            SaveGame();
    }

    static string GetClientSessionSavePath(string address, int port)
    {
        return Path.Combine(Application.persistentDataPath, $"{BuildClientSessionSaveKey(address, port)}.json");
    }

    static string BuildClientSessionSaveKey(string address, int port)
    {
        string safeAddress = string.IsNullOrWhiteSpace(address) ? "unknown" : address.Trim();

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            safeAddress = safeAddress.Replace(invalidChar, '_');

        safeAddress = safeAddress.Replace(':', '_').Replace('/', '_').Replace('\\', '_').Replace('.', '_');
        return $"multiplayer_client_{safeAddress}_{Mathf.Clamp(port, 1, 65535)}";
    }
}
