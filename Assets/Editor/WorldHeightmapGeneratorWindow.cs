using System.IO;
using UnityEditor;
using UnityEngine;

public class WorldHeightmapGeneratorWindow : EditorWindow
{
    const string OutputFolder = "Assets/Resources/World";
    const string TexturePath = OutputFolder + "/GeneratedBaseHeightmap.png";
    const string AssetPath = OutputFolder + "/DefaultHeightmapData.asset";

    Vector2 worldOrigin = new Vector2(-400f, -400f);
    Vector2 worldSize = new Vector2(800f, 800f);
    int resolution = 1024;
    float minHeight = 0f;
    float maxHeight = 24f;
    float terrainScale = 40f;
    float detailNoiseScale = 0.05f;
    float detailNoiseStrength = 2f;
    bool applyRiverCarving = true;

    [MenuItem("Tools/Terrain/Generate Base Heightmap")]
    static void OpenWindow()
    {
        GetWindow<WorldHeightmapGeneratorWindow>("Base Heightmap");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Gerar Heightmap Base", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Gera uma textura grayscale do relevo procedural atual e cria/atualiza o asset DefaultHeightmapData para o TerrainChunk usar.", MessageType.Info);

        worldOrigin = EditorGUILayout.Vector2Field("World Origin", worldOrigin);
        worldSize = EditorGUILayout.Vector2Field("World Size", worldSize);
        resolution = EditorGUILayout.IntPopup("Resolution", resolution, new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
        minHeight = EditorGUILayout.FloatField("Min Height", minHeight);
        maxHeight = EditorGUILayout.FloatField("Max Height", maxHeight);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Procedural Base", EditorStyles.boldLabel);
        terrainScale = EditorGUILayout.FloatField("Terrain Scale", terrainScale);
        detailNoiseScale = EditorGUILayout.FloatField("Detail Noise Scale", detailNoiseScale);
        detailNoiseStrength = EditorGUILayout.FloatField("Detail Noise Strength", detailNoiseStrength);
        applyRiverCarving = EditorGUILayout.Toggle("Apply River Carving", applyRiverCarving);

        EditorGUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(worldSize.x <= 0f || worldSize.y <= 0f || maxHeight <= minHeight))
        {
            if (GUILayout.Button("Generate Heightmap"))
                Generate();
        }
    }

    void Generate()
    {
        EnsureFolder(OutputFolder);

        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = resolution <= 1 ? 0f : x / (float)(resolution - 1);
                float v = resolution <= 1 ? 0f : y / (float)(resolution - 1);

                Vector2 worldPoint = new Vector2(
                    Mathf.Lerp(worldOrigin.x, worldOrigin.x + worldSize.x, u),
                    Mathf.Lerp(worldOrigin.y, worldOrigin.y + worldSize.y, v)
                );

                float height = SampleProceduralHeight(worldPoint);
                float normalized = Mathf.InverseLerp(minHeight, maxHeight, height);
                texture.SetPixel(x, y, new Color(normalized, normalized, normalized, 1f));
            }
        }

        texture.Apply(false, false);

        byte[] pngBytes = texture.EncodeToPNG();
        File.WriteAllBytes(TexturePath, pngBytes);
        DestroyImmediate(texture);

        AssetDatabase.Refresh();
        ConfigureImportedTexture(TexturePath);
        CreateOrUpdateHeightmapAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<WorldHeightmapData>(AssetPath);
        EditorUtility.DisplayDialog("Heightmap gerada", "A texture e o asset DefaultHeightmapData foram criados/atualizados em Assets/Resources/World.", "OK");
    }

    float SampleProceduralHeight(Vector2 point)
    {
        float height = Mathf.PerlinNoise(point.x / terrainScale, point.y / terrainScale) * maxHeight;
        height += Mathf.PerlinNoise(point.x * detailNoiseScale, point.y * detailNoiseScale) * detailNoiseStrength;
        return Mathf.Clamp(height, minHeight, maxHeight);
    }

    void CreateOrUpdateHeightmapAsset()
    {
        WorldHeightmapData asset = AssetDatabase.LoadAssetAtPath<WorldHeightmapData>(AssetPath);
        if (asset == null)
        {
            asset = CreateInstance<WorldHeightmapData>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        asset.heightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        asset.worldOrigin = worldOrigin;
        asset.worldSize = worldSize;
        asset.minHeight = minHeight;
        asset.maxHeight = maxHeight;
        asset.clampOutsideBounds = true;
        asset.applyRiverCarving = applyRiverCarving;
        EditorUtility.SetDirty(asset);
    }

    static void ConfigureImportedTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
