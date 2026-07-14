using UnityEngine;
using UnityEngine.SceneManagement;

public class BossSpawnPoint : MonoBehaviour
{
    public GameObject bossPrefab;
    public GameObject playerPrefab;
    public bool spawnOnStart = true;
    public bool respawnEveryDay = true;
    public bool ensurePlayerOnStart = true;
    public float groundRayHeight = 40f;
    public float groundRayDistance = 120f;
    public LayerMask groundMask = ~0;
    public Vector3 playerSpawnOffset = new Vector3(0f, 0f, -12f);
    public bool activateOnlyInFinalRegion = true;
    public float activationDistance = DemoWorldProgression.FinalBossActivationDistance;

    GameObject spawnedBoss;
    GameObject spawnedPlayer;
    bool initialBossSpawnHandled;
    float nextFinalRegionCheckTime;
    const float FinalRegionCheckInterval = 0.5f;

    void OnEnable()
    {
        DayNightCycle.DayStarted += HandleDayStarted;
    }

    void OnDisable()
    {
        DayNightCycle.DayStarted -= HandleDayStarted;
    }

    void Start()
    {
        DemoWorldProgression.ApplyLandmarkLayout(gameObject.scene);
        UpdateFinalLandmarkVisibility();

        if (ensurePlayerOnStart)
            SpawnPlayerIfNeeded();

        TryHandleInitialBossSpawn();
    }

    void Update()
    {
        if (Time.time < nextFinalRegionCheckTime)
            return;

        nextFinalRegionCheckTime = Time.time + FinalRegionCheckInterval;
        UpdateFinalLandmarkVisibility();
        TryHandleInitialBossSpawn();
    }

    void TryHandleInitialBossSpawn()
    {
        if (initialBossSpawnHandled || GameState.IsInLobby || !HasReadyGameplaySession())
            return;

        if (!CanActivateFinalBoss())
            return;

        initialBossSpawnHandled = true;

        if (!spawnOnStart || bossPrefab == null || !ShouldRunAuthority())
            return;

        SpawnBoss();
    }

    bool HasReadyGameplaySession()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        return manager == null ||
               manager.Mode != LanMultiplayerManager.SessionMode.None &&
               manager.IsSessionReady;
    }

    public void SpawnBoss()
    {
        if (spawnedBoss != null || bossPrefab == null)
            return;

        BossEnemy[] existingBosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        if (existingBosses.Length > 0)
        {
            BossEnemy primaryBoss = existingBosses[0];
            spawnedBoss = primaryBoss.gameObject;
            spawnedBoss.name = bossPrefab.name;
            LanNetworkEntity.Ensure(primaryBoss.transform, BuildBossEntityId());

            for (int i = 1; i < existingBosses.Length; i++)
            {
                if (existingBosses[i] != null)
                    Destroy(existingBosses[i].gameObject);
            }

            return;
        }

        Vector3 spawnPosition = GetGroundedSpawnPosition();
        Vector3 finalSpawnPosition = spawnPosition + Vector3.up * 0.5f;
        BossEnemyProfile bossProfile = bossPrefab.GetComponent<BossEnemyProfile>();

        if (bossProfile != null)
        {
            BossEnemy bossComponent = ForestMushroomBossFactory.CreateInstance(
                bossPrefab,
                finalSpawnPosition,
                transform.rotation);

            if (bossComponent == null)
                return;

            spawnedBoss = bossComponent.gameObject;
            spawnedBoss.name = bossPrefab.name;
            LanNetworkEntity.Ensure(bossComponent.transform, BuildBossEntityId());
            return;
        }

        spawnedBoss = Instantiate(bossPrefab, finalSpawnPosition, transform.rotation);
        spawnedBoss.name = bossPrefab.name;
        LanNetworkEntity.Ensure(spawnedBoss.transform, BuildBossEntityId());
    }

    void HandleDayStarted(int day)
    {
        if (!respawnEveryDay || bossPrefab == null || !ShouldRunAuthority())
            return;

        if (!CanActivateFinalBoss())
            return;

        if (spawnedBoss != null)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null)
            manager.ClearDestroyedEntity(BuildBossEntityId());

        SpawnBoss();
    }

    void UpdateFinalLandmarkVisibility()
    {
        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            DemoWorldProgression.UpdateLandmarkVisibility(gameObject.scene, PlayerMovement.DefaultFreshStartPosition);
            return;
        }

        DemoWorldProgression.UpdateLandmarkVisibility(gameObject.scene, player.transform.position);
    }

    bool CanActivateFinalBoss()
    {
        if (!activateOnlyInFinalRegion)
            return true;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();
        if (player == null)
            return false;

        Vector3 playerPosition = player.transform.position;
        if (!DemoWorldProgression.HasReachedFinalBossRegion(playerPosition))
            return false;

        Vector2 playerPlanar = new Vector2(playerPosition.x, playerPosition.z);
        Vector2 bossPlanar = new Vector2(transform.position.x, transform.position.z);
        float requiredDistance = Mathf.Max(1f, activationDistance);
        return Vector2.Distance(playerPlanar, bossPlanar) <= requiredDistance;
    }

    bool ShouldRunAuthority()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        return manager == null ||
               !manager.IsMultiplayerActive ||
               manager.IsServerAuthority;
    }

    void SpawnPlayerIfNeeded()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        if (LanMultiplayerManager.IsDedicatedProcessRequested ||
            (manager != null && manager.Mode == LanMultiplayerManager.SessionMode.DedicatedServer))
            return;

        if (playerPrefab == null || spawnedPlayer != null || FindFirstObjectByType<PlayerMovement>() != null)
            return;

        Vector3 desiredPosition = PlayerMovement.DefaultFreshStartPosition;
        Quaternion desiredRotation = PlayerMovement.DefaultFreshStartRotation;
        Vector3 groundedPosition = GetGroundedPositionOrFallback(desiredPosition);
        spawnedPlayer = Instantiate(playerPrefab, groundedPosition + Vector3.up * 0.5f, desiredRotation);
        spawnedPlayer.name = playerPrefab.name;
    }

    string BuildBossEntityId()
    {
        return $"{SceneManager.GetActiveScene().name}|BossSpawn|{BuildTransformPath(transform)}";
    }

    static string BuildTransformPath(Transform current)
    {
        if (current == null)
            return "null";

        string path = current.name;
        Transform cursor = current.parent;

        while (cursor != null)
        {
            path = $"{cursor.name}/{path}";
            cursor = cursor.parent;
        }

        return path;
    }

    Vector3 GetGroundedSpawnPosition()
    {
        return GetGroundedPositionOrFallback(transform.position);
    }

    Vector3 GetGroundedPositionOrFallback(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * groundRayHeight;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        Vector3 groundedPosition = position;

        foreach (RaycastHit hit in hits)
        {
            if (!IsValidGroundHit(hit))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundedPosition = hit.point;
            }
        }

        if (closestDistance < float.MaxValue)
            return groundedPosition;

        return position;
    }

    bool IsValidGroundHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null || hit.normal.y < 0.35f)
            return false;

        if (collider.GetComponentInParent<Cow>() != null)
            return false;

        if (collider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (collider.GetComponentInParent<BossEnemy>() != null)
            return false;

        if (collider.GetComponentInParent<PlayerMovement>() != null)
            return false;

        if (collider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.85f, 0.18f, 0.18f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
    }
}
