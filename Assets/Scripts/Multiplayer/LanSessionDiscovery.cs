using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class LanDiscoveredSession
{
    public string sessionId;
    public string hostName;
    public string sceneName;
    public string address;
    public int port;
    public int playerCount;
    public long lastSeenUtcTicks;
}

public class LanSessionDiscovery : MonoBehaviour
{
    [Serializable]
    class LanSessionAnnouncement
    {
        public string sessionId;
        public string hostName;
        public string sceneName;
        public int port;
        public int playerCount;
    }

    public static LanSessionDiscovery Instance { get; private set; }

    const int DiscoveryPort = 47777;
    const int SessionTimeoutSeconds = 4;
    const int BroadcastIntervalMilliseconds = 1000;

    readonly object sessionsLock = new object();
    readonly Dictionary<string, LanDiscoveredSession> sessions = new Dictionary<string, LanDiscoveredSession>();

    UdpClient listener;
    UdpClient broadcaster;
    Thread listenThread;
    Thread announceThread;

    volatile bool isShuttingDown;
    volatile bool isAnnouncing;

    string announcedSessionId;
    string announcedHostName;
    string announcedSceneName;
    int announcedPort;
    int announcedPlayerCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null || FindFirstObjectByType<LanSessionDiscovery>() != null)
            return;

        GameObject discoveryObject = new GameObject("LanSessionDiscovery");
        discoveryObject.AddComponent<LanSessionDiscovery>();
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
        StartListener();
        StartAnnouncer();
    }

    void Update()
    {
        PruneExpiredSessions();
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Shutdown();
    }

    public void StartAnnouncing(string sessionId, string hostName, int port, string sceneName, int playerCount)
    {
        announcedSessionId = sessionId;
        announcedHostName = string.IsNullOrWhiteSpace(hostName) ? "Host LAN" : hostName.Trim();
        announcedSceneName = string.IsNullOrWhiteSpace(sceneName) ? "-" : sceneName.Trim();
        announcedPort = Mathf.Max(1, port);
        announcedPlayerCount = Mathf.Max(1, playerCount);
        isAnnouncing = !string.IsNullOrWhiteSpace(announcedSessionId);
    }

    public void UpdateAnnouncement(string sceneName, int playerCount)
    {
        announcedSceneName = string.IsNullOrWhiteSpace(sceneName) ? "-" : sceneName.Trim();
        announcedPlayerCount = Mathf.Max(1, playerCount);
    }

    public void StopAnnouncing()
    {
        isAnnouncing = false;
        announcedSessionId = null;
        announcedHostName = null;
        announcedSceneName = null;
        announcedPort = 0;
        announcedPlayerCount = 0;
    }

    public List<LanDiscoveredSession> GetSessions()
    {
        PruneExpiredSessions();

        List<LanDiscoveredSession> results = new List<LanDiscoveredSession>();

        lock (sessionsLock)
        {
            foreach (KeyValuePair<string, LanDiscoveredSession> entry in sessions)
            {
                LanDiscoveredSession session = entry.Value;
                if (session == null)
                    continue;

                results.Add(new LanDiscoveredSession
                {
                    sessionId = session.sessionId,
                    hostName = session.hostName,
                    sceneName = session.sceneName,
                    address = session.address,
                    port = session.port,
                    playerCount = session.playerCount,
                    lastSeenUtcTicks = session.lastSeenUtcTicks
                });
            }
        }

        results.Sort((left, right) => right.lastSeenUtcTicks.CompareTo(left.lastSeenUtcTicks));
        return results;
    }

    void StartListener()
    {
        try
        {
            listener = new UdpClient();
            listener.EnableBroadcast = true;
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            listenThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "LanSessionDiscoveryListen"
            };
            listenThread.Start();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LanDiscovery] Falha ao iniciar escuta de sessoes: {exception.Message}");
        }
    }

    void StartAnnouncer()
    {
        announceThread = new Thread(AnnounceLoop)
        {
            IsBackground = true,
            Name = "LanSessionDiscoveryAnnounce"
        };
        announceThread.Start();
    }

    void ListenLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (!isShuttingDown)
        {
            try
            {
                if (listener == null)
                {
                    Thread.Sleep(250);
                    continue;
                }

                byte[] data = listener.Receive(ref remoteEndPoint);
                if (data == null || data.Length == 0)
                    continue;

                string json = Encoding.UTF8.GetString(data);
                LanSessionAnnouncement announcement = JsonUtility.FromJson<LanSessionAnnouncement>(json);
                if (announcement == null || string.IsNullOrWhiteSpace(announcement.sessionId) || announcement.port <= 0)
                    continue;

                lock (sessionsLock)
                {
                    sessions[announcement.sessionId] = new LanDiscoveredSession
                    {
                        sessionId = announcement.sessionId,
                        hostName = string.IsNullOrWhiteSpace(announcement.hostName) ? "Host LAN" : announcement.hostName.Trim(),
                        sceneName = string.IsNullOrWhiteSpace(announcement.sceneName) ? "-" : announcement.sceneName.Trim(),
                        address = remoteEndPoint.Address.ToString(),
                        port = announcement.port,
                        playerCount = Mathf.Max(1, announcement.playerCount),
                        lastSeenUtcTicks = DateTime.UtcNow.Ticks
                    };
                }
            }
            catch (SocketException)
            {
                if (isShuttingDown)
                    return;
            }
            catch
            {
            }
        }
    }

    void AnnounceLoop()
    {
        try
        {
            broadcaster = new UdpClient();
            broadcaster.EnableBroadcast = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LanDiscovery] Falha ao iniciar broadcast de sessoes: {exception.Message}");
            return;
        }

        IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        while (!isShuttingDown)
        {
            try
            {
                if (!isAnnouncing || string.IsNullOrWhiteSpace(announcedSessionId) || announcedPort <= 0)
                {
                    Thread.Sleep(250);
                    continue;
                }

                LanSessionAnnouncement announcement = new LanSessionAnnouncement
                {
                    sessionId = announcedSessionId,
                    hostName = announcedHostName,
                    sceneName = announcedSceneName,
                    port = announcedPort,
                    playerCount = announcedPlayerCount
                };

                byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(announcement));
                broadcaster.Send(data, data.Length, broadcastEndPoint);
            }
            catch (SocketException)
            {
            }
            catch
            {
            }

            Thread.Sleep(BroadcastIntervalMilliseconds);
        }
    }

    void PruneExpiredSessions()
    {
        long minTicks = DateTime.UtcNow.AddSeconds(-SessionTimeoutSeconds).Ticks;
        List<string> expiredIds = null;

        lock (sessionsLock)
        {
            foreach (KeyValuePair<string, LanDiscoveredSession> entry in sessions)
            {
                if (entry.Value == null || entry.Value.lastSeenUtcTicks >= minTicks)
                    continue;

                if (expiredIds == null)
                    expiredIds = new List<string>();
                expiredIds.Add(entry.Key);
            }

            if (expiredIds == null)
                return;

            for (int i = 0; i < expiredIds.Count; i++)
                sessions.Remove(expiredIds[i]);
        }
    }

    void Shutdown()
    {
        isShuttingDown = true;
        isAnnouncing = false;

        try
        {
            listener?.Close();
        }
        catch
        {
        }

        try
        {
            broadcaster?.Close();
        }
        catch
        {
        }

        listener = null;
        broadcaster = null;
    }
}
