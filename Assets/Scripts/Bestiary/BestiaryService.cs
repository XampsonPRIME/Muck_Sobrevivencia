using System;
using System.Collections.Generic;
using UnityEngine;

public class BestiaryService : MonoBehaviour
{
    public static BestiaryService Instance { get; private set; }

    readonly Dictionary<string, SaveBestiaryEntryData> progressById = new Dictionary<string, SaveBestiaryEntryData>();

    PlayerMovement trackedPlayer;
    BestiaryTracker trackedTracker;
    float nextTrackerCheckTime;

    public event Action OnProgressChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<BestiaryService>() != null)
            return;

        GameObject serviceObject = new GameObject("BestiaryService");
        serviceObject.AddComponent<BestiaryService>();
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
        BestiaryDatabase.EnsureInitialized();
    }

    void Update()
    {
        if (Time.unscaledTime < nextTrackerCheckTime)
            return;

        nextTrackerCheckTime = Time.unscaledTime + 1f;
        EnsureTracker();
    }

    public bool Discover(string creatureId, bool showMessage = true)
    {
        BestiaryCreatureData data = BestiaryDatabase.Get(creatureId);
        if (data == null)
            return false;

        SaveBestiaryEntryData entry = GetOrCreateEntry(creatureId);
        if (entry.discovered)
            return false;

        entry.discovered = true;
        entry.firstDiscoveredAt = string.IsNullOrWhiteSpace(entry.firstDiscoveredAt) ? BuildTimestamp() : entry.firstDiscoveredAt;
        OnProgressChanged?.Invoke();

        if (showMessage && !GameState.IsInLobby && !GameState.IsPlayerDead)
            MessageSystem.Instance?.ShowMessage($"Nova entrada no bestiario: {data.displayName}");

        return true;
    }

    public bool RecordDefeat(string creatureId)
    {
        BestiaryCreatureData data = BestiaryDatabase.Get(creatureId);
        if (data == null)
            return false;

        SaveBestiaryEntryData entry = GetOrCreateEntry(creatureId);
        bool changed = false;

        if (!entry.discovered)
        {
            entry.discovered = true;
            entry.firstDiscoveredAt = string.IsNullOrWhiteSpace(entry.firstDiscoveredAt) ? BuildTimestamp() : entry.firstDiscoveredAt;
            changed = true;
        }

        entry.defeatCount = Mathf.Max(0, entry.defeatCount) + 1;

        if (!entry.defeated)
        {
            entry.defeated = true;
            entry.firstDefeatedAt = string.IsNullOrWhiteSpace(entry.firstDefeatedAt) ? BuildTimestamp() : entry.firstDefeatedAt;
            changed = true;
            MessageSystem.Instance?.ShowMessage($"Bestiario atualizado: {data.displayName}");
        }

        OnProgressChanged?.Invoke();
        return changed;
    }

    public SaveBestiaryEntryData GetProgress(string creatureId)
    {
        if (string.IsNullOrWhiteSpace(creatureId))
            return null;

        return progressById.TryGetValue(creatureId, out SaveBestiaryEntryData progress) ? progress : null;
    }

    public BestiaryDiscoveryState GetState(string creatureId)
    {
        SaveBestiaryEntryData progress = GetProgress(creatureId);
        if (progress == null || !progress.discovered)
            return BestiaryDiscoveryState.NotDiscovered;

        return progress.defeated ? BestiaryDiscoveryState.Defeated : BestiaryDiscoveryState.Discovered;
    }

    public List<SaveBestiaryEntryData> CaptureProgress()
    {
        List<SaveBestiaryEntryData> results = new List<SaveBestiaryEntryData>();
        foreach (SaveBestiaryEntryData entry in progressById.Values)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.creatureId))
                continue;

            results.Add(Clone(entry));
        }

        return results;
    }

    public void LoadProgress(List<SaveBestiaryEntryData> savedProgress)
    {
        progressById.Clear();
        BestiaryDatabase.EnsureInitialized();

        if (savedProgress != null)
        {
            for (int i = 0; i < savedProgress.Count; i++)
            {
                SaveBestiaryEntryData entry = savedProgress[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.creatureId) || BestiaryDatabase.Get(entry.creatureId) == null)
                    continue;

                progressById[entry.creatureId] = Clone(entry);
            }
        }

        OnProgressChanged?.Invoke();
    }

    public void ResetProgress()
    {
        progressById.Clear();
        OnProgressChanged?.Invoke();
    }

    public int CountDiscovered()
    {
        int count = 0;
        foreach (BestiaryCreatureData data in BestiaryDatabase.Creatures)
        {
            if (GetState(data.creatureId) != BestiaryDiscoveryState.NotDiscovered)
                count++;
        }

        return count;
    }

    public int CountDefeatedCreatures()
    {
        int count = 0;
        foreach (BestiaryCreatureData data in BestiaryDatabase.Creatures)
        {
            if (GetState(data.creatureId) == BestiaryDiscoveryState.Defeated)
                count++;
        }

        return count;
    }

    public int CountTotalDefeats()
    {
        int total = 0;
        foreach (SaveBestiaryEntryData entry in progressById.Values)
        {
            if (entry != null)
                total += Mathf.Max(0, entry.defeatCount);
        }

        return total;
    }

    public int GetCompletionPercent()
    {
        int total = BestiaryDatabase.Creatures.Count;
        if (total <= 0)
            return 0;

        return Mathf.RoundToInt(CountDefeatedCreatures() / (float)total * 100f);
    }

    SaveBestiaryEntryData GetOrCreateEntry(string creatureId)
    {
        if (!progressById.TryGetValue(creatureId, out SaveBestiaryEntryData entry) || entry == null)
        {
            entry = new SaveBestiaryEntryData { creatureId = creatureId };
            progressById[creatureId] = entry;
        }

        return entry;
    }

    void EnsureTracker()
    {
        if (GameState.IsInLobby)
            return;

        if (trackedPlayer == null || !trackedPlayer.gameObject.activeInHierarchy)
        {
            trackedPlayer = LanMultiplayerManager.FindGameplayPlayer();
            trackedTracker = null;
        }

        if (trackedPlayer == null)
            return;

        if (trackedTracker == null || !trackedTracker.isActiveAndEnabled)
            trackedTracker = trackedPlayer.GetComponent<BestiaryTracker>();

        if (trackedTracker == null)
            trackedTracker = trackedPlayer.gameObject.AddComponent<BestiaryTracker>();
    }

    static SaveBestiaryEntryData Clone(SaveBestiaryEntryData source)
    {
        return new SaveBestiaryEntryData
        {
            creatureId = source.creatureId,
            discovered = source.discovered,
            defeated = source.defeated,
            defeatCount = Mathf.Max(0, source.defeatCount),
            firstDiscoveredAt = source.firstDiscoveredAt,
            firstDefeatedAt = source.firstDefeatedAt
        };
    }

    static string BuildTimestamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    }
}
