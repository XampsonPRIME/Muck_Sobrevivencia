using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrain : MonoBehaviour
{
    public int width = 350;
    public int depth = 350;

    public float terrainScale = 40f;
    public float heightMultiplier = 12f;

    // 🌍 BIOMAS
    [Header("🌍 Biomas")]
    [Range(0f,1f)] public float desertX = 0.2f;
    [Range(0f,1f)] public float desertZ = 0.3f;
    [Range(0f,1f)] public float forestX = 0.7f;
    [Range(0f,1f)] public float forestZ = 0.3f;
    [Range(0f,1f)] public float denseForestX = 0.4f;
    [Range(0f,1f)] public float denseForestZ = 0.75f;
    [Range(0f,1f)] public float snowX = 0.85f;
    [Range(0f,1f)] public float snowZ = 0.4f;

    public float forestSize = 0.6f;
    public float desertSize = 1.2f;
    public float denseForestSize = 0.8f;

    [Header("❄️ Neve")]
    public float snowRadius = 40f;

    [Header("🏔️ Montanhas")]
    public int mountainCount = 5;
    public float mountainRadius = 60f;
    public float mountainHeight = 25f;

    // 🌲 VEGETAÇÃO
    [Header("🌲 Vegetação")]
    public GameObject treePrefab;
    public GameObject mushroomPrefab;

    public float treeDensity = 0.02f;
    public float mushroomDensity = 0.03f;

    public int treeStep = 6;

    // 🪨 ROCHAS (CLUSTERS)
    [Header("🪨 Rochas")]
    public GameObject rockSmallPrefab;
    public GameObject rockMediumPrefab;
    public GameObject rockLargePrefab;

    public int rockClusterCount = 25;

    public NavMeshSurface navMeshSurface;

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    Color[] colors;

    Vector2 desertCenter;
    Vector2 forestCenter;
    Vector2 denseForestCenter;
    Vector2 snowCenter;

    List<Vector2> mountains = new List<Vector2>();

    void Start()
    {
        SetupCenters();
        GenerateMountains();
        GenerateTerrain();

        if (navMeshSurface != null)
            StartCoroutine(BuildNavMeshDelayed());
    }

    void SetupCenters()
    {
        desertCenter = new Vector2(width * desertX, depth * desertZ);
        forestCenter = new Vector2(width * forestX, depth * forestZ);
        denseForestCenter = new Vector2(width * denseForestX, depth * denseForestZ);
        snowCenter = new Vector2(width * snowX, depth * snowZ);
    }

    void GenerateMountains()
    {
        mountains.Clear();

        for (int i = 0; i < mountainCount; i++)
        {
            mountains.Add(new Vector2(Random.Range(0, width), Random.Range(0, depth)));
        }
    }

    IEnumerator BuildNavMeshDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        navMeshSurface.BuildNavMesh();
    }

    float GetHeight(Vector2 point)
    {
        float h = Mathf.PerlinNoise(point.x / terrainScale, point.y / terrainScale) * heightMultiplier;

        foreach (var m in mountains)
        {
            float dist = Vector2.Distance(point, m);
            float mask = Mathf.Clamp01(1 - (dist / mountainRadius));
            h += mask * mountainHeight;
        }

        return h;
    }

    void GenerateTerrain()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GetComponent<MeshFilter>().mesh = mesh;

        vertices = new Vector3[(width + 1) * (depth + 1)];
        colors = new Color[(width + 1) * (depth + 1)];

        for (int z = 0, i = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                Vector2 p = new Vector2(x, z);
                float height = GetHeight(p);

                vertices[i] = new Vector3(x, height, z);

                float dDesert = Vector2.Distance(p, desertCenter) * desertSize;
                float dForest = Vector2.Distance(p, forestCenter) * forestSize;
                float dDense = Vector2.Distance(p, denseForestCenter) * denseForestSize;
                float dSnow = Vector2.Distance(p, snowCenter);

                if (dSnow < snowRadius)
                    colors[i] = Color.white;
                else if (dDesert < dForest && dDesert < dDense)
                    colors[i] = new Color(0.95f, 0.85f, 0.5f);
                else if (dForest < dDense)
                    colors[i] = new Color(0.4f, 0.8f, 0.3f);
                else
                    colors[i] = new Color(0.2f, 0.6f, 0.2f);

                i++;
            }
        }

        BuildMesh();
        SpawnVegetation();
        SpawnRockClusters();
    }

    void BuildMesh()
    {
        triangles = new int[width * depth * 6];

        int vert = 0;
        int tris = 0;

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris] = vert;
                triangles[tris + 1] = vert + width + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + width + 1;
                triangles[tris + 5] = vert + width + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void SpawnVegetation()
    {
        for (int z = 0; z < depth; z += treeStep)
        {
            for (int x = 0; x < width; x += treeStep)
            {
                int i = z * (width + 1) + x;

                Vector3 pos = vertices[i];
                Vector2 p = new Vector2(x, z);

                float slope = mesh.normals[i].y;

                float dDesert = Vector2.Distance(p, desertCenter) * desertSize;
                float dForest = Vector2.Distance(p, forestCenter) * forestSize;
                float dDense = Vector2.Distance(p, denseForestCenter) * denseForestSize;
                float dSnow = Vector2.Distance(p, snowCenter);

                bool ehNeve = dSnow < snowRadius;
                bool ehDeserto = dDesert < dForest && dDesert < dDense;

                if (!ehNeve && !ehDeserto && slope > 0.7f)
                {
                    if (Random.value < treeDensity)
                        Instantiate(treePrefab, pos, Quaternion.identity, transform);

                    float chance = mushroomDensity;
                    if (dDense < dForest) chance *= 2f;

                    if (Random.value < chance)
                        Instantiate(mushroomPrefab, pos + Vector3.up * 0.2f, Quaternion.identity, transform);
                }
            }
        }
    }

    void SpawnRockClusters()
    {
        for (int c = 0; c < rockClusterCount; c++)
        {
            float centerX = Random.Range(0, width);
            float centerZ = Random.Range(0, depth);

            Vector2 p = new Vector2(centerX, centerZ);

            float dDesert = Vector2.Distance(p, desertCenter) * desertSize;
            float dForest = Vector2.Distance(p, forestCenter) * forestSize;
            float dDense = Vector2.Distance(p, denseForestCenter) * denseForestSize;
            float dSnow = Vector2.Distance(p, snowCenter);

            bool ehNeve = dSnow < snowRadius;
            bool ehDeserto = dDesert < dForest && dDesert < dDense;

            if (!(ehNeve || ehDeserto)) continue;

            int rocksInCluster = Random.Range(6, 15);

            for (int i = 0; i < rocksInCluster; i++)
            {
                float offsetX = Random.Range(-8f, 8f);
                float offsetZ = Random.Range(-8f, 8f);

                float x = centerX + offsetX;
                float z = centerZ + offsetZ;

                if (x < 0 || z < 0 || x >= width || z >= depth) continue;

                int index = (int)z * (width + 1) + (int)x;
                Vector3 pos = vertices[index];

                GameObject prefab = GetRandomRock();

                Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
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