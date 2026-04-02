using UnityEngine;
using System.Collections.Generic;

public class TerrainChunk : MonoBehaviour
{
    public enum BiomeType
    {
        Desert,
        Forest,
        Snow
    }

    [System.Serializable]
    public class TreeData
    {
        public GameObject prefab;
        public float yOffset;
        public BiomeType biome;
    }

    [Header("Config")]
    public int size = 50;
    public float terrainScale = 40f;
    public float heightMultiplier = 12f;

    public Transform player;
    public float safeZoneRadius = 10f;

    [Header("🌍 Bioma")]
    public float biomeScale = 0.003f;

    [Header("Vegetação")]
    public TreeData[] trees;
    public GameObject mushroomPrefab;

    public float treeDensity = 0.003f;
    public float mushroomDensity = 0.001f;
    public int rockClusterCount = 3;

    public float minDistanceBetweenObjects = 15f;

    [Header("Rochas")]
    public GameObject rockSmallPrefab;
    public GameObject rockMediumPrefab;
    public GameObject rockLargePrefab;


    [Header("Material")]
    public Material terrainMaterial;

    bool alreadyGenerated = false;

    public float safeRadius = 12f;
    public float forwardSafeDistance = 8f;

    public float rockDensity = 0.5f; // base

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    Color[] colors;
    Vector2[] uvs;

    List<Vector3> usedPositions = new List<Vector3>();

    public void Generate(Vector2 offset)
    {
        if (alreadyGenerated)
            return;

        alreadyGenerated = true;

        if (player == null)
            player = FindFirstObjectByType<PlayerMovement>()?.transform;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = terrainMaterial;

        vertices = new Vector3[(size + 1) * (size + 1)];
        colors = new Color[vertices.Length];
        uvs = new Vector2[vertices.Length];

        for (int z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                int i = z * (size + 1) + x;

                float worldX = x + offset.x;
                float worldZ = z + offset.y;

                Vector2 point = new Vector2(worldX, worldZ);

                float h = GetHeight(point);
                vertices[i] = new Vector3(x, h, z);

                uvs[i] = new Vector2(x / (float)size, z / (float)size);

                // 🌍 BIOMA
                BiomeType biome = GetBiome(point);

                switch (biome)
                {
                    case BiomeType.Desert:
                        colors[i] = new Color(0, 0, 1); // areia
                        break;

                    case BiomeType.Forest:
                        colors[i] = new Color(0, 1, 0); // grama
                        break;

                    case BiomeType.Snow:
                        colors[i] = new Color(1, 0, 0); // neve
                        break;
                }
            }
        }

