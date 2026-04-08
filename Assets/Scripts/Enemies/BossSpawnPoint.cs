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

        Vector3 spawnPosition = GetGroundedSpawnPosition();
        spawnedBoss = Instantiate(bossPrefab, spawnPosition + Vector3.up * 0.5f, transform.rotation);
        spawnedBoss.name = bossPrefab.name;
        LanNetworkEntity.Ensure(spawnedBoss.transform, BuildBossEntityId());
    }

    void SpawnPlayerIfNeeded()
    {
        if (playerPrefab == null || spawnedPlayer != null || FindFirstObjectByType<PlayerMovement>() != null)
            return;

        Vector3 desiredPosition = transform.position + playerSpawnOffset;
        Vector3 groundedPosition = GetGroundedPositionOrFallback(desiredPosition);
        spawnedPlayer = Instantiate(playerPrefab, groundedPosition + Vector3.up * 0.5f, Quaternion.identity);
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

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return position;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.85f, 0.18f, 0.18f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
    }
}
