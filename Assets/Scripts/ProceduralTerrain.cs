using UnityEngine;
using Unity.AI.Navigation;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrain : MonoBehaviour
{
    public int width = 700;
    public int depth = 700;

    public float terrainScale = 40f;
    public float biomeScale = 50f;

    public float heightMultiplier = 8f;

    // Prefabs
    public GameObject treePrefab;
    public GameObject rockPrefab;
    public GameObject mushroomPrefab;

    // 🔥 NavMesh
    public NavMeshSurface navMeshSurface;

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    Color[] colors;

    void Start()
    {

        // 🔥 Gera o NavMesh DEPOIS do terreno
        if (navMeshSurface != null)
        {
            GenerateTerrain();

            StartCoroutine(BuildNavMeshDelayed());
        }
        else
        {
            Debug.LogWarning("NavMeshSurface não atribuída!");
        }
    }

    System.Collections.IEnumerator BuildNavMeshDelayed()
{
    yield return new WaitForSeconds(0.5f);

    if (navMeshSurface != null)
    {
        navMeshSurface.BuildNavMesh();
        Debug.Log("🔥 NavMesh atualizado com terreno procedural!");
    }
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
                float height = Mathf.PerlinNoise(x / terrainScale, z / terrainScale) * heightMultiplier;
                float biomeValue = Mathf.PerlinNoise(x / biomeScale + 100, z / biomeScale + 100);

                vertices[i] = new Vector3(x, height, z);
                Vector3 spawnPos = new Vector3(x, height, z);

                if (biomeValue < 0.3f)
                {
                    // DESERTO
                    colors[i] = new Color(0.9f, 0.8f, 0.4f);

                    if (Random.value < 0.01f && rockPrefab != null)
                        Instantiate(rockPrefab, spawnPos, Quaternion.identity, transform);
                }
                else if (biomeValue < 0.6f)
                {
                    // FLORESTA
                    colors[i] = new Color(0.4f, 0.8f, 0.3f);

                    if (Random.value < 0.02f && treePrefab != null)
                        Instantiate(treePrefab, spawnPos, Quaternion.identity, transform);

                    if (Random.value < 0.01f && mushroomPrefab != null)
                        Instantiate(mushroomPrefab, spawnPos, Quaternion.identity, transform);
                }
                else
                {
                    // NEVE
                    colors[i] = Color.white;

                    if (Random.value < 0.015f && rockPrefab != null)
                        Instantiate(rockPrefab, spawnPos, Quaternion.identity, transform);
                }

                i++;
            }
        }

        triangles = new int[width * depth * 6];

        int vert = 0;
        int tris = 0;

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris + 0] = vert;
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
}