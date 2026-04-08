using UnityEngine;

public class BossSpawnPoint : MonoBehaviour
{
    public GameObject bossPrefab;
    public bool spawnOnStart = true;
    public float groundRayHeight = 40f;
    public float groundRayDistance = 120f;
    public LayerMask groundMask = ~0;

    GameObject spawnedBoss;

    void Start()
    {
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
    }

    Vector3 GetGroundedSpawnPosition()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundRayHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return transform.position;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.85f, 0.18f, 0.18f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
    }
}
