using System.Collections.Generic;
using UnityEngine;

public class MiniKrugSpawnPoint : MonoBehaviour
{
    public GameObject miniKrugPrefab;
    public int krugsPerWave = 4;
    public float minSpawnDistanceFromPlayer = 18f;
    public float maxSpawnDistanceFromPlayer = 30f;
    public float respawnCheckInterval = 2f;

    readonly List<MiniKrug> activeMiniKrugs = new List<MiniKrug>();

    bool wasNight;
    float nextRespawnCheckTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<MiniKrugSpawnPoint>() != null)
            return;

        GameObject spawnerObject = new GameObject("MiniKrug Night Spawner");
        spawnerObject.AddComponent<MiniKrugSpawnPoint>();
    }

    void Start()
    {
        if (!ShouldRunAuthority())
            return;

        if (miniKrugPrefab == null)
            miniKrugPrefab = FindMiniKrugPrefab();

        wasNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;

        if (wasNight)
            SpawnMissingNightKrugs();
    }

    void Update()
    {
        if (!ShouldRunAuthority())
            return;

        DayNightCycle cycle = DayNightCycle.Instance;
        if (cycle == null)
            return;

        bool isNight = cycle.IsNight;

        if (isNight && !wasNight)
        {
            DespawnAllMiniKrugs();
            SpawnMissingNightKrugs();
        }
        else if (!isNight && wasNight)
        {
            DespawnAllMiniKrugs();
        }
        else if (isNight && Time.time >= nextRespawnCheckTime)
        {
            nextRespawnCheckTime = Time.time + respawnCheckInterval;
            SpawnMissingNightKrugs();
        }

        wasNight = isNight;
    }

    public void NotifyMiniKrugDeath(MiniKrug miniKrug)
    {
        activeMiniKrugs.Remove(miniKrug);
    }

    void SpawnMissingNightKrugs()
    {
        CleanupDeadEntries();

        if (miniKrugPrefab == null)
            miniKrugPrefab = FindMiniKrugPrefab();

        if (miniKrugPrefab == null)
        {
            Debug.LogWarning("Prefab do MiniKrug nao encontrado para spawn noturno.", this);
            return;
        }

        PlayerMovement player = FindClosestPlayerToSpawner();
        if (player == null)
            return;

        while (activeMiniKrugs.Count < krugsPerWave)
        {
            Vector3 spawnPos = FindSpawnPositionAroundPlayer(player.transform.position);

            GameObject miniKrugObject = Instantiate(
                miniKrugPrefab,
                spawnPos,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            MiniKrug miniKrug = miniKrugObject.GetComponent<MiniKrug>();
            if (miniKrug == null)
                miniKrug = miniKrugObject.AddComponent<MiniKrug>();

            miniKrug.SetSpawnData(this);
            LanNetworkEntity.Ensure(miniKrug);
            activeMiniKrugs.Add(miniKrug);
        }
    }

    PlayerMovement FindClosestPlayerToSpawner()
    {
        PlayerMovement[] players = LanMultiplayerManager.GetGameplayPlayers();
        float bestDistance = float.MaxValue;
        PlayerMovement bestPlayer = null;

        foreach (PlayerMovement player in players)
        {
            if (player == null)
                continue;

            float distance = (player.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPlayer = player;
            }
        }

        return bestPlayer;
    }

    Vector3 FindSpawnPositionAroundPlayer(Vector3 playerPosition)
    {
        Vector2 direction2D = Random.insideUnitCircle.normalized;
        if (direction2D == Vector2.zero)
            direction2D = Vector2.right;

        float distance = Random.Range(minSpawnDistanceFromPlayer, maxSpawnDistanceFromPlayer);
        Vector3 candidate = playerPosition + new Vector3(direction2D.x, 0f, direction2D.y) * distance;
        candidate.y = playerPosition.y + 15f;

        if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return new Vector3(candidate.x, playerPosition.y, candidate.z);
    }

    GameObject FindMiniKrugPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>("Enemies/MiniKrug");
        if (prefab != null)
            return prefab;

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in objects)
        {
            if (obj == null || obj.name != "MiniKrug")
                continue;

            if (obj.GetComponent<MiniKrug>() != null)
                return obj;
        }

        return null;
    }

    void DespawnAllMiniKrugs()
    {
        CleanupDeadEntries();

        foreach (MiniKrug miniKrug in activeMiniKrugs)
        {
            if (miniKrug != null)
            {
                LanMultiplayerManager.Instance?.NotifyEnemyDestroyed(miniKrug);
                Destroy(miniKrug.gameObject);
            }
        }

        activeMiniKrugs.Clear();
    }

    void CleanupDeadEntries()
    {
        activeMiniKrugs.RemoveAll(miniKrug => miniKrug == null);
    }

    bool ShouldRunAuthority()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        return manager == null ||
               !manager.IsMultiplayerActive ||
               manager.IsServerAuthority;
    }
}
