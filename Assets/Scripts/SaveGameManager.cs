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
public class SaveGameData
{
    public string sceneName;
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
    public int currentDay;
    public float normalizedTimeOfDay;
    public int selectedHotbarIndex;
    public List<SaveInventoryItemData> inventory = new List<SaveInventoryItemData>();
    public List<SaveHotbarSlotData> hotbar = new List<SaveHotbarSlotData>();
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
    public int currentDay;
    public float normalizedTimeOfDay;
    public int selectedHotbarIndex;
    public List<SaveInventoryItemData> inventory = new List<SaveInventoryItemData>();
    public List<SaveHotbarSlotData> hotbar = new List<SaveHotbarSlotData>();
    public List<LanSavedEntityState> worldEntities = new List<LanSavedEntityState>();
}

public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }

    public float autoSaveInterval = 20f;

    PlayerMovement playerMovement;
    PlayerInteraction playerInteraction;
    Inventory inventory;
    Hotbar hotbar;
    DayNightCycle dayNightCycle;
    PlayerProgression progression;
    PlayerMagic playerMagic;
    InventoryUI inventoryUI;

    float autoSaveTimer;
    MultiplayerSessionSaveData pendingMultiplayerSessionLoad;
    MultiplayerSessionSaveData pendingClientSessionLoad;
    string pendingClientSessionSaveKey;
    string lastAppliedClientSessionSaveKey;

    static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    static string MultiplayerSessionSavePath => Path.Combine(Application.persistentDataPath, "multiplayer_session.json");

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
        DeleteSave();
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
        GameState.IsPaused = false;
        ExitLobby();
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

        MultiplayerSceneSetState startupSceneSet = MultiplayerSceneSetCatalog.ResolveStartupState("Overworld", "Main");
        if (startupSceneSet == null)
            return false;

        if (!LanMultiplayerManager.Instance.StartHost(port, data.worldSeed))
            return false;

        pendingMultiplayerSessionLoad = data;
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
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

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused)
            return false;

        if (playerMovement == null || inventory == null || hotbar == null || progression == null)
            return false;

        SaveGameData data = new SaveGameData
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
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
            currentDay = dayNightCycle != null ? dayNightCycle.CurrentDay : 1,
            normalizedTimeOfDay = dayNightCycle != null ? dayNightCycle.CurrentNormalizedTime : 0f,
            selectedHotbarIndex = hotbar.SelectedIndex
        };

        foreach (InventoryItem item in inventory.items)
        {
            if (item == null || item.itemData == null || item.quantity <= 0)
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

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused)
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
            currentDay = dayNightCycle != null ? dayNightCycle.CurrentDay : 1,
            normalizedTimeOfDay = dayNightCycle != null ? dayNightCycle.CurrentNormalizedTime : 0f,
            selectedHotbarIndex = hotbar.SelectedIndex,
            worldEntities = manager.CaptureSavedWorldEntities()
        };

        if (currentSceneSet?.sceneNames != null)
            data.sceneNames.AddRange(currentSceneSet.sceneNames);

        foreach (InventoryItem item in inventory.items)
        {
            if (item == null || item.itemData == null || item.quantity <= 0)
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

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused)
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
            currentDay = dayNightCycle != null ? dayNightCycle.CurrentDay : 1,
            normalizedTimeOfDay = dayNightCycle != null ? dayNightCycle.CurrentNormalizedTime : 0f,
            selectedHotbarIndex = hotbar.SelectedIndex
        };

        if (currentSceneSet?.sceneNames != null)
            data.sceneNames.AddRange(currentSceneSet.sceneNames);

        foreach (InventoryItem item in inventory.items)
        {
            if (item == null || item.itemData == null || item.quantity <= 0)
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
        GameState.IsPaused = false;
        progression ??= playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();
        playerMagic ??= playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
        progression.LoadProgress(data.currentXp, data.hasSilverLoadoutGranted);
        playerMagic.LoadState(data.hasUnlockedAreaMagic);

        Quaternion rotation = Quaternion.Euler(0f, data.playerRotY, 0f);
        playerMovement.ApplySavedState(
            new Vector3(data.playerPosX, data.playerPosY, data.playerPosZ),
            rotation,
            data.thirdPerson,
            data.health,
            data.stamina,
            data.hunger,
            data.thirst
        );

        if (dayNightCycle != null)
            dayNightCycle.LoadState(data.currentDay, data.normalizedTimeOfDay);

        inventory.ClearAll();
        RestoreInventory(data.inventory);
        RestoreHotbar(data.hotbar);

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

        inventory.ClearAll();
        hotbar.ClearAll();
        progression.LoadProgress(0, false);
        playerMagic.LoadState(false);
        playerMovement.ResetToFreshStart();
        playerInteraction?.ResetStarterLoadout();

        int selectedIndex = hotbar.slots != null && hotbar.slots.Length > 0 ? 0 : -1;
        if (selectedIndex >= 0)
        {
            hotbar.SetSelectedIndex(selectedIndex);
            playerInteraction?.SelectSlotIndex(selectedIndex);
        }

        inventoryUI?.Refresh();
        FindFirstObjectByType<GoldHUD>()?.Refresh();
        FindFirstObjectByType<LevelHUD>()?.Refresh();

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
        if (string.Equals(itemName, "Gold", StringComparison.OrdinalIgnoreCase))
            return GoldItemRegistry.GetOrCreate();

        if (string.Equals(itemName, "Magia Ancestral", StringComparison.OrdinalIgnoreCase))
            return MagicSpellItemRegistry.GetOrCreate();

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

    void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = LanMultiplayerManager.FindGameplayPlayer();

        if (playerInteraction == null)
            playerInteraction = playerMovement != null ? playerMovement.GetComponent<PlayerInteraction>() : FindFirstObjectByType<PlayerInteraction>();

        if (inventory == null)
            inventory = playerMovement != null ? playerMovement.GetComponent<Inventory>() : FindFirstObjectByType<Inventory>();

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();

        if (dayNightCycle == null)
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();

        if (progression == null && playerMovement != null)
            progression = playerMovement.GetComponent<PlayerProgression>() ?? playerMovement.gameObject.AddComponent<PlayerProgression>();

        if (playerMagic == null && playerMovement != null)
            playerMagic = playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();

        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>();
    }

    void ExitLobby()
    {
        GameState.IsInLobby = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        progression.LoadProgress(pendingMultiplayerSessionLoad.currentXp, pendingMultiplayerSessionLoad.hasSilverLoadoutGranted);
        playerMagic.LoadState(pendingMultiplayerSessionLoad.hasUnlockedAreaMagic);

        Quaternion rotation = Quaternion.Euler(0f, pendingMultiplayerSessionLoad.playerRotY, 0f);
        playerMovement.ApplySavedState(
            new Vector3(pendingMultiplayerSessionLoad.playerPosX, pendingMultiplayerSessionLoad.playerPosY, pendingMultiplayerSessionLoad.playerPosZ),
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
        progression.LoadProgress(pendingClientSessionLoad.currentXp, pendingClientSessionLoad.hasSilverLoadoutGranted);
        playerMagic.LoadState(pendingClientSessionLoad.hasUnlockedAreaMagic);

        Quaternion rotation = Quaternion.Euler(0f, pendingClientSessionLoad.playerRotY, 0f);
        playerMovement.ApplySavedState(
            new Vector3(pendingClientSessionLoad.playerPosX, pendingClientSessionLoad.playerPosY, pendingClientSessionLoad.playerPosZ),
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
