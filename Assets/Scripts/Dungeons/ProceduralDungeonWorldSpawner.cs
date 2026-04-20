using UnityEngine;

public class ProceduralDungeonWorldSpawner : MonoBehaviour
{
    const int CandidateSalt = 47291;
    const string EntranceResourcesPath = "Dungeons/MushroomTyrantDungeonEntrance";

    public float spawnCheckInterval = 2f;
    public float minDistanceFromPlayer = 95f;

    GameObject entrancePrefab;
    MushroomTyrantDungeon spawnedDungeon;
    float nextSpawnCheckTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<ProceduralDungeonWorldSpawner>() != null)
            return;

        GameObject root = new GameObject("Procedural Dungeon World Spawner");
        root.AddComponent<ProceduralDungeonWorldSpawner>();
    }

    void Update()
    {
        if (!ShouldRun() || spawnedDungeon != null)
            return;

        if (Time.time < nextSpawnCheckTime)
            return;

        nextSpawnCheckTime = Time.time + Mathf.Max(0.5f, spawnCheckInterval);

        if (entrancePrefab == null)
            entrancePrefab = Resources.Load<GameObject>(EntranceResourcesPath);

        if (!TryFindDungeonPosition(out Vector3 spawnPosition, out float bestScore))
            return;

        spawnedDungeon = CreateDungeon(spawnPosition, bestScore);
    }

    bool TryFindDungeonPosition(out Vector3 spawnPosition, out float bestScore)
    {
        spawnPosition = Vector3.zero;
        bestScore = float.MaxValue;
        bool found = false;

        TerrainChunk[] chunks = FindObjectsByType<TerrainChunk>(FindObjectsSortMode.None);
        for (int i = 0; i < chunks.Length; i++)
        {
            TerrainChunk chunk = chunks[i];
            if (chunk == null)
                continue;

            if (!chunk.TryGetForestBossSpawnPoint(CandidateSalt, minDistanceFromPlayer, out Vector3 candidate, out float score))
                continue;

            if (!found || score < bestScore)
            {
                spawnPosition = candidate;
                bestScore = score;
                found = true;
            }
        }

        return found;
    }

    MushroomTyrantDungeon CreateDungeon(Vector3 position, float score)
    {
        GameObject root = new GameObject("Mushroom Tyrant Dungeon");
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(score * 3600f, 360f), 0f);

        MushroomTyrantDungeon dungeon = root.AddComponent<MushroomTyrantDungeon>();
        dungeon.Initialize(entrancePrefab);
        return dungeon;
    }

    bool ShouldRun()
    {
        if (GameState.IsInLobby)
            return false;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? FindFirstObjectByType<LanMultiplayerManager>();
        return manager == null ||
               !manager.IsMultiplayerActive ||
               manager.IsServerAuthority;
    }
}
