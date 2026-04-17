using UnityEngine;
using UnityEngine.SceneManagement;

public class ForestMushroomBossWorldSpawner : MonoBehaviour
{
    public float spawnCheckInterval = 2f;
    public float minDistanceFromPlayer = 70f;
    public int candidateSalt = 911;

    GameObject bossPrefab;
    BossEnemy spawnedBoss;
    float nextSpawnCheckTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<ForestMushroomBossWorldSpawner>() != null)
            return;

        GameObject spawnerObject = new GameObject("Forest Mushroom Boss World Spawner");
        spawnerObject.AddComponent<ForestMushroomBossWorldSpawner>();
    }

    void Update()
    {
        if (!ShouldRunAuthority() || SceneHasManualBossSpawner())
            return;

        if (spawnedBoss == null)
            spawnedBoss = FindExistingBoss();

        if (spawnedBoss != null)
            return;

        if (Time.time < nextSpawnCheckTime)
            return;

        nextSpawnCheckTime = Time.time + spawnCheckInterval;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.IsEntityDestroyed(ForestMushroomBossFactory.EntityId))
            return;

        if (bossPrefab == null)
            bossPrefab = ForestMushroomBossFactory.LoadPrefab();

        if (bossPrefab == null)
            return;

        if (!TryFindSpawnPosition(out Vector3 spawnPosition))
            return;

        spawnedBoss = ForestMushroomBossFactory.CreateInstance(
            bossPrefab,
            spawnPosition,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        if (spawnedBoss == null)
            return;

        LanNetworkEntity.Ensure(spawnedBoss.transform, ForestMushroomBossFactory.EntityId);
    }

    bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        float bestScore = float.MaxValue;
        bool foundCandidate = false;

        TerrainChunk[] chunks = FindObjectsByType<TerrainChunk>(FindObjectsSortMode.None);
        for (int i = 0; i < chunks.Length; i++)
        {
            TerrainChunk chunk = chunks[i];
            if (chunk == null)
                continue;

            if (!chunk.TryGetForestBossSpawnPoint(candidateSalt, minDistanceFromPlayer, out Vector3 candidate, out float score))
                continue;

            if (!foundCandidate || score < bestScore)
            {
                bestScore = score;
                spawnPosition = candidate;
                foundCandidate = true;
            }
        }

        return foundCandidate;
    }

    BossEnemy FindExistingBoss()
    {
        BossEnemy[] bosses = FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null)
                continue;

            LanNetworkEntity entity = bosses[i].GetComponent<LanNetworkEntity>();
            if (entity != null && entity.EntityId == ForestMushroomBossFactory.EntityId)
                return bosses[i];
        }

        return null;
    }

    bool SceneHasManualBossSpawner()
    {
        return FindFirstObjectByType<BossSpawnPoint>() != null ||
               string.Equals(SceneManager.GetActiveScene().name, "Boss1", System.StringComparison.Ordinal);
    }

    bool ShouldRunAuthority()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        return manager == null ||
               !manager.IsMultiplayerActive ||
               manager.IsServerAuthority;
    }
}
