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

    public bool StartSolo()
    {
        ShutdownSession();
        worldSeed = Environment.TickCount;
        SessionId = null;
        Mode = SessionMode.Solo;
        State = SessionState.Ready;
        StatusMessage = "Solo";
        LastErrorMessage = null;
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
            MultiplayerSceneSetState startupSceneSet = MultiplayerSceneSetCatalog.ResolveStartupState(sceneSetId, sceneName);
            StatusMessage = $"Servidor dedicado ativo em {CurrentAddress}:{CurrentPort} [{SessionId}]";
            knownStates.Clear();

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
                health = resourceNodes[i].CurrentHealth,
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

        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
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
        return component != null && component.GetComponent<RemotePlayerReplica>() != null;
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
            level = GetLocalPlayerLevel()
        };
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
                return "Use uma picareta para pedra.";
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
                    message = $"+{rewardAmount} {rewardItemName}"
                });

            return;
        }

        if (entityKind == nameof(Cow))
        {
            Cow cow = FindEntity<Cow>(entityId);
            if (cow == null)
                return;

            cow.ApplyNetworkHit(damage, out int rewardAmount, out string rewardItemName, out string rewardPrefabName, out int remainingHealth, out bool destroyed);
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
                    message = $"+{rewardAmount} {rewardItemName}"
                });

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
                    message = unlockMagic ? "Magia ancestral desbloqueada!" : $"+{goldAmount} gold"
                });
        }
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

        knownStates[state.playerId] = state;
        UpsertServerPlayerTarget(state);

        if (state.playerId != localPlayerId)
            UpsertReplica(state);

        if (isHostSide)
            BroadcastPacket(CreatePacket("state", state), connection.playerId);
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
        if (localPlayer == null)
            ResolveLocalPlayer();

        if (localPlayer == null || state == null)
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
                destroyed = false
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

        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
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
        GameObject miniKrugPrefab = Resources.Load<GameObject>("Enemies/MiniKrug");
        if (miniKrugPrefab == null)
            return null;

        GameObject miniKrugObject = Instantiate(miniKrugPrefab, state.position, state.rotation);
        miniKrugObject.name = miniKrugPrefab.name;
        LanNetworkEntity.Ensure(miniKrugObject.transform, state.entityId);

        MiniKrug miniKrug = miniKrugObject.GetComponent<MiniKrug>();
        if (miniKrug == null)
            miniKrug = miniKrugObject.AddComponent<MiniKrug>();

        return miniKrug;
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
        State = SessionState.Idle;
        Mode = SessionMode.None;
        SessionId = null;
        StatusMessage = "Solo";
        hasLastLocalPosition = false;
        cachedAnimSpeed = 0f;
        isShuttingDown = false;
        LanSessionDiscovery.Instance?.StopAnnouncing();
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

        return 1;
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
