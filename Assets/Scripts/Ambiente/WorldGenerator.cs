using UnityEngine;
using System.Collections.Generic;

public class WorldGenerator : MonoBehaviour
{
    Transform player;

    public GameObject chunkPrefab;

    public int chunkSize = 30; // 🔥 menor = mais responsivo
    public int viewDistance = 2;

    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();

    void Start()
    {
        player = FindFirstObjectByType<PlayerMovement>().transform;
    }

    void Update()
    {
        GenerateChunksAroundPlayer();
    }

    void GenerateChunksAroundPlayer()
    {
        if (player == null)
        {
            Debug.LogError("PLAYER NÃO ATRIBUÍDO!");
            return;
        }

        int playerChunkX = Mathf.RoundToInt(player.position.x / chunkSize);
        int playerChunkZ = Mathf.RoundToInt(player.position.z / chunkSize);

        Debug.Log("Player Pos: " + player.position);
        Debug.Log("Chunk X/Z: " + playerChunkX + " / " + playerChunkZ);
        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector2Int coord = new Vector2Int(playerChunkX + x, playerChunkZ + z);
                neededChunks.Add(coord);

                if (!chunks.ContainsKey(coord))
                {
                    CreateChunk(coord);
                }
            }
        }

        // 🔥 remover chunks longe
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
        {
            chunks.Remove(coord);
        }
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

        Debug.Log("Criou chunk: " + coord);
    }
}