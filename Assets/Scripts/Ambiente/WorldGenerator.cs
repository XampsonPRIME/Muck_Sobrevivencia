using UnityEngine;
using System.Collections.Generic;

public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator Instance { get; private set; }

    Transform player;
    RiverSystem riverSystem;
    DistantMountains distantMountains;
    Transform worldBorderRoot;

    public GameObject chunkPrefab;
    public RiverSystem riverSystemPrefab;
    public DistantMountains distantMountainsPrefab;
    public bool enableDistantMountains = true;

    public int chunkSize = 50;
    public int viewDistance = 2;
    public int maxChunkCreationsPerFrame = 1;
    public Vector2 worldCenter = Vector2.zero;
    public Vector2 worldSize = new Vector2(800f, 800f);
    public bool createWorldBorder = true;
    public float worldBorderInset = 20f;
    public float worldBorderHeight = 120f;
    public float worldBorderThickness = 12f;

    private readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    readonly Queue<Vector2Int> pendingChunkQueue = new Queue<Vector2Int>();
    readonly HashSet<Vector2Int> queuedChunkCoords = new HashSet<Vector2Int>();
    Vector2Int lastPlayerChunk;
    bool hasLastPlayerChunk;
    bool initialized;

    void Start()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
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
            }
            else
            {
                GameObject riverObject = new GameObject("RiverSystem");
                riverSystem = riverObject.AddComponent<RiverSystem>();
            }
        }

        if (player == null)
            return;

        initialized = true;
        riverSystem.Initialize(player.position);
        SetupDistantMountains();
        BuildWorldBorder();
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
                if (!IsCoordWithinWorldBounds(coord))
                    continue;

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
        if (!IsCoordWithinWorldBounds(coord))
            return;

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

        if (!IsCoordWithinWorldBounds(coord))
            return false;

        Vector2Int playerChunk = GetPlayerChunkCoord();
        return Mathf.Abs(coord.x - playerChunk.x) <= viewDistance &&
               Mathf.Abs(coord.y - playerChunk.y) <= viewDistance;
    }

    void CreateChunk(Vector2Int coord)
    {
        Vector3 position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

        GameObject chunk = Instantiate(chunkPrefab, position, Quaternion.identity);

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

    bool IsCoordWithinWorldBounds(Vector2Int coord)
    {
        float minX = worldCenter.x - worldSize.x * 0.5f;
        float maxX = worldCenter.x + worldSize.x * 0.5f;
        float minZ = worldCenter.y - worldSize.y * 0.5f;
        float maxZ = worldCenter.y + worldSize.y * 0.5f;

        float chunkWorldX = coord.x * chunkSize;
        float chunkWorldZ = coord.y * chunkSize;

        return chunkWorldX >= minX &&
               chunkWorldX < maxX &&
               chunkWorldZ >= minZ &&
               chunkWorldZ < maxZ;
    }

    public bool TryGetPlayableBounds(out Bounds bounds)
    {
        float inset = Mathf.Max(0f, worldBorderInset);
        float innerWidth = Mathf.Max(1f, worldSize.x - inset * 2f);
        float innerDepth = Mathf.Max(1f, worldSize.y - inset * 2f);
        bounds = new Bounds(
            new Vector3(worldCenter.x, 0f, worldCenter.y),
            new Vector3(innerWidth, Mathf.Max(10f, worldBorderHeight), innerDepth)
        );
        return true;
    }

    void BuildWorldBorder()
    {
        if (!createWorldBorder)
        {
            if (worldBorderRoot != null)
                Destroy(worldBorderRoot.gameObject);
            return;
        }

        if (worldBorderRoot == null)
        {
            worldBorderRoot = new GameObject("WorldBorder").transform;
            worldBorderRoot.SetParent(transform, false);
        }

        float inset = Mathf.Max(0f, worldBorderInset);
        float thickness = Mathf.Max(1f, worldBorderThickness);
        float height = Mathf.Max(20f, worldBorderHeight);
        float halfWidth = Mathf.Max(1f, worldSize.x * 0.5f - inset);
        float halfDepth = Mathf.Max(1f, worldSize.y * 0.5f - inset);
        Vector3 center = new Vector3(worldCenter.x, height * 0.5f, worldCenter.y);

        CreateOrUpdateBorderWall("NorthWall", worldBorderRoot, new Vector3(center.x, center.y, center.z + halfDepth), new Vector3(halfWidth * 2f, height, thickness));
        CreateOrUpdateBorderWall("SouthWall", worldBorderRoot, new Vector3(center.x, center.y, center.z - halfDepth), new Vector3(halfWidth * 2f, height, thickness));
        CreateOrUpdateBorderWall("EastWall", worldBorderRoot, new Vector3(center.x + halfWidth, center.y, center.z), new Vector3(thickness, height, halfDepth * 2f));
        CreateOrUpdateBorderWall("WestWall", worldBorderRoot, new Vector3(center.x - halfWidth, center.y, center.z), new Vector3(thickness, height, halfDepth * 2f));
    }

    static void CreateOrUpdateBorderWall(string name, Transform parent, Vector3 position, Vector3 size)
    {
        Transform child = parent.Find(name);
        GameObject wallObject = child != null ? child.gameObject : new GameObject(name);
        wallObject.transform.SetParent(parent, false);
        wallObject.transform.position = position;
        wallObject.layer = 0;

        BoxCollider collider = wallObject.GetComponent<BoxCollider>();
        if (collider == null)
            collider = wallObject.AddComponent<BoxCollider>();

        collider.isTrigger = false;
        collider.size = size;
        collider.center = Vector3.zero;
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
            }
            else
            {
                GameObject mountainObject = new GameObject("DistantMountains");
                distantMountains = mountainObject.AddComponent<DistantMountains>();
            }
        }

        distantMountains.Initialize(player);
    }
}
