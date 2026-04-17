using UnityEngine;
using UnityEngine.SceneManagement;

public class BossSpawnPoint : MonoBehaviour
{
    public GameObject bossPrefab;
    public GameObject playerPrefab;
    public bool spawnOnStart = true;
    public bool ensurePlayerOnStart = true;
    public float groundRayHeight = 40f;
    public float groundRayDistance = 120f;
    public LayerMask groundMask = ~0;
    public Vector3 playerSpawnOffset = new Vector3(0f, 0f, -12f);

    GameObject spawnedBoss;
    GameObject spawnedPlayer;

    void Start()
    {
        if (ensurePlayerOnStart)
            SpawnPlayerIfNeeded();

        if (!spawnOnStart || bossPrefab == null)
            return;

        SpawnBoss();
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

    void SpawnPlayerIfNeeded()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        if (LanMultiplayerManager.IsDedicatedProcessRequested ||
            (manager != null && manager.Mode == LanMultiplayerManager.SessionMode.DedicatedServer))
            return;

        if (playerPrefab == null || spawnedPlayer != null || FindFirstObjectByType<PlayerMovement>() != null)
            return;

        Vector3 desiredPosition = transform.position + playerSpawnOffset;
        Vector3 groundedPosition = GetGroundedPositionOrFallback(desiredPosition);
        spawnedPlayer = Instantiate(playerPrefab, groundedPosition + Vector3.up * 0.5f, Quaternion.identity);
        spawnedPlayer.name = playerPrefab.name;

        Vector3 lookDirection = transform.position - spawnedPlayer.transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
            spawnedPlayer.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
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
