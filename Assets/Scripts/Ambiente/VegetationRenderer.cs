using UnityEngine;
using System.Collections.Generic;

public class VegetationRenderer : MonoBehaviour
{
    public Mesh treeMesh;
    public Material treeMaterial;

    public int width = 350;
    public int depth = 350;

    public float terrainScale = 40f;
    public float heightMultiplier = 12f;

    public int step = 5;
    public float density = 0.3f;

    List<Matrix4x4> matrices = new List<Matrix4x4>();

    void Start()
    {
        GenerateTrees();
    }

    void GenerateTrees()
    {
        matrices.Clear();

        for (int z = 0; z < depth; z += step)
        {
            for (int x = 0; x < width; x += step)
            {
                if (Random.value > density) continue;

                float height = Mathf.PerlinNoise(x / terrainScale, z / terrainScale) * heightMultiplier;

                Vector3 pos = new Vector3(x, height, z);

                Quaternion rot = Quaternion.Euler(0, Random.Range(0, 360), 0);

                float scale = Random.Range(0.8f, 1.3f);

                Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, Vector3.one * scale);

                matrices.Add(matrix);
            }
        }
    }

    void Update()
    {
        RenderTrees();
    }

    void RenderTrees()
    {
        int batchSize = 1023;

        for (int i = 0; i < matrices.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, matrices.Count - i);

            Graphics.DrawMeshInstanced(
                treeMesh,
                0,
                treeMaterial,
                matrices.GetRange(i, count)
            );
        }
    }
}