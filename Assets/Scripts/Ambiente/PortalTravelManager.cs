using UnityEngine;
using UnityEngine.SceneManagement;

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

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Update()
    {
        TryPlacePlayerAtPendingSpawn();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
    }
}
