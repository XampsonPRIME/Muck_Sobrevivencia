using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestMushroomMonsterSpawnPoint : MonoBehaviour
{
    class SpawnRandom
    {
        readonly System.Random random;

        public SpawnRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public float Value()
        {
            return (float)random.NextDouble();
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, Value());
        }

        public Vector2 InsideUnitCircle()
        {
            float angle = Value() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Value());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }

    public GameObject mushroomMonsterPrefab;
    public int enemiesPerGroup = 1;
    public float spawnRadius = 4.5f;
    public float respawnDelay = 40f;

    readonly Dictionary<int, MiniKrug> activeEnemies = new Dictionary<int, MiniKrug>();
    Coroutine respawnRoutine;

    void Start()
    {
        SpawnMissingEnemies();
    }

    public void NotifyEnemyDeath(MiniKrug enemy)
    {
        if (enemy == null)
            return;

        int keyToRemove = -1;
        foreach (KeyValuePair<int, MiniKrug> entry in activeEnemies)
        {
            if (entry.Value == enemy)
            {
                keyToRemove = entry.Key;
                break;
            }
        }

        if (keyToRemove >= 0)
            activeEnemies.Remove(keyToRemove);

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnMissingEnemies();
        respawnRoutine = null;
    }

    void SpawnMissingEnemies()
    {
        CleanupDeadEntries();

        if (mushroomMonsterPrefab == null)
            mushroomMonsterPrefab = ForestMushroomMonsterFactory.LoadPrefab();

        if (mushroomMonsterPrefab == null)
        {
            Debug.LogWarning("Prefab do Forest Mushroom Monster nao encontrado.", this);
            return;
        }

        for (int spawnIndex = 0; spawnIndex < Mathf.Max(1, enemiesPerGroup); spawnIndex++)
        {
            if (activeEnemies.ContainsKey(spawnIndex))
                continue;

            string entityId = BuildEntityId(spawnIndex);
            MiniKrug existingEnemy = FindExistingEnemy(entityId);
            if (existingEnemy != null)
            {
                existingEnemy.SetDeathCallback(NotifyEnemyDeath);
                activeEnemies[spawnIndex] = existingEnemy;
                continue;
            }

            int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
            int seed = BuildSpawnSeed(worldSeed, spawnIndex);
            SpawnRandom rng = new SpawnRandom(seed);
            Vector2 offset2D = rng.InsideUnitCircle() * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            MiniKrug enemy = ForestMushroomMonsterFactory.CreateInstance(
                mushroomMonsterPrefab,
                spawnPos,
                Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                transform);
            if (enemy == null)
                continue;

            enemy.SetEnemyLevel(DetermineEnemyLevel(spawnPos));
            enemy.SetDeathCallback(NotifyEnemyDeath);
            LanNetworkEntity.Ensure(enemy, entityId);
            activeEnemies[spawnIndex] = enemy;
        }
    }

    int DetermineEnemyLevel(Vector3 spawnPosition)
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.TryGetSuggestedEnemyLevel(spawnPosition, out int networkLevel))
            return networkLevel;

        PlayerMovement[] players = LanMultiplayerManager.GetGameplayPlayers();
        float bestDistance = float.MaxValue;
        PlayerProgression closestProgression = null;

        foreach (PlayerMovement player in players)
        {
            if (player == null)
                continue;

            float distance = (player.transform.position - spawnPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closestProgression = player.GetComponent<PlayerProgression>();
            }
        }

        return Mathf.Max(1, closestProgression != null ? closestProgression.currentLevel : 1);
    }

    int BuildSpawnSeed(int worldSeed, int spawnIndex)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + worldSeed;
            hash = (hash * 31) + Mathf.RoundToInt(transform.position.x * 100f);
            hash = (hash * 31) + Mathf.RoundToInt(transform.position.z * 100f);
            hash = (hash * 31) + spawnIndex;
            return hash;
        }
    }

    string BuildEntityId(int spawnIndex)
    {
        return $"{ForestMushroomMonsterFactory.EntityIdPrefix}|{Mathf.RoundToInt(transform.position.x * 100f)}|{Mathf.RoundToInt(transform.position.z * 100f)}|{spawnIndex}";
    }

    MiniKrug FindExistingEnemy(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return null;

        MiniKrug[] enemies = FindObjectsByType<MiniKrug>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
                continue;

            LanNetworkEntity networkEntity = enemies[i].GetComponent<LanNetworkEntity>();
            if (networkEntity != null && networkEntity.EntityId == entityId && !enemies[i].IsPendingDestroy)
                return enemies[i];
        }

        return null;
    }

    void CleanupDeadEntries()
    {
        List<int> staleKeys = null;

        foreach (KeyValuePair<int, MiniKrug> entry in activeEnemies)
        {
            if (entry.Value == null)
            {
                staleKeys ??= new List<int>();
                staleKeys.Add(entry.Key);
            }
        }

        if (staleKeys == null)
            return;

        for (int i = 0; i < staleKeys.Count; i++)
            activeEnemies.Remove(staleKeys[i]);
    }
}
