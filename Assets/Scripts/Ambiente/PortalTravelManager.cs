using UnityEngine;
using UnityEngine.SceneManagement;

<<<<<<< HEAD
public class PortalTravelManager : MonoBehaviour
{
    static PortalTravelManager instance;
    static string pendingSceneSetId;
    static string pendingSpawnPointId;
    static float nextTravelAllowedTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetState()
    {
        pendingSceneSetId = null;
        pendingSpawnPointId = null;
        nextTravelAllowedTime = 0f;
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<PortalTravelManager>() != null)
            return;

        GameObject managerObject = new GameObject("Portal Travel Manager");
        managerObject.AddComponent<PortalTravelManager>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

=======
[DisallowMultipleComponent]
public class PortalTravelManager : MonoBehaviour
{
    struct PendingTravel
    {
        public bool active;
        public string sceneName;
        public string spawnPointId;
        public Vector3 fallbackPosition;
        public Quaternion fallbackRotation;
        public float expiresAt;
    }

    static PortalTravelManager instance;
    static PendingTravel pendingTravel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void QueueArrival(string sceneName, string spawnPointId, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        EnsureInstance();
        pendingTravel = new PendingTravel
        {
            active = true,
            sceneName = sceneName,
            spawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? "Default" : spawnPointId,
            fallbackPosition = fallbackPosition,
            fallbackRotation = fallbackRotation,
            expiresAt = Time.unscaledTime + 8f
        };
    }

    public static void CancelPendingArrival()
    {
        pendingTravel.active = false;
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject managerObject = new GameObject(nameof(PortalTravelManager));
        DontDestroyOnLoad(managerObject);
        instance = managerObject.AddComponent<PortalTravelManager>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
>>>>>>> develop
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Update()
    {
<<<<<<< HEAD
        TryPlacePlayerAtPendingSpawn();
=======
        if (!pendingTravel.active)
            return;

        if (!IsTargetSceneLoaded())
            return;

        TryApplyPendingArrival();
>>>>>>> develop
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
<<<<<<< HEAD
        TryPlacePlayerAtPendingSpawn();
    }

    public static bool RequestTravel(string targetSceneSetId, string targetSpawnPointId, float cooldown = 1.5f)
    {
        if (Time.time < nextTravelAllowedTime || string.IsNullOrWhiteSpace(targetSceneSetId))
            return false;

        pendingSceneSetId = targetSceneSetId.Trim();
        pendingSpawnPointId = string.IsNullOrWhiteSpace(targetSpawnPointId) ? null : targetSpawnPointId.Trim();
        nextTravelAllowedTime = Time.time + Mathf.Max(0.25f, cooldown);

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null)
            return manager.TravelToSceneSet(pendingSceneSetId);

        MultiplayerSceneSetState targetSceneSet = MultiplayerSceneSetCatalog.ResolveStartupState(pendingSceneSetId, null);
        return targetSceneSet != null && MultiplayerSceneSetCatalog.ApplyToRuntime(targetSceneSet);
    }

    void TryPlacePlayerAtPendingSpawn()
    {
        if (string.IsNullOrWhiteSpace(pendingSceneSetId))
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        MultiplayerSceneSetState currentSceneSet = manager != null
            ? manager.CaptureCurrentSceneSet()
            : MultiplayerSceneSetCatalog.CaptureLoadedScenes();

        if (currentSceneSet == null)
            return;

        string currentSceneSetId = string.IsNullOrWhiteSpace(currentSceneSet.sceneSetId)
            ? currentSceneSet.activeSceneName
            : currentSceneSet.sceneSetId;

        if (!string.Equals(currentSceneSetId, pendingSceneSetId, System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentSceneSet.activeSceneName, pendingSceneSetId, System.StringComparison.OrdinalIgnoreCase))
            return;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null)
            return;

        PortalSpawnPoint spawnPoint = PortalSpawnPoint.FindById(pendingSpawnPointId);
        if (spawnPoint == null)
        {
            pendingSceneSetId = null;
            pendingSpawnPointId = null;
            return;
        }

        player.WarpToPosition(spawnPoint.transform.position, spawnPoint.transform.rotation, true);
        pendingSceneSetId = null;
        pendingSpawnPointId = null;
=======
        if (!pendingTravel.active)
            return;

        if (!string.IsNullOrWhiteSpace(pendingTravel.sceneName) &&
            !string.Equals(scene.name, pendingTravel.sceneName, System.StringComparison.Ordinal))
            return;

        TryApplyPendingArrival();
    }

    void TryApplyPendingArrival()
    {
        if (!pendingTravel.active)
            return;

        if (Time.unscaledTime > pendingTravel.expiresAt)
        {
            pendingTravel.active = false;
            MessageSystem.Instance?.ShowMessage("Nao foi possivel encontrar um ponto seguro para o portal.");
            return;
        }

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
            return;

        Vector3 desiredPosition = pendingTravel.fallbackPosition;
        Quaternion desiredRotation = pendingTravel.fallbackRotation;

        if (TryFindSpawnPoint(pendingTravel.sceneName, pendingTravel.spawnPointId, out PortalSpawnPoint spawnPoint))
        {
            desiredPosition = spawnPoint.transform.position;
            desiredRotation = spawnPoint.transform.rotation;
        }
        else if (!TryFindFallbackGround(out desiredPosition))
        {
            return;
        }

        if (!player.WarpToSafePosition(desiredPosition, desiredRotation))
            return;

        pendingTravel.active = false;
    }

    bool IsTargetSceneLoaded()
    {
        if (string.IsNullOrWhiteSpace(pendingTravel.sceneName))
            return true;

        Scene targetScene = SceneManager.GetSceneByName(pendingTravel.sceneName);
        return targetScene.IsValid() && targetScene.isLoaded;
    }

    bool TryFindSpawnPoint(string sceneName, string spawnPointId, out PortalSpawnPoint spawnPoint)
    {
        PortalSpawnPoint[] points = FindObjectsByType<PortalSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
                continue;

            if (!string.IsNullOrWhiteSpace(sceneName) &&
                !string.Equals(points[i].gameObject.scene.name, sceneName, System.StringComparison.Ordinal))
                continue;

            if (!points[i].Matches(spawnPointId))
                continue;

            spawnPoint = points[i];
            return true;
        }

        spawnPoint = null;
        return false;
    }

    bool TryFindFallbackGround(out Vector3 fallbackPosition)
    {
        Vector3[] samples =
        {
            Vector3.zero,
            new Vector3(25f, 0f, 0f),
            new Vector3(-25f, 0f, 0f),
            new Vector3(0f, 0f, 25f),
            new Vector3(0f, 0f, -25f),
            new Vector3(40f, 0f, 40f),
            new Vector3(-40f, 0f, 40f),
            new Vector3(40f, 0f, -40f),
            new Vector3(-40f, 0f, -40f)
        };

        for (int i = 0; i < samples.Length; i++)
        {
            Vector3 rayOrigin = samples[i] + Vector3.up * 200f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                continue;

            fallbackPosition = hit.point;
            return true;
        }

        fallbackPosition = pendingTravel.fallbackPosition;
        return fallbackPosition != Vector3.zero;
>>>>>>> develop
    }
}
