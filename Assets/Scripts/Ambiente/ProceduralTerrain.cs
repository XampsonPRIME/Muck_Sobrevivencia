using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrain : MonoBehaviour
{
    public enum BiomeType
    {
        Desert,
        Forest,
        Snow
    }

    public int width = 350;
    public int depth = 350;

    public float terrainScale = 40f;
    public float heightMultiplier = 12f;

    [Header("🌍 Biomas")]
    [Range(0f, 1f)] public float desertX = 0.2f;
    [Range(0f, 1f)] public float desertZ = 0.3f;
    [Range(0f, 1f)] public float forestX = 0.7f;
    [Range(0f, 1f)] public float forestZ = 0.3f;
    [Range(0f, 1f)] public float denseForestX = 0.4f;
    [Range(0f, 1f)] public float denseForestZ = 0.75f;
    [Range(0f, 1f)] public float snowX = 0.85f;
    [Range(0f, 1f)] public float snowZ = 0.4f;

    public float forestSize = 0.6f;
    public float desertSize = 1.2f;
    public float denseForestSize = 0.8f;

    [Header("❄️ Neve")]
    public float snowRadius = 40f;

    [Header("🏔️ Montanhas")]
    public int mountainCount = 5;
    public float mountainRadius = 60f;
    public float mountainHeight = 25f;

    [System.Serializable]
    public class TreeData
    {
        public GameObject prefab;
        public float yOffset;
        public BiomeType biome; // 🔥 define onde nasce
    }

    [Header("🌲 Vegetação")]
    public TreeData[] trees;
    public GameObject mushroomPrefab;

    // 🌊 CONFIG DO LAGO// 🌊 LAGO
    public GameObject lakePrefab;

    public float lakeRadius = 25f;
    public float waterLevel = 2.5f;
    public float lakeDepthAmount = 4f;

    private Vector2 lakePosition;
    private bool lakePlaced = false;
    [Header("Biomas")]
    public float biomeScale = 50f;

    [Header("🪵 Gravetos")]
    public GameObject gravetoPrefab;
    public float gravetoDensity = 0.04f;
    public float gravetoRespawnTime = 20f;

    public float treeDensity = 0.02f;
    public float mushroomDensity = 0.03f;

    public int treeStep = 6;

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
    Vector2[] uvs;

    Vector2 desertCenter;
    Vector2 forestCenter;
    Vector2 denseForestCenter;
    Vector2 snowCenter;

    List<Mountain> mountains = new List<Mountain>();

    void Start()
    {
        SetupCenters();
        GenerateMountains();
        GenerateTerrain();
        SpawnLake(); // 👈 IMPORTANTE

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
            Mountain m = new Mountain();

            m.position = new Vector2(
                Random.Range(0, width),
                Random.Range(0, depth)
            );

            m.radius = Random.Range(20f, 40f);
            m.strength = Random.Range(5f, 12f);

            mountains.Add(m);
        }
    }

    bool IsDesert(Vector2 p)
    {
        float dDesert = Vector2.Distance(p, desertCenter) * desertSize;
        float dForest = Vector2.Distance(p, forestCenter) * forestSize;
        float dDense = Vector2.Distance(p, denseForestCenter) * denseForestSize;

        return dDesert < dForest && dDesert < dDense;
    }

    IEnumerator BuildNavMeshDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        navMeshSurface.BuildNavMesh();
    }

    void SpawnLake()
    {
        if (lakePrefab != null && lakePlaced)
        {
            Vector3 worldPos = new Vector3(lakePosition.x, waterLevel, lakePosition.y);

            Instantiate(lakePrefab, worldPos, Quaternion.identity);
        }
    }

    float GetHeight(Vector2 point)
    {
        float h = Mathf.PerlinNoise(point.x / terrainScale, point.y / terrainScale) * heightMultiplier;

        h += Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f) * 2f;

        // 🌍 BIOMA
        float biome = Mathf.PerlinNoise(point.x / biomeScale, point.y / biomeScale);
        bool isForest = biome > 0.3f && biome < 0.6f;

        // 🌊 DEFINE POSIÇÃO DO LAGO (1x só)
        if (isForest && !lakePlaced)
        {
            lakePosition = point;
            lakePlaced = true;
        }

        // 🌊 ESCAVA O LAGO
        if (lakePlaced)
        {
            float distance = Vector2.Distance(point, lakePosition);

            if (distance < lakeRadius)
            {
                float t = distance / lakeRadius;

                float lakeDepth = Mathf.Lerp(waterLevel, waterLevel - lakeDepthAmount, 1 - t);

                h = Mathf.Min(h, lakeDepth);
            }
        }

        // 🏔️ MONTANHAS (mantido)
        foreach (var m in mountains)
        {
            float dist = Vector2.Distance(point, m.position);

            if (dist < m.radius)
            {
                float falloff = Mathf.Clamp01(1 - (dist / m.radius));
                falloff = Mathf.SmoothStep(0, 1, falloff);
                falloff = Mathf.Pow(falloff, 2.5f);

                h += falloff * m.strength;
            }
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
        uvs = new Vector2[(width + 1) * (depth + 1)];

        for (int z = 0, i = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                Vector2 p = new Vector2(x, z);
                float height = GetHeight(p);

                vertices[i] = new Vector3(x, height, z);

                uvs[i] = new Vector2((float)x / width, (float)z / depth);

                float dDesert = Vector2.Distance(p, desertCenter) * desertSize;
                float dForest = Vector2.Distance(p, forestCenter) * forestSize;
                float dDense = Vector2.Distance(p, denseForestCenter) * denseForestSize;
                float dSnow = Vector2.Distance(p, snowCenter);

                if (dSnow < snowRadius)
                    colors[i] = new Color(1, 0, 0, 0); // neve
                else if (dDesert < dForest && dDesert < dDense)
                    colors[i] = new Color(0, 0, 0, 0); // deserto
                else
                    colors[i] = new Color(0, 1, 0, 0); // floresta

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
        mesh.uv = uvs;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshCollider col = GetComponent<MeshCollider>();
        col.sharedMesh = null;
        col.sharedMesh = mesh;
    }

    void SpawnVegetation()
    {
        for (int z = 0; z < depth; z += treeStep)
        {
            for (int x = 0; x < width; x += treeStep)
            {
                int i = z * (width + 1) + x;

                Vector3 pos = vertices[i];
                Vector3 normal = mesh.normals[i];

                // 🚫 BLOQUEIA LATERAIS (ESSA LINHA RESOLVE SEU PROBLEMA)
                if (normal.y < 0.8f)
                    continue;

                // 🌍 BIOMA
                bool isSnow = colors[i].r > 0.3f;
                bool isForest = colors[i].g > 0.3f;
                bool isDesert = !isSnow && !isForest;

                // 🌳 ÁRVORES / CACTOS
                if (Random.value < treeDensity)
                {
                    List<TreeData> validTrees = new List<TreeData>();

                    foreach (var tree in trees)
                    {
                        if (tree.biome == BiomeType.Snow && isSnow)
                            validTrees.Add(tree);

                        else if (tree.biome == BiomeType.Forest && isForest)
                            validTrees.Add(tree);

                        else if (tree.biome == BiomeType.Desert && isDesert)
                            validTrees.Add(tree);
                    }

                    if (validTrees.Count == 0)
                        continue;

                    TreeData selected = validTrees[Random.Range(0, validTrees.Count)];

                    float offset = selected.yOffset;

                    GameObject t = Instantiate(
                        selected.prefab,
                        pos + Vector3.up * offset,
                        Quaternion.Euler(0, Random.Range(0, 360), 0),
                        transform
                    );

                    t.transform.localScale *= Random.Range(0.9f, 1.2f);
                }

                // 🍄 cogumelos
                if (Random.value < mushroomDensity && isForest)
                {
                    Instantiate(
                        mushroomPrefab,
                        pos + Vector3.up * 0.2f,
                        Quaternion.identity,
                        transform
                    );
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

            int rocksInCluster = Random.Range(6, 15);

            for (int i = 0; i < rocksInCluster; i++)
            {
                float x = centerX + Random.Range(-8f, 8f);
                float z = centerZ + Random.Range(-8f, 8f);

                if (x < 0 || z < 0 || x >= width || z >= depth) continue;

                int index = (int)z * (width + 1) + (int)x;
                Vector3 pos = AlignRockSpawnToGround(vertices[index] + transform.position);

                GameObject rock = Instantiate(GetRandomRock(), pos, Quaternion.identity, transform);
                AlignRockBaseToGround(rock, pos);
            }
        }
    }

    Vector3 AlignRockSpawnToGround(Vector3 worldPos)
    {
        Collider terrainCollider = GetComponent<Collider>();

        if (terrainCollider != null)
        {
            Ray ray = new Ray(worldPos + Vector3.up * 50f, Vector3.down);

            if (terrainCollider.Raycast(ray, out RaycastHit hit, 100f))
                return hit.point;
        }

        if (Physics.Raycast(worldPos + Vector3.up * 50f, Vector3.down, out RaycastHit fallbackHit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return fallbackHit.point;

        return worldPos;
    }

    void AlignRockBaseToGround(GameObject rock, Vector3 groundPoint)
    {
        Renderer[] renderers = rock.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float groundOffset = groundPoint.y - bounds.min.y;
        rock.transform.position += Vector3.up * groundOffset;
    }

    GameObject GetRandomRock()
    {
        float r = Random.value;

        if (r < 0.2f) return rockLargePrefab;
        if (r < 0.5f) return rockMediumPrefab;
        return rockSmallPrefab;
    }
}