        BuildMesh();
        SpawnVegetation(offset);
        SpawnRockClusters(offset);
    }

    BiomeType GetBiome(Vector2 point)
    {
        float biome = Mathf.PerlinNoise(point.x * biomeScale, point.y * biomeScale);

        if (biome < 0.33f)
            return BiomeType.Desert;

        if (biome < 0.66f)
            return BiomeType.Forest;

        return BiomeType.Snow;
    }

    bool IsInPlayerPath(Vector3 pos)
    {
        Vector3 toObject = (pos - player.position).normalized;
        float dot = Vector3.Dot(player.forward, toObject);

        // 🔥 1 = na frente, 0 = lado, -1 = atrás
        return dot > 0.5f;
    }

    float GetHeight(Vector2 point)
    {
        float h = Mathf.PerlinNoise(point.x / terrainScale, point.y / terrainScale) * heightMultiplier;
        h += Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f) * 2f;
        return h;
    }

    void BuildMesh()
    {
        triangles = new int[size * size * 6];

        int vert = 0;
        int tris = 0;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                triangles[tris] = vert;
                triangles[tris + 1] = vert + size + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + size + 1;
                triangles[tris + 5] = vert + size + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.uv = uvs;

        mesh.RecalculateNormals();

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    // 🌲 VEGETAÇÃO AJUSTADA
    void SpawnVegetation(Vector2 offset)
    {
        usedPositions.Clear();


        for (int i = 0; i < vertices.Length; i += 10) // 🔥 menos spawn
        {
            Vector3 pos = vertices[i];
            Vector3 normal = mesh.normals[i];

            Vector3 worldPos = pos + transform.position;

            // 🚫 zona ao redor
            if (Vector3.Distance(worldPos, player.position) < safeRadius)
                continue;

            // 🚫 na frente do player
            if (Vector3.Distance(worldPos, player.position) < forwardSafeDistance && IsInPlayerPath(worldPos))
                continue;

            if (normal.y < 0.85f)
                continue;

            Vector2 point = new Vector2(pos.x + offset.x, pos.z + offset.y);
            BiomeType biome = GetBiome(point);

            float cluster = Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f);



            if (biome == BiomeType.Forest && cluster < 0.5f)
                continue;

            if (IsTooClose(worldPos))
                continue;

            float density = treeDensity;

            switch (biome)
            {
                case BiomeType.Desert:
                    density = 0.0003f; // 🔥 quase vazio
                    break;

                case BiomeType.Forest:
                    density = 0.05f;
                    break;

                case BiomeType.Snow:
                    density = 0.02f;
                    break;
            }


            // 🌲 ÁRVORES
            if (Random.value < density)
            {
                List<TreeData> validTrees = new List<TreeData>();

                foreach (var tree in trees)
                {
                    if (tree.biome == biome)
                    {
                        validTrees.Add(tree);
                    }

                }

                if (validTrees.Count > 0)
                {
                    TreeData selected = validTrees[Random.Range(0, validTrees.Count)];

                    Instantiate(
                        selected.prefab,
                        worldPos + Vector3.up * selected.yOffset,
                        Quaternion.Euler(0, Random.Range(0, 360), 0),
                        transform
                    );

                    usedPositions.Add(worldPos);
                }
            }

            // 🍄
            if (biome == BiomeType.Forest && Random.value < mushroomDensity) // chance do cluster
            {
                int mushroomCount = 3;

                for (int m = 0; m < mushroomCount; m++)
                {
                    Vector3 offsetPos = new Vector3(
                        Random.Range(-1.5f, 1.5f),
                        0,
                        Random.Range(-1.5f, 1.5f)
                    );

                    Vector3 finalPos = worldPos + offsetPos;

                    // 🚫 evita sobreposição
                    if (IsTooClose(finalPos))
                        continue;

                    Instantiate(
                        mushroomPrefab,
                        finalPos + Vector3.up * 0.2f,
                        Quaternion.Euler(0, Random.Range(0, 360), 0),
                        transform
                    );

                    usedPositions.Add(finalPos);
                }
            }
        }
    }

    bool IsTooClose(Vector3 pos)
    {
        foreach (var p in usedPositions)
        {
            if (Vector3.Distance(p, pos) < minDistanceBetweenObjects)
                return true;
        }
        return false;
    }

    void SpawnRockClusters(Vector2 offset)
    {
        float density = rockDensity;

        Vector2 centerPoint = new Vector2(offset.x + size / 2, offset.y + size / 2);
        BiomeType biome = GetBiome(centerPoint);

        // 🎯 densidade por bioma (bem mais controlado)
        switch (biome)
        {
            case BiomeType.Desert:
                density = 0.08f; // 🔥 quase vazio
                break;

            case BiomeType.Forest:
                density = 0.25f;
                break;

            case BiomeType.Snow:
                density = 0.15f;
                break;
        }

        int rockCount = 0;
        int maxRocksPerChunk = 2; // 🔥 limite baixo

        for (int c = 0; c < rockClusterCount; c++)
        {
            if (rockCount >= maxRocksPerChunk)
                break;

            // 🎯 chance de nem gerar cluster
            if (Random.value > density)
                continue;

            float x = Random.Range(0, size);
            float z = Random.Range(0, size);

            int index = (int)z * (size + 1) + (int)x;

            if (index < 0 || index >= vertices.Length)
                continue;

            Vector3 basePos = vertices[index] + transform.position;

            // 🚫 evita spawn perto do player
            if (player != null && Vector3.Distance(basePos, player.position) < 14f)
                continue;

            // 🚫 evita spawn em terreno inclinado
            if (mesh.normals[index].y < 0.9f)
                continue;

            // 🎯 sempre 1 pedra por cluster (controle total)
            int rocks = 1;

            for (int i = 0; i < rocks; i++)
            {
                if (rockCount >= maxRocksPerChunk)
                    break;

                Vector3 offsetPos = new Vector3(
                    Random.Range(-10f, 10f), // 🔥 bem espalhado
                    0,
                    Random.Range(-10f, 10f)
                );

                Vector3 spawnPos = basePos + offsetPos;

                // 🎯 raycast pra pegar o chão real
                RaycastHit hit;

                int groundLayer = LayerMask.GetMask("Ground");

                if (Physics.Raycast(spawnPos + Vector3.up * 20f, Vector3.down, out hit, 50f, groundLayer))
                {
                    spawnPos = hit.point;
                }

                // 🚫 evita perto do player
                if (player != null && Vector3.Distance(spawnPos, player.position) < 14f)
                    continue;

                // 🚫 evita na frente do player
                if (player != null && Vector3.Distance(spawnPos, player.position) < 12f && IsInPlayerPath(spawnPos))
                    continue;

                // 🚫 evita sobreposição
                if (IsTooClose(spawnPos))
                    continue;

                GameObject rock = Instantiate(
                    GetRandomRock(),
                    spawnPos,
                    Quaternion.Euler(0, Random.Range(0, 360), 0),
                    transform
                );

                Renderer r = rock.GetComponentInChildren<Renderer>();

                if (r != null)
                {
                    float offsetY = r.bounds.extents.y;
                    rock.transform.position += Vector3.up * offsetY * 0.9f;
                    rock.transform.position += Vector3.up * Random.Range(-0.1f, 0.1f);
                }

                rockCount++;
            }
        }
    }

    GameObject GetRandomRock()
    {
        float r = Random.value;

        if (r < 0.2f) return rockLargePrefab;
        if (r < 0.5f) return rockMediumPrefab;
        return rockSmallPrefab;
    }
}