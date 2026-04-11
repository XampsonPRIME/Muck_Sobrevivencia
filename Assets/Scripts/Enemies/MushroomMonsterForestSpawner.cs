using System.Collections.Generic;
using UnityEngine;

public class MushroomMonsterForestSpawner : MonoBehaviour
{
    public GameObject mushroomMonsterPrefab;
    public int spawnCount = 6;
    public float minSpawnSpacing = 26f;
    public float preferredSpawnDistance = 95f;
    public float maxPreferredSpawnDistance = 210f;
    public float groundRayHeight = 80f;
    public float groundRayDistance = 180f;
    public float edgePadding = 60f;
    public float searchRadiusStep = 14f;
    public int spawnSearchAttempts = 320;

    readonly List<MushroomMonsterEnemy> activeMonsters = new List<MushroomMonsterEnemy>();
    readonly List<Vector3> fixedSpawnPositions = new List<Vector3>();

    bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<MushroomMonsterForestSpawner>() != null)
            return;

        GameObject spawnerObject = new GameObject("Mushroom Monster Forest Spawner");
        spawnerObject.AddComponent<MushroomMonsterForestSpawner>();
    }

    void Start()
    {
        if (!ShouldRunAuthority())
            return;

        TryInitialize();
    }

    void Update()
    {
        if (!ShouldRunAuthority())
            return;

        if (!initialized)
            TryInitialize();
    }

    public void NotifyMonsterDestroyed(MushroomMonsterEnemy monster)
    {
        activeMonsters.Remove(monster);
    }

    void TryInitialize()
    {
        if (initialized || WorldGenerator.Instance == null)
            return;

        if (mushroomMonsterPrefab == null)
            mushroomMonsterPrefab = Resources.Load<GameObject>("Enemies/MushroomMonster");

        if (mushroomMonsterPrefab == null)
            return;

        if (!GenerateFixedSpawnPositions())
            return;

        for (int i = 0; i < fixedSpawnPositions.Count; i++)
            SpawnMonsterAt(fixedSpawnPositions[i], i);

        initialized = true;
    }

    bool GenerateFixedSpawnPositions()
    {
        fixedSpawnPositions.Clear();

        if (!WorldGenerator.Instance.TryGetPlayableBounds(out Bounds playableBounds))
            return false;

        int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
        System.Random random = new System.Random(3817 + worldSeed * 17);
        Vector3 searchOrigin = LanMultiplayerManager.FindWorldFocusTransform() != null
            ? LanMultiplayerManager.FindWorldFocusTransform().position
            : new Vector3(playableBounds.center.x, 0f, playableBounds.center.z);

        float minX = playableBounds.min.x + edgePadding;
        float maxX = playableBounds.max.x - edgePadding;
        float minZ = playableBounds.min.z + edgePadding;
        float maxZ = playableBounds.max.z - edgePadding;
        float spacingSqr = minSpawnSpacing * minSpawnSpacing;

        TryGenerateNearbyForestSpawns(searchOrigin, playableBounds, spacingSqr, worldSeed);

        for (int attempt = 0; attempt < spawnSearchAttempts && fixedSpawnPositions.Count < spawnCount; attempt++)
        {
            float candidateX = Mathf.Lerp(minX, maxX, (float)random.NextDouble());
            float candidateZ = Mathf.Lerp(minZ, maxZ, (float)random.NextDouble());
            Vector3 candidate = new Vector3(candidateX, 0f, candidateZ);

            TryAddSpawnPoint(candidate, playableBounds, spacingSqr);
        }

    
        return fixedSpawnPositions.Count > 0;
    }

    void SpawnMonsterAt(Vector3 spawnPosition, int spawnIndex)
    {
        GameObject monsterObject = Instantiate(mushroomMonsterPrefab, spawnPosition, Quaternion.Euler(0f, (spawnIndex * 57f) % 360f, 0f));
        monsterObject.name = $"MushroomMonster_{spawnIndex + 1}";

        MushroomMonsterEnemy monster = monsterObject.GetComponent<MushroomMonsterEnemy>();
        if (monster == null)
            monster = monsterObject.AddComponent<MushroomMonsterEnemy>();

        LanNetworkEntity.Ensure(monster);
        monster.SetSpawnOwner(this);
        activeMonsters.Add(monster);
    }

    bool TryFindGround(Vector3 candidate, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = candidate + Vector3.up * groundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, groundRayDistance, ~0, QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        groundedPosition = candidate;
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundedPosition = hit.point;
                foundGround = true;
            }
        }

        return foundGround;
    }

    void TryGenerateNearbyForestSpawns(Vector3 searchOrigin, Bounds playableBounds, float spacingSqr, int worldSeed)
    {
        float baseAngle = Mathf.Repeat(worldSeed * 37.17f, 360f);

        for (int i = 0; i < spawnCount * 6 && fixedSpawnPositions.Count < spawnCount; i++)
        {
            float t = i / (float)Mathf.Max(1, spawnCount * 6 - 1);
            float radius = Mathf.Lerp(preferredSpawnDistance, maxPreferredSpawnDistance, t);
            float angle = baseAngle + i * 57.5f;
            Vector3 candidate = searchOrigin + Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * radius);
            TryAddSpawnPoint(candidate, playableBounds, spacingSqr);
        }
    }

    void TryAddSpawnPoint(Vector3 candidate, Bounds playableBounds, float spacingSqr)
    {
        if (!playableBounds.Contains(new Vector3(candidate.x, playableBounds.center.y, candidate.z)))
            return;

        if (!IsForestPoint(candidate))
            return;

        if (RoadSystem.IsReserved(candidate))
            return;

        if (RiverSystem.Instance != null && RiverSystem.Instance.IsRiverZone(new Vector2(candidate.x, candidate.z), true, 6f))
            return;

        for (int i = 0; i < fixedSpawnPositions.Count; i++)
        {
            if ((fixedSpawnPositions[i] - candidate).sqrMagnitude < spacingSqr)
                return;
        }

        if (!TryFindGround(candidate, out Vector3 groundedPosition))
            return;

        fixedSpawnPositions.Add(groundedPosition);
    }

    bool IsValidGroundHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null || hit.normal.y < 0.35f)
            return false;

        if (collider.GetComponentInParent<PlayerMovement>() != null)
            return false;

        if (collider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        if (collider.GetComponentInParent<Cow>() != null)
            return false;

        if (collider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (collider.GetComponentInParent<BossEnemy>() != null)
            return false;

        if (collider.GetComponentInParent<MushroomMonsterEnemy>() != null)
            return false;

        return true;
    }

    bool IsForestPoint(Vector3 point)
    {
        float biomeValue = Mathf.PerlinNoise(point.x * 0.003f, point.z * 0.003f);
        return biomeValue >= 0.33f && biomeValue < 0.66f;
    }

    bool ShouldRunAuthority()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        return manager == null ||
               !manager.IsMultiplayerActive ||
               manager.IsServerAuthority;
    }
}
