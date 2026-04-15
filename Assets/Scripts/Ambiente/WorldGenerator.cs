using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class WorldGenerator : MonoBehaviour
{
    Transform player;
    RiverSystem riverSystem;
    DistantMountains distantMountains;

    public GameObject chunkPrefab;
    public RiverSystem riverSystemPrefab;
    public DistantMountains distantMountainsPrefab;
    public bool enableDistantMountains = true;

    public int chunkSize = 50;
    public int viewDistance = 2;
    public int maxChunkCreationsPerFrame = 1;

    private readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    readonly Queue<Vector2Int> pendingChunkQueue = new Queue<Vector2Int>();
    readonly HashSet<Vector2Int> queuedChunkCoords = new HashSet<Vector2Int>();
    Vector2Int lastPlayerChunk;
    bool hasLastPlayerChunk;
    bool initialized;

    void Start()
    {
        player = LanMultiplayerManager.FindWorldFocusTransform();
    }

    void Update()
    {
        if (!initialized)
        {
            if (GameState.IsInLobby)
                return;

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Client &&
                !LanMultiplayerManager.Instance.IsSessionReady)
                return;

            InitializeWorld();
        }

        if (player == null)
            return;

        Vector2Int currentChunk = GetPlayerChunkCoord();
        if (!hasLastPlayerChunk || currentChunk != lastPlayerChunk)
        {
            UpdateChunkTargets(force: true);
            lastPlayerChunk = currentChunk;
            hasLastPlayerChunk = true;
        }

        ProcessPendingChunkCreates();
    }

    void InitializeWorld()
    {
        player = LanMultiplayerManager.FindWorldFocusTransform();
        riverSystem = FindFirstObjectByType<RiverSystem>();

        if (riverSystem == null)
        {
            if (riverSystemPrefab != null)
            {
                riverSystem = Instantiate(riverSystemPrefab, Vector3.zero, Quaternion.identity);
                riverSystem.name = "RiverSystem";
                SceneManager.MoveGameObjectToScene(riverSystem.gameObject, gameObject.scene);
            }
            else
            {
                GameObject riverObject = new GameObject("RiverSystem");
                riverSystem = riverObject.AddComponent<RiverSystem>();
                SceneManager.MoveGameObjectToScene(riverObject, gameObject.scene);
            }
        }

        if (player == null)
            return;

        initialized = true;
        riverSystem.Initialize(player.position);
        SetupDistantMountains();
        UpdateChunkTargets(force: true);
    }

    Vector2Int GetPlayerChunkCoord()
    {
        return new Vector2Int(
            Mathf.RoundToInt(player.position.x / chunkSize),
            Mathf.RoundToInt(player.position.z / chunkSize));
    }

    void UpdateChunkTargets(bool force = false)
    {
        if (player == null)
        {
            Debug.LogError("PLAYER NÃO ATRIBUÍDO!");
            return;
        }

        Vector2Int playerChunk = GetPlayerChunkCoord();

        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector2Int coord = new Vector2Int(playerChunk.x + x, playerChunk.y + z);
                neededChunks.Add(coord);

                if (!chunks.ContainsKey(coord) && !queuedChunkCoords.Contains(coord))
                    QueueChunk(coord);
            }
        }

        List<Vector2Int> toRemove = new List<Vector2Int>();

        foreach (var chunk in chunks)
        {
            if (!neededChunks.Contains(chunk.Key))
            {
                Destroy(chunk.Value);
                toRemove.Add(chunk.Key);
            }
        }

        foreach (var coord in toRemove)
            chunks.Remove(coord);

        if (force)
            lastPlayerChunk = playerChunk;
    }

    void QueueChunk(Vector2Int coord)
    {
        pendingChunkQueue.Enqueue(coord);
        queuedChunkCoords.Add(coord);
    }

    void ProcessPendingChunkCreates()
    {
        int chunkBudget = Mathf.Max(1, maxChunkCreationsPerFrame);
        int created = 0;

        while (created < chunkBudget && pendingChunkQueue.Count > 0)
        {
            Vector2Int coord = pendingChunkQueue.Dequeue();
            queuedChunkCoords.Remove(coord);

            if (chunks.ContainsKey(coord) || !IsChunkStillNeeded(coord))
                continue;

            CreateChunk(coord);
            created++;
        }
    }

    bool IsChunkStillNeeded(Vector2Int coord)
    {
        if (player == null)
            return false;

        Vector2Int playerChunk = GetPlayerChunkCoord();
        return Mathf.Abs(coord.x - playerChunk.x) <= viewDistance &&
               Mathf.Abs(coord.y - playerChunk.y) <= viewDistance;
    }

    void CreateChunk(Vector2Int coord)
    {
        Vector3 position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

        GameObject chunk = Instantiate(chunkPrefab, position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(chunk, gameObject.scene);

        TerrainChunk terrain = chunk.GetComponent<TerrainChunk>();

        if (terrain != null)
        {
            terrain.Generate(new Vector2(position.x, position.z));
        }
        else
        {
            Debug.LogError("Prefab NÃO tem TerrainChunk!");
        }

        chunks.Add(coord, chunk);
    }

    void SetupDistantMountains()
    {
        if (!enableDistantMountains || player == null)
            return;

        distantMountains = FindFirstObjectByType<DistantMountains>();
        if (distantMountains == null)
        {
            if (distantMountainsPrefab != null)
            {
                distantMountains = Instantiate(distantMountainsPrefab, Vector3.zero, Quaternion.identity);
                distantMountains.name = "DistantMountains";
                SceneManager.MoveGameObjectToScene(distantMountains.gameObject, gameObject.scene);
            }
            else
            {
                GameObject mountainObject = new GameObject("DistantMountains");
                distantMountains = mountainObject.AddComponent<DistantMountains>();
                SceneManager.MoveGameObjectToScene(mountainObject, gameObject.scene);
            }
        }

        distantMountains.Initialize(player);
    }
}
