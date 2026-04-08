using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class LanMultiplayerManager : MonoBehaviour
{
    public enum SessionMode
    {
        None,
        Solo,
        Host,
        Client
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
    }

    [Serializable]
    class LanWelcome
    {
        public string playerId;
        public int worldSeed;
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
    class LanWorldState
    {
        public int currentDay;
        public float normalizedTimeOfDay;
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

    public SessionMode Mode { get; private set; } = SessionMode.None;
    public SessionState State { get; private set; } = SessionState.Idle;
    public bool IsMultiplayerActive => Mode == SessionMode.Host || Mode == SessionMode.Client;
    public bool IsSessionReady => State == SessionState.Ready;
    public string StatusMessage { get; private set; } = "Solo";
    public string LastErrorMessage { get; private set; }
    public int CurrentPort { get; private set; } = 7777;
    public string CurrentAddress { get; private set; } = "127.0.0.1";
    public int WorldSeed => worldSeed;

    readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    readonly Dictionary<string, RemotePlayerReplica> remoteReplicas = new Dictionary<string, RemotePlayerReplica>();
    readonly Dictionary<string, LanPlayerState> knownStates = new Dictionary<string, LanPlayerState>();
    readonly Dictionary<string, PeerConnection> hostPeers = new Dictionary<string, PeerConnection>();
    readonly Dictionary<string, string> destroyedEntities = new Dictionary<string, string>();
    readonly Dictionary<string, LanEntityUpdate> pendingEntityUpdates = new Dictionary<string, LanEntityUpdate>();

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

    const float StateSendInterval = 0.05f;
    const float WorldSyncInterval = 1f;

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
    }

    void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
            action?.Invoke();

        FlushPendingEntityUpdates();

        ResolveLocalPlayer();

        if (!IsSessionReady || localPlayer == null)
            return;

        if (Time.unscaledTime >= nextStateSendTime)
        {
            nextStateSendTime = Time.unscaledTime + StateSendInterval;
            SendLocalPlayerState();
        }

        if (Mode == SessionMode.Host && Time.unscaledTime >= nextWorldSyncTime)
        {
            nextWorldSyncTime = Time.unscaledTime + WorldSyncInterval;
            BroadcastWorldState();
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

        ShutdownSession();
    }

    public bool StartSolo()
    {
        ShutdownSession();
        worldSeed = Environment.TickCount;
        Mode = SessionMode.Solo;
        State = SessionState.Ready;
        StatusMessage = "Solo";
        LastErrorMessage = null;
        return true;
    }

    public bool StartHost(int port)
    {
        ShutdownSession();
        ResolveLocalPlayer();

        if (localPlayer == null)
        {
            SetError("Player local nao encontrado para hospedar.");
            return false;
        }

        try
        {
            localPlayerId = CreatePlayerId();
            localPlayerName = BuildPlayerName();
            worldSeed = Environment.TickCount;
            CurrentPort = Mathf.Max(1, port);
            CurrentAddress = GetLocalIpv4Address();
            Mode = SessionMode.Host;
            State = SessionState.Ready;
            LastErrorMessage = null;
            StatusMessage = $"Host ativo em {CurrentAddress}:{CurrentPort}";
            knownStates.Clear();

            hostListener = new TcpListener(IPAddress.Any, CurrentPort);
            hostListener.Start();

            acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "LanHostAcceptLoop"
            };
            acceptThread.Start();

            hasLastLocalPosition = false;
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

    public bool StartClient(string address, int port)
    {
        ShutdownSession();
        ResolveLocalPlayer();

        if (localPlayer == null)
        {
            SetError("Player local nao encontrado para conectar.");
            return false;
        }

        localPlayerId = CreatePlayerId();
        localPlayerName = BuildPlayerName();
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
        if (Instance != null && Instance.localPlayer != null && !IsReplica(Instance.localPlayer))
            return Instance.localPlayer;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement player in players)
        {
            if (player != null && !IsReplica(player))
                return player;
        }

        return null;
    }

    public static PlayerMovement[] GetGameplayPlayers()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        List<PlayerMovement> filteredPlayers = new List<PlayerMovement>();

        foreach (PlayerMovement player in players)
        {
            if (player != null && !IsReplica(player))
                filteredPlayers.Add(player);
        }

        return filteredPlayers.ToArray();
    }

    public static bool IsReplica(Component component)
    {
        return component != null && component.GetComponent<RemotePlayerReplica>() != null;
    }

    void ResolveLocalPlayer()
    {
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
            isDead = GameState.IsPlayerDead
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
        if (Mode != SessionMode.Client || state == null || DayNightCycle.Instance == null)
            return;

        DayNightCycle.Instance.LoadState(state.currentDay, state.normalizedTimeOfDay);
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

        SendPacket(connection, CreatePacket("welcome", new LanWelcome
        {
            playerId = connection.playerId,
            worldSeed = worldSeed
        }));

        foreach (KeyValuePair<string, LanPlayerState> state in knownStates)
            SendPacket(connection, CreatePacket("state", state.Value));

        SyncWorldEntitiesTo(connection);
        BroadcastWorldState();
        StatusMessage = $"{connection.playerName} entrou na sessao";
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

    void ApplyRewardLocally(LanReward reward)
    {
        if (reward == null || reward.playerId != localPlayerId)
            return;

        ResolveLocalPlayer();
        Inventory inventory = localPlayer != null ? localPlayer.GetComponent<Inventory>() : null;
        Hotbar hotbar = localPlayer != null ? localPlayer.GetComponent<Hotbar>() : null;
        PlayerProgression progression = localPlayer != null ? localPlayer.GetComponent<PlayerProgression>() : null;
        PlayerMagic magic = localPlayer != null ? localPlayer.GetComponent<PlayerMagic>() : null;

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

        InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (inventoryUI != null)
            inventoryUI.Refresh();
    }

    void HandleStatePacket(PeerConnection connection, string payload, bool isHostSide)
    {
        LanPlayerState state = JsonUtility.FromJson<LanPlayerState>(payload);
        if (state == null || string.IsNullOrWhiteSpace(state.playerId))
            return;

        if (isHostSide && !string.IsNullOrWhiteSpace(connection.playerId))
            state.playerId = connection.playerId;

        knownStates[state.playerId] = state;

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
                StatusMessage = $"{connection.playerName} saiu da sessao";
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
        StatusMessage = "Solo";
        hasLastLocalPosition = false;
        cachedAnimSpeed = 0f;
        isShuttingDown = false;
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
