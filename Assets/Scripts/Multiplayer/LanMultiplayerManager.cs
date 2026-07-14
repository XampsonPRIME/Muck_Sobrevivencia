using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LanMultiplayerManager : MonoBehaviour
{
    public static event Action BearerPowerAvailabilityChanged;

    static bool dedicatedProcessRequested;
    static bool dedicatedStartupConsumed;
    static int dedicatedStartupPort = 7777;
    static int? dedicatedStartupWorldSeed;
    static string dedicatedStartupScene;
    static string dedicatedStartupSceneSetId;

    public enum SessionMode
    {
        None,
        Solo,
        Host,
        Client,
        DedicatedServer
    }

    public enum SessionState
    {
        Idle,
        Connecting,
        Ready,
        Error
    }

    [Serializable]
    class LanPacket
    {
        public string type;
        public string payload;
    }

    [Serializable]
    class LanHandshake
    {
        public string playerId;
        public string playerName;
    }

    [Serializable]
    public class LanPlayerState
    {
        public string playerId;
        public string playerName;
        public Vector3 position;
        public Quaternion rotation;
        public float lookPitch;
        public float animationSpeed;
        public bool thirdPerson;
        public bool isDead;
        public int level;
        public string bearerPowerId;
    }

    [Serializable]
    class LanWelcome
    {
        public string playerId;
        public int worldSeed;
        public string sceneName;
        public string sceneSetId;
        public string activeSceneName;
        public string[] sceneNames;
    }

    [Serializable]
    class LanLeave
    {
        public string playerId;
    }

    [Serializable]
    class LanHitRequest
    {
        public string playerId;
        public string entityId;
        public string entityKind;
        public int damage;
        public int toolType;
    }

    [Serializable]
    class LanEntityUpdate
    {
        public string entityId;
        public string entityKind;
        public int health;
        public bool destroyed;
    }

    [Serializable]
    class LanReward
    {
        public string playerId;
        public string itemName;
        public string prefabName;
        public int itemAmount;
        public int goldAmount;
        public int xpAmount;
        public bool unlockAreaMagic;
        public string bestiaryCreatureId;
        public string message;
    }

    [Serializable]
    class LanEnemyState
    {
        public string entityId;
        public string entityKind;
        public Vector3 position;
        public Quaternion rotation;
        public int level;
        public int health;
        public bool destroyed;
    }

    [Serializable]
    class LanWorldState
    {
        public int currentDay;
        public float normalizedTimeOfDay;
    }

    [Serializable]
    class LanSceneChange
    {
        public string sceneName;
        public string sceneSetId;
        public string activeSceneName;
        public string[] sceneNames;
    }

    [Serializable]
    class LanDamageEvent
    {
        public string playerId;
        public float damage;
    }

    [Serializable]
    class LanBearerPowerClaim
    {
        public string playerId;
        public string powerId;
    }

    [Serializable]
    class LanBearerPowerClaimResult
    {
        public string playerId;
        public string powerId;
        public bool accepted;
        public string message;
        public string[] claimedPowerIds;
    }

    [Serializable]
    class LanBearerPowerSnapshot
    {
        public string[] claimedPowerIds;
    }

    class PeerConnection
    {
        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        public Thread readThread;
        public readonly object writeLock = new object();
        public string playerId;
        public string playerName;
        public string addressLabel;
    }

    public static LanMultiplayerManager Instance { get; private set; }
    public static bool IsDedicatedProcessRequested => dedicatedProcessRequested;
    public static bool IsDedicatedRuntime => Instance != null && Instance.Mode == SessionMode.DedicatedServer;

    public SessionMode Mode { get; private set; } = SessionMode.None;
    public SessionState State { get; private set; } = SessionState.Idle;
    public bool IsMultiplayerActive => Mode == SessionMode.Host || Mode == SessionMode.Client || Mode == SessionMode.DedicatedServer;
    public bool IsServerAuthority => Mode == SessionMode.Host || Mode == SessionMode.DedicatedServer;
    public bool HasLocalGameplayPlayer => localPlayer != null;
    public bool IsSessionReady => State == SessionState.Ready;
    public string StatusMessage { get; private set; } = "Solo";
    public string LastErrorMessage { get; private set; }
    public string SessionId { get; private set; }
    public int CurrentPort { get; private set; } = 7777;
    public string CurrentAddress { get; private set; } = "127.0.0.1";
    public int WorldSeed => worldSeed;

    readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    readonly Dictionary<string, RemotePlayerReplica> remoteReplicas = new Dictionary<string, RemotePlayerReplica>();
    readonly Dictionary<string, Transform> serverPlayerTargets = new Dictionary<string, Transform>();
    readonly Dictionary<string, LanPlayerState> knownStates = new Dictionary<string, LanPlayerState>();
    readonly Dictionary<string, PeerConnection> hostPeers = new Dictionary<string, PeerConnection>();
    readonly Dictionary<string, string> destroyedEntities = new Dictionary<string, string>();
    readonly Dictionary<string, LanEntityUpdate> pendingEntityUpdates = new Dictionary<string, LanEntityUpdate>();
    readonly List<LanReward> pendingLocalRewards = new List<LanReward>();
    readonly Dictionary<string, string> bearerPowerOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> claimedBearerPowerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    TcpListener hostListener;
    Thread acceptThread;
    Thread connectThread;
    PeerConnection serverConnection;

    PlayerMovement localPlayer;
    string localPlayerId;
    string localPlayerName;
    float nextStateSendTime;
    float nextWorldSyncTime;
    float cachedAnimSpeed;
    Vector3 lastLocalPosition;
    bool hasLastLocalPosition;
    volatile bool isShuttingDown;
    int worldSeed;
    bool isApplyingRemoteSceneChange;
    MultiplayerSceneSetState pendingRemoteSceneSet;
    LanWorldState pendingWorldState;
    float nextEnemySyncTime;

    const float StateSendInterval = 0.05f;
    const float WorldSyncInterval = 1f;
    const float EnemySyncInterval = 0.08f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void DetectStartupMode()
    {
        dedicatedProcessRequested = false;
        dedicatedStartupConsumed = false;
        dedicatedStartupPort = 7777;
        dedicatedStartupWorldSeed = null;
        dedicatedStartupScene = null;
        dedicatedStartupSceneSetId = null;

        string[] args;

        try
        {
            args = Environment.GetCommandLineArgs();
        }
        catch
        {
            return;
        }

        if (args == null || args.Length == 0)
            return;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (string.Equals(arg, "-dedicatedServer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-server", StringComparison.OrdinalIgnoreCase))
            {
                dedicatedProcessRequested = true;
                continue;
            }

            if ((string.Equals(arg, "-port", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length &&
                int.TryParse(args[i + 1], out int parsedPort))
            {
                dedicatedStartupPort = Mathf.Clamp(parsedPort, 1, 65535);
                i++;
                continue;
            }

            if ((string.Equals(arg, "-scene", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "--scene", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length)
            {
                dedicatedStartupScene = args[i + 1];
                i++;
                continue;
            }

            if ((string.Equals(arg, "-sceneSet", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "--sceneSet", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length)
            {
                dedicatedStartupSceneSetId = args[i + 1];
                i++;
                continue;
            }

            if ((string.Equals(arg, "-worldSeed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arg, "--worldSeed", StringComparison.OrdinalIgnoreCase)) &&
                i + 1 < args.Length &&
                int.TryParse(args[i + 1], out int parsedSeed))
            {
                dedicatedStartupWorldSeed = parsedSeed;
                i++;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null || FindFirstObjectByType<LanMultiplayerManager>() != null)
            return;

        GameObject managerObject = new GameObject("LanMultiplayerManager");
        managerObject.AddComponent<LanMultiplayerManager>();
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
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        if (dedicatedProcessRequested && !dedicatedStartupConsumed)
        {
            dedicatedStartupConsumed = true;
            StartDedicatedServer(dedicatedStartupPort, dedicatedStartupWorldSeed, dedicatedStartupScene, dedicatedStartupSceneSetId);
        }
    }

    void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
            action?.Invoke();

        FlushPendingEntityUpdates();

        RemoveInvalidRemoteReplicas();
        EnforceSingleSoloPlayer();

        ResolveLocalPlayer();
        TryApplyPendingWorldState();
        TryApplyPendingLocalRewards();
        TryFinalizeRemoteSceneChange();

        if (!IsSessionReady)
            return;

        if (localPlayer != null && Time.unscaledTime >= nextStateSendTime)
        {
            nextStateSendTime = Time.unscaledTime + StateSendInterval;
            SendLocalPlayerState();
        }

        if (IsServerAuthority && Time.unscaledTime >= nextWorldSyncTime)
        {
            nextWorldSyncTime = Time.unscaledTime + WorldSyncInterval;
            BroadcastWorldState();
        }

        if (IsServerAuthority && Time.unscaledTime >= nextEnemySyncTime)
        {
            nextEnemySyncTime = Time.unscaledTime + EnemySyncInterval;
            BroadcastEnemyStates();
        }
    }

    void OnApplicationQuit()
    {
        ShutdownSession();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        ShutdownSession();
    }

    public bool StartSolo(int? savedWorldSeed = null)
    {
        ShutdownSession();
        MultiplayerSceneSetState startupSceneSet = MultiplayerSceneSetCatalog.GetDefaultStartupState();
        if (startupSceneSet != null)
            MultiplayerSceneSetCatalog.ApplyToRuntime(startupSceneSet);

        worldSeed = savedWorldSeed.HasValue && savedWorldSeed.Value != 0
            ? savedWorldSeed.Value
            : Environment.TickCount;
        SessionId = null;
        Mode = SessionMode.Solo;
        State = SessionState.Ready;
        StatusMessage = "Solo";
        LastErrorMessage = null;
        bearerPowerOwners.Clear();
        claimedBearerPowerIds.Clear();
        BearerPowerAvailabilityChanged?.Invoke();
        return true;
    }

    public bool StartHost(int port)
    {
        return StartHost(port, null);
    }

    public MultiplayerSceneSetState CaptureCurrentSceneSet()
    {
        return MultiplayerSceneSetCatalog.CaptureLoadedScenes();
    }

    public bool TravelToSceneSet(string sceneSetId, string fallbackSceneName = null)
    {
        MultiplayerSceneSetState targetSceneSet = MultiplayerSceneSetCatalog.ResolveStartupState(sceneSetId, fallbackSceneName);
        if (targetSceneSet == null)
            return false;

        if (Mode == SessionMode.Client)
        {
            StatusMessage = "A troca de mapa precisa ser feita pelo host.";
            return false;
        }

        if (!MultiplayerSceneSetCatalog.ApplyToRuntime(targetSceneSet))
            return false;

        if (IsServerAuthority && IsMultiplayerActive)
        {
            BroadcastCurrentSceneSet();
            UpdateDiscoveryAnnouncement();
        }

        return true;
    }

    public bool StartDedicatedServer(int port, int? savedWorldSeed = null, string sceneName = null, string sceneSetId = null)
    {
        ShutdownSession();

        try
        {
            Application.runInBackground = true;
            GameState.IsInLobby = false;
            GameState.IsPaused = false;
            GameState.IsInventoryOpen = false;
            GameState.IsPlayerDead = false;
            localPlayer = null;
            localPlayerId = null;
            localPlayerName = BuildPlayerName();
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            worldSeed = savedWorldSeed.HasValue ? savedWorldSeed.Value : Environment.TickCount;
            CurrentPort = Mathf.Max(1, port);
            CurrentAddress = GetLocalIpv4Address();
            Mode = SessionMode.DedicatedServer;
            State = SessionState.Ready;
            LastErrorMessage = null;
            MultiplayerSceneSetState startupSceneSet = MultiplayerSceneSetCatalog.GetDefaultStartupState();
            StatusMessage = $"Servidor dedicado ativo em {CurrentAddress}:{CurrentPort} [{SessionId}]";
            knownStates.Clear();
            bearerPowerOwners.Clear();
            claimedBearerPowerIds.Clear();

            hostListener = new TcpListener(IPAddress.Any, CurrentPort);
            hostListener.Start();

            acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "DedicatedServerAcceptLoop"
            };
            acceptThread.Start();

            if (startupSceneSet != null)
                MultiplayerSceneSetCatalog.ApplyToRuntime(startupSceneSet);

            UpdateDiscoveryAnnouncement();
            BroadcastWorldState();
            return true;
        }
        catch (Exception ex)
        {
            ShutdownSession();
            SetError($"Falha ao iniciar servidor dedicado: {ex.Message}");
            return false;
        }
    }

    public bool StartHost(int port, int? savedWorldSeed)
    {
        ShutdownSession();
        MultiplayerSceneSetState startupSceneSet = MultiplayerSceneSetCatalog.GetDefaultStartupState();
        if (startupSceneSet != null)
            MultiplayerSceneSetCatalog.ApplyToRuntime(startupSceneSet);

        ResolveLocalPlayer();

        try
        {
            localPlayerId = CreatePlayerId();
            localPlayerName = BuildPlayerName();
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            worldSeed = savedWorldSeed.HasValue ? savedWorldSeed.Value : Environment.TickCount;
            CurrentPort = Mathf.Max(1, port);
            CurrentAddress = GetLocalIpv4Address();
            Mode = SessionMode.Host;
            State = SessionState.Ready;
            LastErrorMessage = null;
            StatusMessage = $"Host ativo em {CurrentAddress}:{CurrentPort} [{SessionId}]";
            knownStates.Clear();
            bearerPowerOwners.Clear();
            claimedBearerPowerIds.Clear();
            BearerPowerAvailabilityChanged?.Invoke();

            hostListener = new TcpListener(IPAddress.Any, CurrentPort);
            hostListener.Start();
            UpdateDiscoveryAnnouncement();

            acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "LanHostAcceptLoop"
            };
            acceptThread.Start();

            hasLastLocalPosition = false;
            if (localPlayer != null)
                SendLocalPlayerState();
            BroadcastWorldState();
            return true;
        }
        catch (Exception ex)
        {
            ShutdownSession();
            SetError($"Falha ao iniciar host: {ex.Message}");
            return false;
        }
    }

    public List<LanSavedEntityState> CaptureSavedWorldEntities()
    {
        List<LanSavedEntityState> results = new List<LanSavedEntityState>();
        HashSet<string> liveIds = new HashSet<string>();

        ResourceNode[] resourceNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < resourceNodes.Length; i++)
        {
            LanNetworkEntity entity = resourceNodes[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(resourceNodes[i]);

            if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            liveIds.Add(entity.EntityId);
            results.Add(new LanSavedEntityState
            {
                entityId = entity.EntityId,
                entityKind = nameof(ResourceNode),
                health = resourceNodes[i].IsDepleted ? resourceNodes[i].maxHealth : resourceNodes[i].CurrentHealth,
                destroyed = false
            });
        }

        Cow[] cows = FindObjectsByType<Cow>(FindObjectsSortMode.None);
        for (int i = 0; i < cows.Length; i++)
        {
            LanNetworkEntity entity = cows[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(cows[i]);

            if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            liveIds.Add(entity.EntityId);
            results.Add(new LanSavedEntityState
            {
                entityId = entity.EntityId,
                entityKind = nameof(Cow),
                health = cows[i].CurrentHealth,
                destroyed = false
            });
        }

        WildChicken[] chickens = FindObjectsByType<WildChicken>(FindObjectsSortMode.None);
        for (int i = 0; i < chickens.Length; i++)
        {
            if (chickens[i] == null || chickens[i].IsDead)
                continue;

            LanNetworkEntity entity = chickens[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(chickens[i]);

            if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            liveIds.Add(entity.EntityId);
            results.Add(new LanSavedEntityState
            {
                entityId = entity.EntityId,
                entityKind = nameof(WildChicken),
                health = chickens[i].CurrentHealth,
                destroyed = false
            });
        }

        WildBoar[] boars = FindObjectsByType<WildBoar>(FindObjectsSortMode.None);
        for (int i = 0; i < boars.Length; i++)
        {
            if (boars[i] == null || boars[i].IsDead)
                continue;

            LanNetworkEntity entity = boars[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(boars[i]);

            if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            liveIds.Add(entity.EntityId);
            results.Add(new LanSavedEntityState
            {
                entityId = entity.EntityId,
                entityKind = nameof(WildBoar),
                health = boars[i].CurrentHealth,
                destroyed = false
            });
        }

        EarthGolem[] golems = FindObjectsByType<EarthGolem>(FindObjectsSortMode.None);
        for (int i = 0; i < golems.Length; i++)
        {
            if (golems[i] == null || golems[i].IsDead)
                continue;

            LanNetworkEntity entity = golems[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(golems[i]);

            if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            liveIds.Add(entity.EntityId);
            results.Add(new LanSavedEntityState
            {
                entityId = entity.EntityId,
                entityKind = nameof(EarthGolem),
                health = golems[i].CurrentHealth,
                destroyed = false
            });
        }

        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null || bosses[i].IsPendingDestroy)
                continue;

            LanNetworkEntity entity = bosses[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(bosses[i]);

            if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            liveIds.Add(entity.EntityId);
            results.Add(new LanSavedEntityState
            {
                entityId = entity.EntityId,
                entityKind = nameof(BossEnemy),
                health = bosses[i].CurrentHealth,
                destroyed = false
            });
        }

        foreach (KeyValuePair<string, string> entry in destroyedEntities)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value) || liveIds.Contains(entry.Key))
                continue;

            results.Add(new LanSavedEntityState
            {
                entityId = entry.Key,
                entityKind = entry.Value,
                health = 0,
                destroyed = true
            });
        }

        return results;
    }

    public void RestoreSavedWorldEntities(List<LanSavedEntityState> savedEntities)
    {
        destroyedEntities.Clear();
        pendingEntityUpdates.Clear();

        if (savedEntities == null)
            return;

        for (int i = 0; i < savedEntities.Count; i++)
        {
            LanSavedEntityState state = savedEntities[i];
            if (state == null || string.IsNullOrWhiteSpace(state.entityId) || string.IsNullOrWhiteSpace(state.entityKind))
                continue;

            if (state.destroyed)
                destroyedEntities[state.entityId] = state.entityKind;

            ApplyEntityUpdate(new LanEntityUpdate
            {
                entityId = state.entityId,
                entityKind = state.entityKind,
                health = Mathf.Max(0, state.health),
                destroyed = state.destroyed
            });
        }

        FlushPendingEntityUpdates();
    }

    public bool StartClient(string address, int port)
    {
        ShutdownSession();
        ResolveLocalPlayer();

        localPlayerId = CreatePlayerId();
        localPlayerName = BuildPlayerName();
        SessionId = null;
        CurrentAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        CurrentPort = Mathf.Max(1, port);
        Mode = SessionMode.Client;
        State = SessionState.Connecting;
        LastErrorMessage = null;
        StatusMessage = $"Conectando em {CurrentAddress}:{CurrentPort}...";
        knownStates.Clear();
        bearerPowerOwners.Clear();
        claimedBearerPowerIds.Clear();
        BearerPowerAvailabilityChanged?.Invoke();

        connectThread = new Thread(() => ConnectToHost(CurrentAddress, CurrentPort))
        {
            IsBackground = true,
            Name = "LanClientConnect"
        };
        connectThread.Start();
        return true;
    }

    public static PlayerMovement FindGameplayPlayer()
    {
        if (dedicatedProcessRequested)
            return null;

        if (Instance != null && Instance.Mode == SessionMode.DedicatedServer)
            return null;

        PlayerMovement cameraPlayer = FindMainCameraPlayer();
        if (cameraPlayer != null)
            return cameraPlayer;

        if (Instance != null && Instance.localPlayer != null && !IsReplica(Instance.localPlayer))
            return Instance.localPlayer;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement player in players)
        {
            if (player != null && !IsReplica(player))
                return player;
        }

        PlayerMovement[] inactivePlayers = Resources.FindObjectsOfTypeAll<PlayerMovement>();
        foreach (PlayerMovement player in inactivePlayers)
        {
            if (!IsValidLocalPlayerCandidate(player))
                continue;

            return player;
        }

        return null;
    }

    public static PlayerMovement[] GetGameplayPlayers()
    {
        if (dedicatedProcessRequested)
            return Array.Empty<PlayerMovement>();

        if (Instance != null && Instance.Mode == SessionMode.DedicatedServer)
            return Array.Empty<PlayerMovement>();

        PlayerMovement cameraPlayer = FindMainCameraPlayer();
        if (cameraPlayer != null)
            return new[] { cameraPlayer };

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        List<PlayerMovement> filteredPlayers = new List<PlayerMovement>();

        foreach (PlayerMovement player in players)
        {
            if (player != null && !IsReplica(player))
                filteredPlayers.Add(player);
        }

        PlayerMovement[] inactivePlayers = Resources.FindObjectsOfTypeAll<PlayerMovement>();
        foreach (PlayerMovement player in inactivePlayers)
        {
            if (!IsValidLocalPlayerCandidate(player) || filteredPlayers.Contains(player))
                continue;

            filteredPlayers.Add(player);
        }

        return filteredPlayers.ToArray();
    }

    public static bool IsReplica(Component component)
    {
        return component != null && component.GetComponentInParent<RemotePlayerReplica>() != null;
    }

    static bool IsValidLocalPlayerCandidate(PlayerMovement player)
    {
        if (player == null || IsReplica(player))
            return false;

        GameObject playerObject = player.gameObject;
        if (playerObject == null)
            return false;

        Scene scene = playerObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        if ((playerObject.hideFlags & HideFlags.HideAndDontSave) != 0)
            return false;

        return true;
    }

    static PlayerMovement FindMainCameraPlayer()
    {
        Camera bestCamera = null;
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy ||
                !candidate.CompareTag("MainCamera"))
            {
                continue;
            }

            PlayerMovement candidatePlayer = candidate.GetComponentInParent<PlayerMovement>();
            if (!IsValidLocalPlayerCandidate(candidatePlayer))
                continue;

            if (bestCamera == null || candidate.depth >= bestCamera.depth)
                bestCamera = candidate;
        }

        Camera mainCamera = bestCamera != null ? bestCamera : Camera.main;
        if (mainCamera == null)
            return null;

        PlayerMovement cameraPlayer = mainCamera.GetComponentInParent<PlayerMovement>();
        return IsValidLocalPlayerCandidate(cameraPlayer) ? cameraPlayer : null;
    }

    public static Transform FindWorldFocusTransform()
    {
        if (dedicatedProcessRequested && (Instance == null || Instance.Mode == SessionMode.DedicatedServer))
        {
            if (Instance != null)
            {
                foreach (KeyValuePair<string, Transform> entry in Instance.serverPlayerTargets)
                {
                    if (entry.Value != null)
                        return entry.Value;
                }
            }

            return null;
        }

        PlayerMovement cameraPlayer = FindMainCameraPlayer();
        if (cameraPlayer != null)
            return cameraPlayer.transform;

        if (Instance == null)
        {
            PlayerMovement gameplayPlayer = FindGameplayPlayer();
            return gameplayPlayer != null ? gameplayPlayer.transform : null;
        }

        if (Instance.localPlayer != null && !IsReplica(Instance.localPlayer))
            return Instance.localPlayer.transform;

        foreach (KeyValuePair<string, Transform> entry in Instance.serverPlayerTargets)
        {
            if (entry.Value != null)
                return entry.Value;
        }

        PlayerMovement fallbackPlayer = FindGameplayPlayer();
        return fallbackPlayer != null ? fallbackPlayer.transform : null;
    }

    void ResolveLocalPlayer()
    {
        if (Mode == SessionMode.DedicatedServer)
        {
            localPlayer = null;
            return;
        }

        PlayerMovement cameraPlayer = FindMainCameraPlayer();
        if (cameraPlayer != null && localPlayer != cameraPlayer)
        {
            localPlayer = cameraPlayer;
            lastLocalPosition = localPlayer.transform.position;
            return;
        }

        if (localPlayer != null && !IsReplica(localPlayer))
            return;

        localPlayer = FindGameplayPlayer();
        if (localPlayer != null)
        {
            lastLocalPosition = localPlayer.transform.position;
            hasLastLocalPosition = true;
        }
    }

    void SendLocalPlayerState()
    {
        LanPlayerState state = CaptureLocalPlayerState();
        if (state == null)
            return;

        knownStates[state.playerId] = state;

        if (Mode == SessionMode.Host)
        {
            if (!string.IsNullOrWhiteSpace(state.bearerPowerId) &&
                (!bearerPowerOwners.TryGetValue(state.bearerPowerId, out string ownerId) ||
                 string.Equals(ownerId, state.playerId, StringComparison.Ordinal)))
            {
                RegisterBearerPowerOwner(state.playerId, state.bearerPowerId);
            }

            BroadcastPacket(CreatePacket("state", state));
        }
        else if (Mode == SessionMode.Client && serverConnection != null)
        {
            SendPacket(serverConnection, CreatePacket("state", state));
        }
    }

    LanPlayerState CaptureLocalPlayerState()
    {
        if (localPlayer == null || string.IsNullOrWhiteSpace(localPlayerId))
            return null;

        float animationSpeed = CalculateAnimationSpeed();
        float lookPitch = 0f;

        if (localPlayer.cameraHolder != null)
        {
            float rawPitch = localPlayer.cameraHolder.localEulerAngles.x;
            lookPitch = rawPitch > 180f ? rawPitch - 360f : rawPitch;
        }

        return new LanPlayerState
        {
            playerId = localPlayerId,
            playerName = localPlayerName,
            position = localPlayer.transform.position,
            rotation = localPlayer.transform.rotation,
            lookPitch = lookPitch,
            animationSpeed = animationSpeed,
            thirdPerson = localPlayer.thirdPerson,
            isDead = GameState.IsPlayerDead,
            level = GetLocalPlayerLevel(),
            bearerPowerId = localPlayer.GetComponent<BearerPowerService>()?.CurrentPowerId
        };
    }

    public bool IsBearerPowerClaimed(string powerId)
    {
        if (string.IsNullOrWhiteSpace(powerId))
            return false;

        if (Mode == SessionMode.Client)
            return claimedBearerPowerIds.Contains(powerId);

        return bearerPowerOwners.ContainsKey(powerId);
    }

    public void CaptureBearerPowerOwnership(List<string> powerIds, List<string> playerIds)
    {
        if (powerIds == null || playerIds == null)
            return;

        powerIds.Clear();
        playerIds.Clear();

        foreach (KeyValuePair<string, string> ownership in bearerPowerOwners)
        {
            powerIds.Add(ownership.Key);
            playerIds.Add(ownership.Value);
        }
    }

    public void RestoreBearerPowerOwnership(IReadOnlyList<string> powerIds, IReadOnlyList<string> playerIds)
    {
        if (!IsServerAuthority)
            return;

        bearerPowerOwners.Clear();
        claimedBearerPowerIds.Clear();

        int savedCount = Mathf.Min(powerIds?.Count ?? 0, playerIds?.Count ?? 0);
        for (int i = 0; i < savedCount; i++)
        {
            if (BearerPowerCatalog.Find(powerIds[i]) != null && !string.IsNullOrWhiteSpace(playerIds[i]))
                RegisterBearerPowerOwner(playerIds[i], powerIds[i]);
        }

        ResolveLocalPlayer();
        BearerPowerService localPower = localPlayer != null ? localPlayer.GetComponent<BearerPowerService>() : null;
        if (localPower != null && localPower.HasPower)
        {
            string ownerId = string.IsNullOrWhiteSpace(localPlayerId) ? "host" : localPlayerId;
            if (!bearerPowerOwners.TryGetValue(localPower.CurrentPowerId, out string savedOwner) ||
                string.Equals(savedOwner, ownerId, StringComparison.Ordinal))
            {
                RegisterBearerPowerOwner(ownerId, localPower.CurrentPowerId);
            }
            else
            {
                localPower.LoadState(string.Empty, false);
                MessageSystem.Instance?.ShowMessage("Seu legado pertence a outro portador neste mundo.");
            }
        }

        BroadcastBearerPowerSnapshot();
    }

    public void RequestBearerPowerClaim(string powerId)
    {
        BearerPowerDefinition definition = BearerPowerCatalog.Find(powerId);
        if (definition == null)
        {
            MessageSystem.Instance?.ShowMessage("Legado desconhecido.");
            return;
        }

        ResolveLocalPlayer();
        if (localPlayer == null)
            return;

        if (Mode == SessionMode.Client)
        {
            if (serverConnection == null || !IsSessionReady)
            {
                MessageSystem.Instance?.ShowMessage("Aguardando o mundo responder...");
                return;
            }

            SendPacket(serverConnection, CreatePacket("bearer_power_claim", new LanBearerPowerClaim
            {
                playerId = localPlayerId,
                powerId = definition.stableId
            }));
            MessageSystem.Instance?.ShowMessage($"O legado {definition.displayName} esta respondendo...");
            return;
        }

        string ownerId = string.IsNullOrWhiteSpace(localPlayerId) ? "solo" : localPlayerId;
        ResolveBearerPowerClaim(ownerId, definition.stableId, null);
    }

    public bool TryHandleGameplayHit(Component target, PlayerMovement attacker, ToolType toolType, int damage)
    {
        if (!IsMultiplayerActive || target == null || attacker == null)
            return false;

        LanNetworkEntity entity = ResolveNetworkEntity(target);
        if (entity == null)
            return false;

        string entityKind = GetEntityKind(target);
        if (string.IsNullOrWhiteSpace(entityKind))
            return false;

        if (target is ResourceNode resourceNode && !resourceNode.CanBeHitBy(toolType))
        {
            MessageSystem.Instance?.ShowMessage(currentToolMessage(resourceNode));
            return true;
        }

        if (target is BossEnemy bossEnemy && !bossEnemy.CanBeChallengedBy(attacker))
        {
            MessageSystem.Instance?.ShowMessage(bossEnemy.BuildMinimumLevelMessage());
            return true;
        }

        if (Mode == SessionMode.Client)
        {
            if (serverConnection == null)
                return true;

            SendPacket(serverConnection, CreatePacket("hit_request", new LanHitRequest
            {
                playerId = localPlayerId,
                entityId = entity.EntityId,
                entityKind = entityKind,
                damage = Mathf.Max(1, damage),
                toolType = (int)toolType
            }));
            TriggerClientHitFeedback(target, Mathf.Max(1, damage));
            return true;
        }

        if (Mode == SessionMode.Host)
        {
            ProcessHit(entity.EntityId, entityKind, Mathf.Max(1, damage), toolType, localPlayerId);
            return true;
        }

        return false;
    }

    string currentToolMessage(ResourceNode resourceNode)
    {
        if (resourceNode == null)
            return "Ferramenta inadequada.";

        switch (resourceNode.requiredTool)
        {
            case ToolType.Axe:
                return "Use um machado para madeira.";
            case ToolType.Pickaxe:
                return "Use uma picareta para minerar.";
            default:
                return "Ferramenta inadequada.";
        }
    }

    float CalculateAnimationSpeed()
    {
        if (localPlayer == null)
            return 0f;

        Vector3 currentPosition = localPlayer.transform.position;
        if (!hasLastLocalPosition)
        {
            lastLocalPosition = currentPosition;
            hasLastLocalPosition = true;
            return 0f;
        }

        float distance = Vector3.Distance(currentPosition, lastLocalPosition);
        float speed = Time.unscaledDeltaTime > 0.0001f ? distance / Time.unscaledDeltaTime : 0f;
        float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.01f, localPlayer.runSpeed));
        cachedAnimSpeed = Mathf.Lerp(cachedAnimSpeed, normalizedSpeed, 0.4f);
        lastLocalPosition = currentPosition;
        return cachedAnimSpeed;
    }

    void BroadcastWorldState()
    {
        DayNightCycle dayNightCycle = DayNightCycle.Instance;
        if (dayNightCycle == null)
            return;

        LanWorldState state = new LanWorldState
        {
            currentDay = dayNightCycle.CurrentDay,
            normalizedTimeOfDay = dayNightCycle.CurrentNormalizedTime
        };

        BroadcastPacket(CreatePacket("world", state));
    }

    void ApplyWorldState(LanWorldState state)
    {
        if (Mode != SessionMode.Client || state == null)
            return;

        if (DayNightCycle.Instance == null)
        {
            pendingWorldState = state;
            return;
        }

        DayNightCycle.Instance.LoadState(state.currentDay, state.normalizedTimeOfDay);
        pendingWorldState = null;
    }

    void AcceptLoop()
    {
        while (!isShuttingDown)
        {
            try
            {
                TcpClient client = hostListener.AcceptTcpClient();
                client.NoDelay = true;

                PeerConnection connection = CreateConnection(client);
                EnqueueMainThread(() =>
                {
                    hostPeers[connection.addressLabel] = connection;
                    StartReadLoop(connection, true);
                });
            }
            catch (SocketException)
            {
                if (isShuttingDown)
                    return;
            }
            catch (Exception ex)
            {
                if (isShuttingDown)
                    return;

                EnqueueMainThread(() => SetError($"Erro aceitando conexao: {ex.Message}"));
                return;
            }
        }
    }

    void ConnectToHost(string address, int port)
    {
        TcpClient client = new TcpClient();

        try
        {
            IAsyncResult result = client.BeginConnect(address, port, null, null);
            bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
            if (!connected || !client.Connected)
                throw new IOException($"Tempo esgotado ao conectar em {address}:{port}. Verifique se o IP esta correto, se o host clicou em Hospedar e se a porta nao esta bloqueada.");

            client.EndConnect(result);
            client.NoDelay = true;

            PeerConnection connection = CreateConnection(client);
            EnqueueMainThread(() =>
            {
                serverConnection = connection;
                StartReadLoop(connection, false);
                SendPacket(connection, CreatePacket("hello", new LanHandshake
                {
                    playerId = localPlayerId,
                    playerName = localPlayerName
                }));
            });
        }
        catch (Exception ex)
        {
            try
            {
                client.Close();
            }
            catch
            {
            }

            string connectionError = BuildConnectionErrorMessage(address, port, ex);
            EnqueueMainThread(() => SetError(connectionError));
        }
    }

    PeerConnection CreateConnection(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        return new PeerConnection
        {
            client = client,
            reader = new StreamReader(stream),
            writer = new StreamWriter(stream) { AutoFlush = true },
            addressLabel = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString("N")
        };
    }

    void StartReadLoop(PeerConnection connection, bool isHostSide)
    {
        connection.readThread = new Thread(() => ReadLoop(connection, isHostSide))
        {
            IsBackground = true,
            Name = isHostSide ? "LanHostReadLoop" : "LanClientReadLoop"
        };
        connection.readThread.Start();
    }

    void ReadLoop(PeerConnection connection, bool isHostSide)
    {
        try
        {
            while (!isShuttingDown && connection.client != null && connection.client.Connected)
            {
                string line = connection.reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    break;

                EnqueueMainThread(() => ProcessPacket(connection, line, isHostSide));
            }
        }
        catch
        {
        }
        finally
        {
            EnqueueMainThread(() => HandleDisconnect(connection, isHostSide));
        }
    }

    void ProcessPacket(PeerConnection connection, string json, bool isHostSide)
    {
        LanPacket packet;

        try
        {
            packet = JsonUtility.FromJson<LanPacket>(json);
        }
        catch
        {
            return;
        }

        if (packet == null || string.IsNullOrWhiteSpace(packet.type))
            return;

        switch (packet.type)
        {
            case "hello":
                if (isHostSide)
                    HandleHelloPacket(connection, packet.payload);
                break;

            case "welcome":
                if (!isHostSide)
                    HandleWelcomePacket(packet.payload);
                break;

            case "state":
                HandleStatePacket(connection, packet.payload, isHostSide);
                break;

            case "leave":
                HandleLeavePacket(packet.payload);
                break;

            case "world":
                if (!isHostSide)
                    ApplyWorldState(JsonUtility.FromJson<LanWorldState>(packet.payload));
                break;

            case "enemy_state":
                if (!isHostSide)
                    ApplyEnemyState(JsonUtility.FromJson<LanEnemyState>(packet.payload));
                break;

            case "scene":
                if (!isHostSide)
                    ApplySceneChange(JsonUtility.FromJson<LanSceneChange>(packet.payload));
                break;

            case "hit_request":
                if (isHostSide)
                    HandleHitRequest(packet.payload);
                break;

            case "entity_update":
                if (!isHostSide)
                    ApplyEntityUpdate(JsonUtility.FromJson<LanEntityUpdate>(packet.payload));
                break;

            case "reward":
                if (!isHostSide)
                    ApplyRewardLocally(JsonUtility.FromJson<LanReward>(packet.payload));
                break;

            case "damage":
                if (!isHostSide)
                    ApplyDamageLocally(JsonUtility.FromJson<LanDamageEvent>(packet.payload));
                break;

            case "bearer_power_claim":
                if (isHostSide)
                    HandleBearerPowerClaim(connection, packet.payload);
                break;

            case "bearer_power_result":
                if (!isHostSide)
                    ApplyBearerPowerClaimResult(JsonUtility.FromJson<LanBearerPowerClaimResult>(packet.payload));
                break;

            case "bearer_power_snapshot":
                if (!isHostSide)
                    ApplyBearerPowerSnapshot(JsonUtility.FromJson<LanBearerPowerSnapshot>(packet.payload));
                break;
        }
    }

    void HandleHelloPacket(PeerConnection connection, string payload)
    {
        LanHandshake handshake = JsonUtility.FromJson<LanHandshake>(payload);
        if (handshake == null || string.IsNullOrWhiteSpace(handshake.playerId))
            return;

        connection.playerId = handshake.playerId;
        connection.playerName = string.IsNullOrWhiteSpace(handshake.playerName) ? $"Player {hostPeers.Count + 1}" : handshake.playerName.Trim();
        hostPeers[connection.playerId] = connection;
        hostPeers.Remove(connection.addressLabel);
        MultiplayerSceneSetState currentSceneSet = CaptureCurrentSceneSet();

        SendPacket(connection, CreatePacket("welcome", new LanWelcome
        {
            playerId = connection.playerId,
            worldSeed = worldSeed,
            sceneName = SceneManager.GetActiveScene().name,
            sceneSetId = currentSceneSet?.sceneSetId,
            activeSceneName = currentSceneSet?.activeSceneName,
            sceneNames = currentSceneSet?.sceneNames
        }));

        foreach (KeyValuePair<string, LanPlayerState> state in knownStates)
            SendPacket(connection, CreatePacket("state", state.Value));

        SendBearerPowerSnapshot(connection);
        SyncWorldEntitiesTo(connection);
        SyncEnemyStatesTo(connection);
        BroadcastWorldState();
        StatusMessage = $"{connection.playerName} entrou na sessao";
        UpdateDiscoveryAnnouncement();
    }

    void HandleWelcomePacket(string payload)
    {
        LanWelcome welcome = JsonUtility.FromJson<LanWelcome>(payload);
        if (welcome == null)
            return;

        worldSeed = welcome.worldSeed;
        State = SessionState.Ready;
        StatusMessage = $"Conectado em {CurrentAddress}:{CurrentPort}";
        hasLastLocalPosition = false;
        ClearRemoteReplicas();
        ApplySceneChange(new LanSceneChange
        {
            sceneName = welcome.sceneName,
            sceneSetId = welcome.sceneSetId,
            activeSceneName = welcome.activeSceneName,
            sceneNames = welcome.sceneNames
        });
    }

    void ApplySceneChange(LanSceneChange sceneChange)
    {
        if (sceneChange == null)
            return;

        MultiplayerSceneSetState targetSceneSet = MultiplayerSceneSetCatalog.Normalize(new MultiplayerSceneSetState
        {
            sceneSetId = sceneChange.sceneSetId,
            activeSceneName = string.IsNullOrWhiteSpace(sceneChange.activeSceneName) ? sceneChange.sceneName : sceneChange.activeSceneName,
            sceneNames = sceneChange.sceneNames != null && sceneChange.sceneNames.Length > 0
                ? sceneChange.sceneNames
                : (string.IsNullOrWhiteSpace(sceneChange.sceneName) ? null : new[] { sceneChange.sceneName })
        });

        if (targetSceneSet == null)
            return;

        if (MultiplayerSceneSetCatalog.LoadedScenesMatch(targetSceneSet))
        {
            Scene targetActiveScene = SceneManager.GetSceneByName(targetSceneSet.activeSceneName);
            if (targetActiveScene.IsValid() && targetActiveScene.isLoaded && SceneManager.GetActiveScene().name != targetSceneSet.activeSceneName)
                SceneManager.SetActiveScene(targetActiveScene);
            return;
        }

        isApplyingRemoteSceneChange = true;
        pendingRemoteSceneSet = targetSceneSet;
        StatusMessage = $"Carregando pacote {MultiplayerSceneSetCatalog.BuildDisplayLabel(targetSceneSet)}...";
        MultiplayerSceneSetCatalog.ApplyToRuntime(targetSceneSet);
        TryFinalizeRemoteSceneChange();
    }

    void HandleHitRequest(string payload)
    {
        LanHitRequest request = JsonUtility.FromJson<LanHitRequest>(payload);
        if (request == null || string.IsNullOrWhiteSpace(request.entityId) || string.IsNullOrWhiteSpace(request.playerId))
            return;

        ProcessHit(request.entityId, request.entityKind, Mathf.Max(1, request.damage), (ToolType)request.toolType, request.playerId);
    }

    void ProcessHit(string entityId, string entityKind, int damage, ToolType toolType, string attackerPlayerId)
    {
        PlayerMovement attacker = ResolveAttacker(attackerPlayerId);
        if (attacker == null && attackerPlayerId == localPlayerId)
            attacker = localPlayer;

        if (entityKind == nameof(MiniKrug))
        {
            MiniKrug miniKrug = FindEntity<MiniKrug>(entityId);
            if (miniKrug == null)
                return;

            miniKrug.ApplyNetworkHit(damage, out int goldAmount, out int xpAmount, out int remainingHealth, out bool destroyed);
            BroadcastPacket(CreatePacket("enemy_state", new LanEnemyState
            {
                entityId = entityId,
                entityKind = entityKind,
                position = miniKrug != null ? miniKrug.transform.position : Vector3.zero,
                rotation = miniKrug != null ? miniKrug.transform.rotation : Quaternion.identity,
                level = miniKrug != null ? miniKrug.EnemyLevel : 1,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (goldAmount > 0 || xpAmount > 0)
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    goldAmount = goldAmount,
                    xpAmount = xpAmount,
                    bestiaryCreatureId = destroyed ? BestiaryDatabase.MiniKrugId : null,
                    message = $"+{goldAmount} gold"
                });

            return;
        }

        if (entityKind == nameof(ResourceNode))
        {
            ResourceNode node = FindEntity<ResourceNode>(entityId);
            if (node == null)
                return;

            if (!node.TryHitForReward(toolType, damage, out string rewardItemName, out string rewardPrefabName, out int rewardAmount, out int remainingHealth, out bool destroyed))
                return;

            BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entityId,
                entityKind = entityKind,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (destroyed)
                destroyedEntities[entityId] = entityKind;
            else
                destroyedEntities.Remove(entityId);

            if (rewardAmount > 0)
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    itemName = rewardItemName,
                    prefabName = rewardPrefabName,
                    itemAmount = rewardAmount,
                    bestiaryCreatureId = destroyed ? BestiaryDatabase.CowId : null,
                    message = $"+{rewardAmount} {rewardItemName}"
                });

            return;
        }

        if (entityKind == nameof(Cow))
        {
            Cow cow = FindEntity<Cow>(entityId);
            if (cow == null)
                return;

            cow.ApplyNetworkHit(
                damage,
                out int meatAmount,
                out string meatItemName,
                out string meatPrefabName,
                out int leatherAmount,
                out string leatherItemName,
                out string leatherPrefabName,
                out int remainingHealth,
                out bool destroyed);
            BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entityId,
                entityKind = entityKind,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (destroyed)
                destroyedEntities[entityId] = entityKind;
            else
                destroyedEntities.Remove(entityId);

            bool bestiarySent = false;
            if (meatAmount > 0)
            {
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    itemName = meatItemName,
                    prefabName = meatPrefabName,
                    itemAmount = meatAmount,
                    bestiaryCreatureId = destroyed ? BestiaryDatabase.CowId : null,
                    message = $"+{meatAmount} {meatItemName}"
                });

                bestiarySent = destroyed;
            }

            if (leatherAmount > 0)
            {
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    itemName = leatherItemName,
                    prefabName = leatherPrefabName,
                    itemAmount = leatherAmount,
                    bestiaryCreatureId = destroyed && !bestiarySent ? BestiaryDatabase.CowId : null,
                    message = $"+{leatherAmount} {leatherItemName}"
                });

                bestiarySent = bestiarySent || destroyed;
            }

            if (destroyed && !bestiarySent)
            {
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    bestiaryCreatureId = BestiaryDatabase.CowId
                });
            }

            return;
        }

        if (entityKind == nameof(WildChicken))
        {
            WildChicken chicken = FindEntity<WildChicken>(entityId);
            if (chicken == null)
                return;

            chicken.ApplyNetworkHit(damage, out int featherAmount, out int rawMeatAmount, out int remainingHealth, out bool destroyed);
            BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entityId,
                entityKind = entityKind,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (destroyed)
                destroyedEntities[entityId] = entityKind;
            else
                destroyedEntities.Remove(entityId);

            if (featherAmount > 0)
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    itemName = FeatherItemRegistry.ItemName,
                    itemAmount = featherAmount,
                    bestiaryCreatureId = destroyed ? BestiaryDatabase.WildChickenId : null,
                    message = $"+{featherAmount} {FeatherItemRegistry.ItemName}"
                });

            if (rawMeatAmount > 0)
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    itemName = RawChickenMeatItemRegistry.ItemName,
                    itemAmount = rawMeatAmount,
                    message = $"+{rawMeatAmount} {RawChickenMeatItemRegistry.ItemName}"
                });

            return;
        }

        if (entityKind == nameof(WildBoar))
        {
            WildBoar boar = FindEntity<WildBoar>(entityId);
            if (boar == null)
                return;

            boar.ApplyNetworkHit(damage, out int leatherAmount, out int tuskAmount, out int meatAmount, out int trophyAmount, out int remainingHealth, out bool destroyed);
            BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entityId,
                entityKind = entityKind,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (destroyed)
                destroyedEntities[entityId] = entityKind;
            else
                destroyedEntities.Remove(entityId);

            bool bestiarySent = false;
            GrantBoarReward(attackerPlayerId, ThickLeatherItemRegistry.ItemName, leatherAmount, destroyed, ref bestiarySent);
            GrantBoarReward(attackerPlayerId, SharpTuskItemRegistry.ItemName, tuskAmount, destroyed, ref bestiarySent);
            GrantBoarReward(attackerPlayerId, BoarMeatItemRegistry.ItemName, meatAmount, destroyed, ref bestiarySent);
            GrantBoarReward(attackerPlayerId, BoarTrophyItemRegistry.ItemName, trophyAmount, destroyed, ref bestiarySent);

            if (destroyed && !bestiarySent)
            {
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    bestiaryCreatureId = BestiaryDatabase.WildBoarId
                });
            }

            return;
        }

        if (entityKind == nameof(EarthGolem))
        {
            EarthGolem golem = FindEntity<EarthGolem>(entityId);
            if (golem == null)
                return;

            golem.ApplyNetworkHit(damage, out int stoneAmount, out int mossAmount, out int ironAmount, out int coreAmount, out int remainingHealth, out bool destroyed);
            BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entityId,
                entityKind = entityKind,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (destroyed)
                destroyedEntities[entityId] = entityKind;
            else
                destroyedEntities.Remove(entityId);

            bool bestiarySent = false;
            GrantGolemReward(attackerPlayerId, StoneFragmentItemRegistry.ItemName, stoneAmount, destroyed, ref bestiarySent);
            GrantGolemReward(attackerPlayerId, ResilientMossItemRegistry.ItemName, mossAmount, destroyed, ref bestiarySent);
            GrantGolemReward(attackerPlayerId, IronItemRegistry.ItemName, ironAmount, destroyed, ref bestiarySent);
            GrantGolemReward(attackerPlayerId, EarthCoreItemRegistry.ItemName, coreAmount, destroyed, ref bestiarySent);

            if (destroyed && !bestiarySent)
            {
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    bestiaryCreatureId = BestiaryDatabase.EarthGolemId
                });
            }

            return;
        }

        if (entityKind == nameof(BossEnemy))
        {
            BossEnemy boss = FindEntity<BossEnemy>(entityId);
            if (boss == null)
                return;

            int attackerLevel = GetPlayerLevel(attackerPlayerId);
            if (!boss.CanBeChallengedByLevel(attackerLevel))
            {
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    message = boss.BuildMinimumLevelMessage()
                });
                return;
            }

            boss.ApplyNetworkHit(damage, out int goldAmount, out int xpAmount, out bool unlockMagic, out int remainingHealth, out bool destroyed);
            BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entityId,
                entityKind = entityKind,
                health = remainingHealth,
                destroyed = destroyed
            }));

            if (destroyed)
                destroyedEntities[entityId] = entityKind;
            else
                destroyedEntities.Remove(entityId);

            if (goldAmount > 0 || xpAmount > 0 || unlockMagic)
                GrantReward(attackerPlayerId, new LanReward
                {
                    playerId = attackerPlayerId,
                    goldAmount = goldAmount,
                    xpAmount = xpAmount,
                    unlockAreaMagic = unlockMagic,
                    bestiaryCreatureId = destroyed ? BestiaryDatabase.BossEnemyId : null,
                    message = unlockMagic ? "Magia ancestral desbloqueada!" : $"+{goldAmount} gold"
                });
        }
    }

    void GrantBoarReward(string attackerPlayerId, string itemName, int amount, bool destroyed, ref bool bestiarySent)
    {
        if (amount <= 0)
            return;

        GrantReward(attackerPlayerId, new LanReward
        {
            playerId = attackerPlayerId,
            itemName = itemName,
            itemAmount = amount,
            bestiaryCreatureId = destroyed && !bestiarySent ? BestiaryDatabase.WildBoarId : null,
            message = $"+{amount} {itemName}"
        });

        if (destroyed)
            bestiarySent = true;
    }

    void GrantGolemReward(string attackerPlayerId, string itemName, int amount, bool destroyed, ref bool bestiarySent)
    {
        if (amount <= 0)
            return;

        GrantReward(attackerPlayerId, new LanReward
        {
            playerId = attackerPlayerId,
            itemName = itemName,
            itemAmount = amount,
            bestiaryCreatureId = destroyed && !bestiarySent ? BestiaryDatabase.EarthGolemId : null,
            message = $"+{amount} {itemName}"
        });

        if (destroyed)
            bestiarySent = true;
    }

    void ApplyEntityUpdate(LanEntityUpdate update)
    {
        if (update == null || string.IsNullOrWhiteSpace(update.entityId) || string.IsNullOrWhiteSpace(update.entityKind))
            return;

        if (update.entityKind == nameof(ResourceNode))
        {
            ResourceNode node = FindEntity<ResourceNode>(update.entityId);
            if (node != null)
                node.ApplyNetworkState(update.health, update.destroyed);
            else
                pendingEntityUpdates[update.entityId] = update;
            return;
        }

        if (update.entityKind == nameof(Cow))
        {
            Cow cow = FindEntity<Cow>(update.entityId);
            if (cow != null)
                cow.ApplyNetworkState(update.health, update.destroyed);
            else
                pendingEntityUpdates[update.entityId] = update;
            return;
        }

        if (update.entityKind == nameof(WildChicken))
        {
            WildChicken chicken = FindEntity<WildChicken>(update.entityId);
            if (chicken != null)
                chicken.ApplyNetworkState(update.health, update.destroyed);
            else
                pendingEntityUpdates[update.entityId] = update;
            return;
        }

        if (update.entityKind == nameof(WildBoar))
        {
            WildBoar boar = FindEntity<WildBoar>(update.entityId);
            if (boar != null)
                boar.ApplyNetworkState(update.health, update.destroyed);
            else
                pendingEntityUpdates[update.entityId] = update;
            return;
        }

        if (update.entityKind == nameof(EarthGolem))
        {
            EarthGolem golem = FindEntity<EarthGolem>(update.entityId);
            if (golem != null)
                golem.ApplyNetworkState(update.health, update.destroyed);
            else
                pendingEntityUpdates[update.entityId] = update;
            return;
        }

        if (update.entityKind == nameof(BossEnemy))
        {
            BossEnemy boss = FindEntity<BossEnemy>(update.entityId);
            if (boss != null)
                boss.ApplyNetworkState(update.health, update.destroyed);
            else
                pendingEntityUpdates[update.entityId] = update;
        }
    }

    void ApplyEnemyState(LanEnemyState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.entityId) || string.IsNullOrWhiteSpace(state.entityKind))
            return;

        if (state.entityKind == nameof(MiniKrug))
        {
            MiniKrug miniKrug = FindEntity<MiniKrug>(state.entityId);
            if (miniKrug == null && !state.destroyed)
                miniKrug = CreateRemoteMiniKrug(state);

            if (miniKrug != null)
                miniKrug.ApplyNetworkState(state.position, state.rotation, state.level, state.health, state.destroyed);

            return;
        }

        if (state.entityKind == nameof(BossEnemy))
        {
            BossEnemy boss = FindEntity<BossEnemy>(state.entityId);
            if (boss == null && !state.destroyed)
                boss = CreateRemoteBossEnemy(state);

            if (boss != null)
                boss.ApplyNetworkState(state.position, state.rotation, state.level, state.health, state.destroyed);
        }
    }

    void ApplyRewardLocally(LanReward reward)
    {
        if (reward == null || reward.playerId != localPlayerId)
            return;

        ResolveLocalPlayer();
        if (localPlayer == null)
        {
            QueueLocalReward(reward);
            return;
        }

        Inventory inventory = localPlayer.GetComponent<Inventory>();
        Hotbar hotbar = localPlayer.GetComponent<Hotbar>();
        PlayerProgression progression = localPlayer.GetComponent<PlayerProgression>() ?? localPlayer.gameObject.AddComponent<PlayerProgression>();
        PlayerMagic magic = localPlayer.GetComponent<PlayerMagic>();

        bool needsInventory = (reward.itemAmount > 0 || reward.goldAmount > 0) && inventory == null;
        if (needsInventory)
        {
            QueueLocalReward(reward);
            return;
        }

        if (reward.itemAmount > 0 && inventory != null)
        {
            Item item = ResolveItem(reward.itemName, reward.prefabName);
            if (item != null)
            {
                inventory.AddItem(reward.itemName, reward.itemAmount, item);
                if (hotbar != null && (item.itemType == ItemType.Tool || item.itemType == ItemType.Consumable))
                    hotbar.AddInventoryItem(new InventoryItem(reward.itemName, reward.itemAmount, item));
            }
        }

        if (reward.goldAmount > 0 && inventory != null)
            inventory.AddItem("Gold", reward.goldAmount, GoldItemRegistry.GetOrCreate());

        if (reward.xpAmount > 0 && progression != null)
            progression.AddExperience(reward.xpAmount);

        if (reward.unlockAreaMagic)
        {
            if (magic == null && localPlayer != null)
                magic = localPlayer.GetComponent<PlayerMagic>() ?? localPlayer.gameObject.AddComponent<PlayerMagic>();

            magic?.UnlockAreaMagic();
        }

        if (!string.IsNullOrWhiteSpace(reward.bestiaryCreatureId))
            BestiaryService.Instance?.RecordDefeat(reward.bestiaryCreatureId);

        if (!string.IsNullOrWhiteSpace(reward.message))
            MessageSystem.Instance?.ShowMessage(reward.message);

        RefreshClientHud();
    }

    void ApplyDamageLocally(LanDamageEvent damageEvent)
    {
        if (damageEvent == null || damageEvent.playerId != localPlayerId)
            return;

        ResolveLocalPlayer();
        localPlayer?.TakeDamage(damageEvent.damage);
    }

    void HandleStatePacket(PeerConnection connection, string payload, bool isHostSide)
    {
        LanPlayerState state = JsonUtility.FromJson<LanPlayerState>(payload);
        if (state == null || string.IsNullOrWhiteSpace(state.playerId))
            return;

        if (isHostSide && !string.IsNullOrWhiteSpace(connection.playerId))
            state.playerId = connection.playerId;

        if (isHostSide && !string.IsNullOrWhiteSpace(state.bearerPowerId))
        {
            if (!bearerPowerOwners.TryGetValue(state.bearerPowerId, out string ownerId))
            {
                RegisterBearerPowerOwner(state.playerId, state.bearerPowerId);
            }
            else if (!string.Equals(ownerId, state.playerId, StringComparison.Ordinal))
            {
                state.bearerPowerId = string.Empty;
                SendBearerPowerClaimResult(
                    connection,
                    state.playerId,
                    string.Empty,
                    false,
                    "Seu legado ja possui outro portador neste mundo.");
            }
        }

        knownStates[state.playerId] = state;
        UpsertServerPlayerTarget(state);

        if (state.playerId != localPlayerId)
            UpsertReplica(state);

        if (isHostSide)
            BroadcastPacket(CreatePacket("state", state), connection.playerId);
    }

    void HandleBearerPowerClaim(PeerConnection connection, string payload)
    {
        LanBearerPowerClaim claim = JsonUtility.FromJson<LanBearerPowerClaim>(payload);
        if (claim == null || connection == null)
            return;

        string playerId = !string.IsNullOrWhiteSpace(connection.playerId)
            ? connection.playerId
            : claim.playerId;
        ResolveBearerPowerClaim(playerId, claim.powerId, connection);
    }

    void ResolveBearerPowerClaim(string playerId, string powerId, PeerConnection requestingConnection)
    {
        BearerPowerDefinition definition = BearerPowerCatalog.Find(powerId);
        if (definition == null || string.IsNullOrWhiteSpace(playerId))
        {
            SendBearerPowerClaimResult(requestingConnection, playerId, powerId, false, "Este legado nao reconheceu o portador.");
            return;
        }

        foreach (KeyValuePair<string, string> ownership in bearerPowerOwners)
        {
            if (string.Equals(ownership.Value, playerId, StringComparison.Ordinal) &&
                !string.Equals(ownership.Key, definition.stableId, StringComparison.OrdinalIgnoreCase))
            {
                SendBearerPowerClaimResult(requestingConnection, playerId, definition.stableId, false, "Cada portador pode carregar apenas um legado.");
                return;
            }
        }

        if (bearerPowerOwners.TryGetValue(definition.stableId, out string currentOwner) &&
            !string.Equals(currentOwner, playerId, StringComparison.Ordinal))
        {
            SendBearerPowerClaimResult(requestingConnection, playerId, definition.stableId, false, $"{definition.displayName} ja escolheu outro portador.");
            return;
        }

        RegisterBearerPowerOwner(playerId, definition.stableId);

        if (requestingConnection != null)
        {
            SendBearerPowerClaimResult(requestingConnection, playerId, definition.stableId, true, $"Voce absorveu {definition.displayName}.");
        }
        else
        {
            ResolveLocalPlayer();
            BearerPowerService service = localPlayer != null
                ? localPlayer.GetComponent<BearerPowerService>() ?? localPlayer.gameObject.AddComponent<BearerPowerService>()
                : null;
            service?.AcceptPower(definition.stableId);
        }

        BroadcastBearerPowerSnapshot();
    }

    void RegisterBearerPowerOwner(string playerId, string powerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || BearerPowerCatalog.Find(powerId) == null)
            return;

        if (bearerPowerOwners.TryGetValue(powerId, out string existingOwner) &&
            string.Equals(existingOwner, playerId, StringComparison.Ordinal))
        {
            claimedBearerPowerIds.Add(powerId);
            return;
        }

        bearerPowerOwners[powerId] = playerId;
        claimedBearerPowerIds.Add(powerId);
        BearerPowerAvailabilityChanged?.Invoke();
    }

    void SendBearerPowerClaimResult(PeerConnection connection, string playerId, string powerId, bool accepted, string message)
    {
        LanBearerPowerClaimResult result = new LanBearerPowerClaimResult
        {
            playerId = playerId,
            powerId = powerId,
            accepted = accepted,
            message = message,
            claimedPowerIds = BuildClaimedBearerPowerIds()
        };

        if (connection != null)
        {
            SendPacket(connection, CreatePacket("bearer_power_result", result));
            return;
        }

        ApplyBearerPowerClaimResult(result);
    }

    void ApplyBearerPowerClaimResult(LanBearerPowerClaimResult result)
    {
        if (result == null)
            return;

        ReplaceClaimedBearerPowers(result.claimedPowerIds);

        if (!string.IsNullOrWhiteSpace(result.message))
            MessageSystem.Instance?.ShowMessage(result.message);

        if (result.playerId != localPlayerId)
            return;

        ResolveLocalPlayer();
        BearerPowerService service = localPlayer != null
            ? localPlayer.GetComponent<BearerPowerService>() ?? localPlayer.gameObject.AddComponent<BearerPowerService>()
            : null;

        if (!result.accepted)
        {
            if (service != null && service.HasPower && string.IsNullOrWhiteSpace(result.powerId))
                service.LoadState(string.Empty, false);
            return;
        }

        service?.AcceptPower(result.powerId);
    }

    void SendBearerPowerSnapshot(PeerConnection connection)
    {
        if (connection == null)
            return;

        SendPacket(connection, CreatePacket("bearer_power_snapshot", new LanBearerPowerSnapshot
        {
            claimedPowerIds = BuildClaimedBearerPowerIds()
        }));
    }

    void BroadcastBearerPowerSnapshot()
    {
        LanBearerPowerSnapshot snapshot = new LanBearerPowerSnapshot
        {
            claimedPowerIds = BuildClaimedBearerPowerIds()
        };
        BroadcastPacket(CreatePacket("bearer_power_snapshot", snapshot));
        ApplyBearerPowerSnapshot(snapshot);
    }

    void ApplyBearerPowerSnapshot(LanBearerPowerSnapshot snapshot)
    {
        ReplaceClaimedBearerPowers(snapshot?.claimedPowerIds);
    }

    string[] BuildClaimedBearerPowerIds()
    {
        string[] ids = new string[bearerPowerOwners.Count];
        int index = 0;
        foreach (string powerId in bearerPowerOwners.Keys)
            ids[index++] = powerId;
        return ids;
    }

    void ReplaceClaimedBearerPowers(string[] powerIds)
    {
        claimedBearerPowerIds.Clear();
        if (powerIds != null)
        {
            for (int i = 0; i < powerIds.Length; i++)
            {
                if (BearerPowerCatalog.Find(powerIds[i]) != null)
                    claimedBearerPowerIds.Add(powerIds[i]);
            }
        }

        BearerPowerAvailabilityChanged?.Invoke();
    }

    void HandleLeavePacket(string payload)
    {
        LanLeave leave = JsonUtility.FromJson<LanLeave>(payload);
        if (leave == null || string.IsNullOrWhiteSpace(leave.playerId))
            return;

        RemoveReplica(leave.playerId);
        RemoveServerPlayerTarget(leave.playerId);
        knownStates.Remove(leave.playerId);
    }

    void UpsertReplica(LanPlayerState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.playerId) || state.playerId == localPlayerId)
            return;

        if (!ShouldKeepRemoteReplica(state.playerId))
        {
            Debug.LogWarning($"Replica remota bloqueada: {state.playerId}. Modo={Mode}, Sessao={State}.");
            RemoveReplica(state.playerId);
            return;
        }

        if (localPlayer == null)
            ResolveLocalPlayer();

        if (localPlayer == null)
            return;

        if (!remoteReplicas.TryGetValue(state.playerId, out RemotePlayerReplica replica) || replica == null)
        {
            replica = RemotePlayerReplica.CreateFromPlayer(localPlayer, state.playerId, state.playerName);
            if (replica == null)
                return;

            remoteReplicas[state.playerId] = replica;
        }

        replica.ApplyState(state);
    }

    public bool ShouldKeepRemoteReplica(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || playerId == localPlayerId || !IsSessionReady)
            return false;

        return Mode switch
        {
            SessionMode.Client => true,
            SessionMode.Host => hostPeers.ContainsKey(playerId),
            _ => false
        };
    }

    void RemoveInvalidRemoteReplicas()
    {
        List<string> staleIds = null;
        foreach (KeyValuePair<string, RemotePlayerReplica> entry in remoteReplicas)
        {
            if (entry.Value != null && ShouldKeepRemoteReplica(entry.Key))
                continue;

            staleIds ??= new List<string>();
            staleIds.Add(entry.Key);
        }

        if (staleIds != null)
        {
            for (int i = 0; i < staleIds.Count; i++)
                RemoveReplica(staleIds[i]);
        }

        RemotePlayerReplica[] looseReplicas = FindObjectsByType<RemotePlayerReplica>(FindObjectsSortMode.None);
        for (int i = 0; i < looseReplicas.Length; i++)
        {
            RemotePlayerReplica replica = looseReplicas[i];
            if (replica == null || ShouldKeepRemoteReplica(replica.PlayerId))
                continue;

            if (!string.IsNullOrWhiteSpace(replica.PlayerId))
                remoteReplicas.Remove(replica.PlayerId);

            Debug.LogWarning($"Replica remota solta removida: {replica.PlayerId}. Modo={Mode}, Sessao={State}.");
            Destroy(replica.gameObject);
        }
    }

    void EnforceSingleSoloPlayer()
    {
        if (GameState.IsInLobby || Mode == SessionMode.DedicatedServer)
            return;

        PlayerMovement authoritativePlayer = FindMainCameraPlayer();
        if (authoritativePlayer == null && localPlayer != null && !IsReplica(localPlayer))
            authoritativePlayer = localPlayer;

        if (authoritativePlayer == null)
            return;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement candidate = players[i];
            if (candidate == null || candidate == authoritativePlayer || IsReplica(candidate))
                continue;

            Debug.LogWarning(
                $"Player duplicado removido: {candidate.name} em {candidate.transform.position}. " +
                $"Player mantido: {authoritativePlayer.name} em {authoritativePlayer.transform.position}."
            );
            Destroy(candidate.gameObject);
        }

        localPlayer = authoritativePlayer;
    }

    void RemoveReplica(string playerId)
    {
        if (!remoteReplicas.TryGetValue(playerId, out RemotePlayerReplica replica))
            return;

        remoteReplicas.Remove(playerId);

        if (replica != null)
            Destroy(replica.gameObject);
    }

    void UpsertServerPlayerTarget(LanPlayerState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.playerId) || state.playerId == localPlayerId)
            return;

        if (!serverPlayerTargets.TryGetValue(state.playerId, out Transform target) || target == null)
        {
            GameObject targetObject = new GameObject($"ServerPlayerTarget_{state.playerId}");
            targetObject.transform.SetParent(transform, false);
            target = targetObject.transform;
            serverPlayerTargets[state.playerId] = target;
        }

        target.SetPositionAndRotation(state.position, state.rotation);
    }

    void RemoveServerPlayerTarget(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return;

        if (!serverPlayerTargets.TryGetValue(playerId, out Transform target))
            return;

        serverPlayerTargets.Remove(playerId);

        if (target != null)
            Destroy(target.gameObject);
    }

    void HandleDisconnect(PeerConnection connection, bool isHostSide)
    {
        if (connection == null)
            return;

        if (Mode == SessionMode.None)
        {
            CloseConnection(connection);
            return;
        }

        if (isHostSide)
        {
            if (!string.IsNullOrWhiteSpace(connection.playerId))
            {
                hostPeers.Remove(connection.playerId);
                knownStates.Remove(connection.playerId);
                BroadcastPacket(CreatePacket("leave", new LanLeave { playerId = connection.playerId }), connection.playerId);
                RemoveReplica(connection.playerId);
                RemoveServerPlayerTarget(connection.playerId);
                StatusMessage = $"{connection.playerName} saiu da sessao";
                UpdateDiscoveryAnnouncement();
            }

            hostPeers.Remove(connection.addressLabel);
        }
        else if (serverConnection == connection)
        {
            serverConnection = null;
            ClearRemoteReplicas();

            if (!isShuttingDown)
            {
                Mode = SessionMode.None;
                SetError("Conexao com o host encerrada.");
            }
        }

        CloseConnection(connection);
    }

    LanPacket CreatePacket(string type, object payload)
    {
        return new LanPacket
        {
            type = type,
            payload = JsonUtility.ToJson(payload)
        };
    }

    void BroadcastPacket(LanPacket packet, string exceptPlayerId = null)
    {
        if (packet == null)
            return;

        string json = JsonUtility.ToJson(packet);

        foreach (KeyValuePair<string, PeerConnection> entry in hostPeers)
        {
            PeerConnection peer = entry.Value;
            if (peer == null || string.IsNullOrWhiteSpace(peer.playerId))
                continue;

            if (!string.IsNullOrWhiteSpace(exceptPlayerId) && peer.playerId == exceptPlayerId)
                continue;

            SendRaw(peer, json);
        }
    }

    void SendPacket(PeerConnection connection, LanPacket packet)
    {
        if (connection == null || packet == null)
            return;

        SendRaw(connection, JsonUtility.ToJson(packet));
    }

    void SendToPlayer(string playerId, LanPacket packet)
    {
        if (string.IsNullOrWhiteSpace(playerId) || packet == null)
            return;

        if (playerId == localPlayerId)
        {
            if (packet.type == "reward")
                ApplyRewardLocally(JsonUtility.FromJson<LanReward>(packet.payload));
            return;
        }

        if (hostPeers.TryGetValue(playerId, out PeerConnection peer))
            SendPacket(peer, packet);
    }

    void SendRaw(PeerConnection connection, string json)
    {
        if (connection == null || string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            lock (connection.writeLock)
            {
                connection.writer.WriteLine(json);
            }
        }
        catch
        {
        }
    }

    void EnqueueMainThread(Action action)
    {
        if (action != null)
            mainThreadActions.Enqueue(action);
    }

    void GrantReward(string playerId, LanReward reward)
    {
        if (reward == null || string.IsNullOrWhiteSpace(playerId))
            return;

        reward.playerId = playerId;
        SendToPlayer(playerId, CreatePacket("reward", reward));
    }

    void BroadcastEnemyStates()
    {
        MiniKrug[] miniKrugs = FindObjectsByType<MiniKrug>(FindObjectsSortMode.None);
        for (int i = 0; i < miniKrugs.Length; i++)
        {
            if (miniKrugs[i] == null || miniKrugs[i].IsPendingDestroy)
                continue;

            LanNetworkEntity entity = miniKrugs[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(miniKrugs[i]);

            BroadcastPacket(CreatePacket("enemy_state", new LanEnemyState
            {
                entityId = entity.EntityId,
                entityKind = nameof(MiniKrug),
                position = miniKrugs[i].transform.position,
                rotation = miniKrugs[i].transform.rotation,
                level = miniKrugs[i].EnemyLevel,
                health = miniKrugs[i].CurrentHealth,
                destroyed = false
            }));
        }

        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null || bosses[i].IsPendingDestroy)
                continue;

            LanNetworkEntity entity = bosses[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(bosses[i]);

            BroadcastPacket(CreatePacket("enemy_state", new LanEnemyState
            {
                entityId = entity.EntityId,
                entityKind = nameof(BossEnemy),
                position = bosses[i].transform.position,
                rotation = bosses[i].transform.rotation,
                level = bosses[i].BossLevel,
                health = bosses[i].CurrentHealth,
                destroyed = false
            }));
        }
    }

    void FlushPendingEntityUpdates()
    {
        if (pendingEntityUpdates.Count == 0)
            return;

        List<string> resolvedIds = new List<string>();

        foreach (KeyValuePair<string, LanEntityUpdate> entry in pendingEntityUpdates)
        {
            LanEntityUpdate update = entry.Value;
            bool applied = false;

            if (update.entityKind == nameof(ResourceNode))
            {
                ResourceNode node = FindEntity<ResourceNode>(update.entityId);
                if (node != null)
                {
                    node.ApplyNetworkState(update.health, update.destroyed);
                    applied = true;
                }
            }
            else if (update.entityKind == nameof(Cow))
            {
                Cow cow = FindEntity<Cow>(update.entityId);
                if (cow != null)
                {
                    cow.ApplyNetworkState(update.health, update.destroyed);
                    applied = true;
                }
            }
            else if (update.entityKind == nameof(WildChicken))
            {
                WildChicken chicken = FindEntity<WildChicken>(update.entityId);
                if (chicken != null)
                {
                    chicken.ApplyNetworkState(update.health, update.destroyed);
                    applied = true;
                }
            }
            else if (update.entityKind == nameof(WildBoar))
            {
                WildBoar boar = FindEntity<WildBoar>(update.entityId);
                if (boar != null)
                {
                    boar.ApplyNetworkState(update.health, update.destroyed);
                    applied = true;
                }
            }
            else if (update.entityKind == nameof(EarthGolem))
            {
                EarthGolem golem = FindEntity<EarthGolem>(update.entityId);
                if (golem != null)
                {
                    golem.ApplyNetworkState(update.health, update.destroyed);
                    applied = true;
                }
            }
            else if (update.entityKind == nameof(BossEnemy))
            {
                BossEnemy boss = FindEntity<BossEnemy>(update.entityId);
                if (boss != null)
                {
                    boss.ApplyNetworkState(update.health, update.destroyed);
                    applied = true;
                }
            }

            if (applied)
                resolvedIds.Add(entry.Key);
        }

        for (int i = 0; i < resolvedIds.Count; i++)
            pendingEntityUpdates.Remove(resolvedIds[i]);
    }

    void SyncWorldEntitiesTo(PeerConnection connection)
    {
        HashSet<string> liveIds = new HashSet<string>();

        ResourceNode[] resourceNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < resourceNodes.Length; i++)
        {
            LanNetworkEntity entity = resourceNodes[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                continue;

            liveIds.Add(entity.EntityId);

            SendPacket(connection, CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entity.EntityId,
                entityKind = nameof(ResourceNode),
                health = resourceNodes[i].CurrentHealth,
                destroyed = resourceNodes[i].IsDepleted
            }));
        }

        Cow[] cows = FindObjectsByType<Cow>(FindObjectsSortMode.None);
        for (int i = 0; i < cows.Length; i++)
        {
            LanNetworkEntity entity = cows[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                continue;

            liveIds.Add(entity.EntityId);

            SendPacket(connection, CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entity.EntityId,
                entityKind = nameof(Cow),
                health = cows[i].CurrentHealth,
                destroyed = false
            }));
        }

        WildChicken[] chickens = FindObjectsByType<WildChicken>(FindObjectsSortMode.None);
        for (int i = 0; i < chickens.Length; i++)
        {
            if (chickens[i] == null || chickens[i].IsDead)
                continue;

            LanNetworkEntity entity = chickens[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                continue;

            liveIds.Add(entity.EntityId);

            SendPacket(connection, CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entity.EntityId,
                entityKind = nameof(WildChicken),
                health = chickens[i].CurrentHealth,
                destroyed = false
            }));
        }

        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null || bosses[i].IsPendingDestroy)
                continue;

            LanNetworkEntity entity = bosses[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                continue;

            liveIds.Add(entity.EntityId);

            SendPacket(connection, CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entity.EntityId,
                entityKind = nameof(BossEnemy),
                health = bosses[i].CurrentHealth,
                destroyed = false
            }));
        }

        foreach (KeyValuePair<string, string> entry in destroyedEntities)
        {
            if (liveIds.Contains(entry.Key))
                continue;

            SendPacket(connection, CreatePacket("entity_update", new LanEntityUpdate
            {
                entityId = entry.Key,
                entityKind = entry.Value,
                health = 0,
                destroyed = true
            }));
        }
    }

    void SyncEnemyStatesTo(PeerConnection connection)
    {
        if (connection == null)
            return;

        MiniKrug[] miniKrugs = FindObjectsByType<MiniKrug>(FindObjectsSortMode.None);
        for (int i = 0; i < miniKrugs.Length; i++)
        {
            if (miniKrugs[i] == null || miniKrugs[i].IsPendingDestroy)
                continue;

            LanNetworkEntity entity = miniKrugs[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(miniKrugs[i]);

            SendPacket(connection, CreatePacket("enemy_state", new LanEnemyState
            {
                entityId = entity.EntityId,
                entityKind = nameof(MiniKrug),
                position = miniKrugs[i].transform.position,
                rotation = miniKrugs[i].transform.rotation,
                level = miniKrugs[i].EnemyLevel,
                health = miniKrugs[i].CurrentHealth,
                destroyed = false
            }));
        }

        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null || bosses[i].IsPendingDestroy)
                continue;

            LanNetworkEntity entity = bosses[i].GetComponent<LanNetworkEntity>();
            if (entity == null)
                entity = LanNetworkEntity.Ensure(bosses[i]);

            SendPacket(connection, CreatePacket("enemy_state", new LanEnemyState
            {
                entityId = entity.EntityId,
                entityKind = nameof(BossEnemy),
                position = bosses[i].transform.position,
                rotation = bosses[i].transform.rotation,
                level = bosses[i].BossLevel,
                health = bosses[i].CurrentHealth,
                destroyed = false
            }));
        }
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveLocalPlayer();
        TryApplyPendingWorldState();
        TryApplyPendingLocalRewards();

        if (Mode == SessionMode.Client && isApplyingRemoteSceneChange)
            TryFinalizeRemoteSceneChange();

        if (!IsServerAuthority || !IsMultiplayerActive)
            return;

        BroadcastCurrentSceneSet();

        foreach (KeyValuePair<string, PeerConnection> entry in hostPeers)
        {
            if (entry.Value == null || string.IsNullOrWhiteSpace(entry.Value.playerId))
                continue;

            SyncWorldEntitiesTo(entry.Value);
            SyncEnemyStatesTo(entry.Value);
        }

        UpdateDiscoveryAnnouncement();
    }

    void HandleSceneUnloaded(Scene scene)
    {
        if (Mode == SessionMode.Client && isApplyingRemoteSceneChange)
            TryFinalizeRemoteSceneChange();

        if (!IsServerAuthority || !IsMultiplayerActive)
            return;

        BroadcastCurrentSceneSet();
        UpdateDiscoveryAnnouncement();
    }

    void BroadcastCurrentSceneSet()
    {
        MultiplayerSceneSetState currentSceneSet = CaptureCurrentSceneSet();
        LanPacket scenePacket = CreatePacket("scene", new LanSceneChange
        {
            sceneName = currentSceneSet?.activeSceneName ?? SceneManager.GetActiveScene().name,
            sceneSetId = currentSceneSet?.sceneSetId,
            activeSceneName = currentSceneSet?.activeSceneName,
            sceneNames = currentSceneSet?.sceneNames
        });

        BroadcastPacket(scenePacket);
    }

    void UpdateDiscoveryAnnouncement()
    {
        if (!IsServerAuthority || !IsMultiplayerActive || string.IsNullOrWhiteSpace(SessionId))
        {
            LanSessionDiscovery.Instance?.StopAnnouncing();
            return;
        }

        int playerCount = localPlayer != null ? 1 : 0;

        foreach (KeyValuePair<string, PeerConnection> entry in hostPeers)
        {
            if (entry.Value != null && !string.IsNullOrWhiteSpace(entry.Value.playerId))
                playerCount++;
        }

        MultiplayerSceneSetState currentSceneSet = CaptureCurrentSceneSet();
        string sceneName = MultiplayerSceneSetCatalog.BuildDisplayLabel(currentSceneSet);
        string hostName = string.IsNullOrWhiteSpace(localPlayerName) ? BuildPlayerName() : localPlayerName;
        LanSessionDiscovery.Instance?.StartAnnouncing(SessionId, hostName, CurrentPort, sceneName, playerCount);
    }

    void TryFinalizeRemoteSceneChange()
    {
        if (Mode != SessionMode.Client || pendingRemoteSceneSet == null)
            return;

        if (!MultiplayerSceneSetCatalog.LoadedScenesMatch(pendingRemoteSceneSet))
            return;

        Scene targetActiveScene = SceneManager.GetSceneByName(pendingRemoteSceneSet.activeSceneName);
        if (targetActiveScene.IsValid() && targetActiveScene.isLoaded)
            SceneManager.SetActiveScene(targetActiveScene);

        isApplyingRemoteSceneChange = false;
        pendingRemoteSceneSet = null;
        ClearRemoteReplicas();
        TryApplyPendingWorldState();
        TryApplyPendingLocalRewards();
        StatusMessage = $"Conectado em {CurrentAddress}:{CurrentPort}";
    }

    void TriggerClientHitFeedback(Component target, int damage)
    {
        if (target is ResourceNode resourceNode)
        {
            resourceNode.PlayHitFeedback();
            return;
        }

        if (target is MiniKrug miniKrug)
        {
            miniKrug.PlayLocalHitFeedback(damage);
            return;
        }

        if (target is WildChicken chicken)
        {
            chicken.PlayLocalHitFeedback(damage);
            return;
        }

        if (target is WildBoar boar)
        {
            boar.PlayLocalHitFeedback(damage);
            return;
        }

        if (target is EarthGolem golem)
        {
            golem.PlayLocalHitFeedback(damage);
            return;
        }

        if (target is BossEnemy bossEnemy)
            bossEnemy.PlayLocalHitFeedback(damage);
    }

    void TryApplyPendingWorldState()
    {
        if (Mode != SessionMode.Client || pendingWorldState == null || DayNightCycle.Instance == null)
            return;

        DayNightCycle.Instance.LoadState(pendingWorldState.currentDay, pendingWorldState.normalizedTimeOfDay);
        pendingWorldState = null;
    }

    void QueueLocalReward(LanReward reward)
    {
        if (reward == null)
            return;

        pendingLocalRewards.Add(reward);
    }

    void TryApplyPendingLocalRewards()
    {
        if (Mode != SessionMode.Client || pendingLocalRewards.Count == 0)
            return;

        for (int i = pendingLocalRewards.Count - 1; i >= 0; i--)
        {
            LanReward reward = pendingLocalRewards[i];
            pendingLocalRewards.RemoveAt(i);
            ApplyRewardLocally(reward);
        }
    }

    void RefreshClientHud()
    {
        InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (inventoryUI != null)
            inventoryUI.Refresh();

        GoldHUD goldHud = FindFirstObjectByType<GoldHUD>();
        if (goldHud != null)
            goldHud.Refresh();

        LevelHUD levelHud = FindFirstObjectByType<LevelHUD>();
        if (levelHud != null)
            levelHud.Refresh();
    }

    MiniKrug CreateRemoteMiniKrug(LanEnemyState state)
    {
        GameObject miniKrugPrefab = ForestMushroomMonsterFactory.IsForestMushroomEntity(state.entityId)
            ? ForestMushroomMonsterFactory.LoadPrefab()
            : Resources.Load<GameObject>("Enemies/MiniKrug");
        if (miniKrugPrefab == null)
            return null;

        MiniKrug miniKrug;

        if (ForestMushroomMonsterFactory.IsForestMushroomEntity(state.entityId))
        {
            miniKrug = ForestMushroomMonsterFactory.CreateInstance(miniKrugPrefab, state.position, state.rotation);
            if (miniKrug == null)
                return null;

            LanNetworkEntity.Ensure(miniKrug.transform, state.entityId);
        }
        else
        {
            GameObject miniKrugObject = Instantiate(miniKrugPrefab, state.position, state.rotation);
            miniKrugObject.name = miniKrugPrefab.name;
            LanNetworkEntity.Ensure(miniKrugObject.transform, state.entityId);
            miniKrug = miniKrugObject.GetComponent<MiniKrug>();
        }

        return miniKrug;
    }

    BossEnemy CreateRemoteBossEnemy(LanEnemyState state)
    {
        if (!ForestMushroomBossFactory.IsForestMushroomBossEntity(state.entityId))
            return null;

        GameObject bossPrefab = ForestMushroomBossFactory.LoadPrefab();
        if (bossPrefab == null)
            return null;

        BossEnemy boss = ForestMushroomBossFactory.CreateInstance(bossPrefab, state.position, state.rotation);
        if (boss == null)
            return null;

        LanNetworkEntity.Ensure(boss.transform, state.entityId);
        return boss;
    }

    public bool TryGetSuggestedEnemyLevel(Vector3 origin, out int level)
    {
        level = 1;
        float bestDistance = float.MaxValue;
        bool foundCandidate = false;

        if (localPlayer != null && !GameState.IsPlayerDead)
        {
            float distance = (localPlayer.transform.position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                level = GetLocalPlayerLevel();
                foundCandidate = true;
            }
        }

        foreach (KeyValuePair<string, Transform> entry in serverPlayerTargets)
        {
            if (entry.Value == null)
                continue;

            if (knownStates.TryGetValue(entry.Key, out LanPlayerState state) && state != null && state.isDead)
                continue;

            float distance = (entry.Value.position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                level = GetPlayerLevel(entry.Key);
                foundCandidate = true;
            }
        }

        return foundCandidate;
    }

    public bool TryFindClosestEnemyTarget(Vector3 origin, out Transform targetTransform, out string targetPlayerId)
    {
        targetTransform = null;
        targetPlayerId = null;

        float bestDistance = float.MaxValue;

        if (localPlayer != null && !GameState.IsPlayerDead)
        {
            float distance = (localPlayer.transform.position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                targetTransform = localPlayer.transform;
                targetPlayerId = localPlayerId;
            }
        }

        foreach (KeyValuePair<string, Transform> entry in serverPlayerTargets)
        {
            if (entry.Value == null)
                continue;

            if (knownStates.TryGetValue(entry.Key, out LanPlayerState state) && state != null && state.isDead)
                continue;

            float distance = (entry.Value.position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                targetTransform = entry.Value;
                targetPlayerId = entry.Key;
            }
        }

        return targetTransform != null && !string.IsNullOrWhiteSpace(targetPlayerId);
    }

    public void ApplyEnemyDamage(string targetPlayerId, float damage)
    {
        if (string.IsNullOrWhiteSpace(targetPlayerId) || damage <= 0f)
            return;

        if (targetPlayerId == localPlayerId)
        {
            ResolveLocalPlayer();
            localPlayer?.TakeDamage(damage);
            return;
        }

        SendToPlayer(targetPlayerId, CreatePacket("damage", new LanDamageEvent
        {
            playerId = targetPlayerId,
            damage = damage
        }));
    }

    public void ApplyEnemyAreaDamage(Vector3 origin, float radius, float damage)
    {
        if (radius <= 0f || damage <= 0f)
            return;

        float radiusSqr = radius * radius;

        if (localPlayer != null && !GameState.IsPlayerDead)
        {
            Vector3 toLocalPlayer = localPlayer.transform.position - origin;
            toLocalPlayer.y = 0f;
            if (toLocalPlayer.sqrMagnitude <= radiusSqr)
                localPlayer.TakeDamage(damage);
        }

        foreach (KeyValuePair<string, Transform> entry in serverPlayerTargets)
        {
            if (entry.Value == null)
                continue;

            if (knownStates.TryGetValue(entry.Key, out LanPlayerState state) && state != null && state.isDead)
                continue;

            Vector3 toRemotePlayer = entry.Value.position - origin;
            toRemotePlayer.y = 0f;
            if (toRemotePlayer.sqrMagnitude <= radiusSqr)
                ApplyEnemyDamage(entry.Key, damage);
        }
    }

    public bool IsEntityDestroyed(string entityId)
    {
        return !string.IsNullOrWhiteSpace(entityId) && destroyedEntities.ContainsKey(entityId);
    }

    public void ClearDestroyedEntity(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return;

        destroyedEntities.Remove(entityId);
        pendingEntityUpdates.Remove(entityId);
    }

    public void NotifyResourceRespawned(ResourceNode resource, int health)
    {
        if (resource == null)
            return;

        LanNetworkEntity entity = ResolveNetworkEntity(resource);
        if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId))
            return;

        ClearDestroyedEntity(entity.EntityId);

        if (!IsServerAuthority || !IsMultiplayerActive)
            return;

        BroadcastPacket(CreatePacket("entity_update", new LanEntityUpdate
        {
            entityId = entity.EntityId,
            entityKind = nameof(ResourceNode),
            health = Mathf.Max(1, health),
            destroyed = false
        }));
    }

    public void NotifyEnemyDestroyed(Component enemy)
    {
        if (enemy == null || !IsServerAuthority)
            return;

        LanNetworkEntity entity = ResolveNetworkEntity(enemy);
        string entityKind = GetEntityKind(enemy);
        if (entity == null || string.IsNullOrWhiteSpace(entityKind))
            return;

        BroadcastPacket(CreatePacket("enemy_state", new LanEnemyState
        {
            entityId = entity.EntityId,
            entityKind = entityKind,
            position = enemy.transform.position,
            rotation = enemy.transform.rotation,
            level = GetEnemyLevel(enemy),
            health = 0,
            destroyed = true
        }));
    }

    void ShutdownSession()
    {
        isShuttingDown = true;

        try
        {
            hostListener?.Stop();
        }
        catch
        {
        }

        hostListener = null;

        if (serverConnection != null)
            CloseConnection(serverConnection);

        serverConnection = null;

        foreach (KeyValuePair<string, PeerConnection> entry in hostPeers)
            CloseConnection(entry.Value);

        hostPeers.Clear();

        ClearRemoteReplicas();
        knownStates.Clear();
        destroyedEntities.Clear();
        pendingEntityUpdates.Clear();
        bearerPowerOwners.Clear();
        claimedBearerPowerIds.Clear();
        State = SessionState.Idle;
        Mode = SessionMode.None;
        SessionId = null;
        StatusMessage = "Solo";
        hasLastLocalPosition = false;
        cachedAnimSpeed = 0f;
        isShuttingDown = false;
        LanSessionDiscovery.Instance?.StopAnnouncing();
        BearerPowerAvailabilityChanged?.Invoke();
    }

    void ClearRemoteReplicas()
    {
        List<string> remotePlayerIds = new List<string>();

        foreach (KeyValuePair<string, RemotePlayerReplica> entry in remoteReplicas)
        {
            remotePlayerIds.Add(entry.Key);

            if (entry.Value != null)
                Destroy(entry.Value.gameObject);
        }

        remoteReplicas.Clear();

        foreach (string playerId in remotePlayerIds)
            knownStates.Remove(playerId);

        List<string> serverTargetIds = new List<string>(serverPlayerTargets.Keys);
        for (int i = 0; i < serverTargetIds.Count; i++)
            RemoveServerPlayerTarget(serverTargetIds[i]);
    }

    void CloseConnection(PeerConnection connection)
    {
        if (connection == null)
            return;

        try
        {
            connection.client?.Close();
        }
        catch
        {
        }
    }

    void SetError(string message)
    {
        State = SessionState.Error;
        StatusMessage = message;
        LastErrorMessage = message;
        Debug.LogError($"[LanMultiplayer] {message}");
    }

    string BuildConnectionErrorMessage(string address, int port, Exception exception)
    {
        if (exception is SocketException socketException)
        {
            switch (socketException.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    return $"Falha na conexao: {address}:{port} recusou a conexao. O host provavelmente nao esta hospedando ou a porta esta bloqueada.";
                case SocketError.TimedOut:
                    return $"Falha na conexao: tempo esgotado em {address}:{port}. Verifique o IP do Tailscale, firewall e se o host esta online.";
                case SocketError.HostNotFound:
                case SocketError.NoData:
                    return $"Falha na conexao: host {address} nao encontrado.";
                case SocketError.NetworkUnreachable:
                case SocketError.HostUnreachable:
                    return $"Falha na conexao: nao foi possivel alcancar {address}:{port}. Verifique a conexao do Tailscale.";
            }

            return $"Falha na conexao: erro de socket {socketException.SocketErrorCode} em {address}:{port}. {socketException.Message}";
        }

        if (exception is IOException)
            return $"Falha na conexao: {exception.Message}";

        return $"Falha na conexao em {address}:{port}: {exception.GetType().Name}: {exception.Message}";
    }

    LanNetworkEntity ResolveNetworkEntity(Component target)
    {
        if (target == null)
            return null;

        if (target.GetComponentInParent<ResourceNode>() is ResourceNode node)
            return LanNetworkEntity.Ensure(node);

        if (target.GetComponentInParent<Cow>() is Cow cow)
            return LanNetworkEntity.Ensure(cow);

        if (target.GetComponentInParent<WildChicken>() is WildChicken chicken)
            return LanNetworkEntity.Ensure(chicken);

        if (target.GetComponentInParent<WildBoar>() is WildBoar boar)
            return LanNetworkEntity.Ensure(boar);

        if (target.GetComponentInParent<EarthGolem>() is EarthGolem golem)
            return LanNetworkEntity.Ensure(golem);

        if (target.GetComponentInParent<MiniKrug>() is MiniKrug miniKrug)
            return LanNetworkEntity.Ensure(miniKrug);

        if (target.GetComponentInParent<BossEnemy>() is BossEnemy boss)
            return LanNetworkEntity.Ensure(boss);

        return null;
    }

    string GetEntityKind(Component target)
    {
        if (target == null)
            return null;

        if (target.GetComponentInParent<ResourceNode>() != null)
            return nameof(ResourceNode);

        if (target.GetComponentInParent<Cow>() != null)
            return nameof(Cow);

        if (target.GetComponentInParent<WildChicken>() != null)
            return nameof(WildChicken);

        if (target.GetComponentInParent<WildBoar>() != null)
            return nameof(WildBoar);

        if (target.GetComponentInParent<EarthGolem>() != null)
            return nameof(EarthGolem);

        if (target.GetComponentInParent<MiniKrug>() != null)
            return nameof(MiniKrug);

        if (target.GetComponentInParent<BossEnemy>() != null)
            return nameof(BossEnemy);

        return null;
    }

    T FindEntity<T>(string entityId) where T : Component
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return null;

        T[] entities = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < entities.Length; i++)
        {
            T entity = entities[i];
            if (entity == null)
                continue;

            LanNetworkEntity networkEntity = entity.GetComponent<LanNetworkEntity>();
            if (networkEntity != null && networkEntity.EntityId == entityId)
                return entity;
        }

        if (typeof(T) == typeof(BossEnemy))
        {
            T[] bosses = FindObjectsByType<T>(FindObjectsSortMode.None);
            if (bosses.Length == 1 && bosses[0] != null)
            {
                LanNetworkEntity networkEntity = bosses[0].GetComponent<LanNetworkEntity>();
                if (networkEntity == null || networkEntity.EntityId != entityId)
                {
                    LanNetworkEntity.Ensure(bosses[0], entityId);
                    Debug.LogWarning($"[LanMultiplayer] Boss entity id fallback applied. Using '{entityId}' for the only boss in scene.");
                }

                return bosses[0];
            }
        }

        return null;
    }

    PlayerMovement ResolveAttacker(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return null;

        if (playerId == localPlayerId)
            return localPlayer;

        return null;
    }

    int GetLocalPlayerLevel()
    {
        if (localPlayer == null)
            ResolveLocalPlayer();

        PlayerProgression progression = localPlayer != null ? localPlayer.GetComponent<PlayerProgression>() : null;
        return Mathf.Max(1, progression != null ? progression.currentLevel : 1);
    }

    int GetPlayerLevel(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return 1;

        if (playerId == localPlayerId)
            return GetLocalPlayerLevel();

        if (knownStates.TryGetValue(playerId, out LanPlayerState state) && state != null)
            return Mathf.Max(1, state.level);

        return 1;
    }

    int GetEnemyLevel(Component enemy)
    {
        if (enemy is MiniKrug miniKrug)
            return miniKrug.EnemyLevel;

        if (enemy is BossEnemy bossEnemy)
            return bossEnemy.BossLevel;

        if (enemy is EarthGolem earthGolem)
            return earthGolem.GolemLevel;

        return 1;
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

    string CreatePlayerId()
    {
        return Guid.NewGuid().ToString("N");
    }

    string BuildPlayerName()
    {
        string deviceName = SystemInfo.deviceName;
        return string.IsNullOrWhiteSpace(deviceName) ? "Sobrevivente" : deviceName.Trim();
    }

    string GetLocalIpv4Address()
    {
        try
        {
            string firstLanAddress = null;

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (networkInterface == null || networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    IPAddress address = unicastAddress.Address;
                    if (address == null || address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                        continue;

                    string ip = address.ToString();

                    // Tailscale uses the CGNAT 100.64.0.0/10 range. Prefer it for remote LAN sessions.
                    if (IsTailscaleAddress(address) || networkInterface.Name.Contains("Tailscale") || networkInterface.Description.Contains("Tailscale"))
                        return ip;

                    if (firstLanAddress == null)
                        firstLanAddress = ip;
                }
            }

            if (!string.IsNullOrWhiteSpace(firstLanAddress))
                return firstLanAddress;
        }
        catch
        {
        }

        try
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    return address.ToString();
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }

    bool IsTailscaleAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (bytes == null || bytes.Length != 4)
            return false;

        return bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;
    }
}
