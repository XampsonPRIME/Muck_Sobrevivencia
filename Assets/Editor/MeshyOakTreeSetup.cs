using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MeshyOakTreeSetup
{
    const string RootFolder = "Assets/Resources/World/OakTree";
    const string SourceFolder = RootFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_fbx";
    const string ModelPath = SourceFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture.fbx";
    const string AlbedoPath = SourceFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture.png";
    const string EmissionPath = SourceFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_emission.png";
    const string MetallicPath = SourceFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_metallic.png";
    const string NormalPath = SourceFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_normal.png";
    const string RoughnessPath = SourceFolder + "/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_roughness.png";
    const string MaterialPath = RootFolder + "/MeshyOakTree_Material.mat";
    const string PrefabPath = RootFolder + "/MeshyOakTree.prefab";

    static bool setupScheduled;

    static MeshyOakTreeSetup()
    {
        ScheduleAutoSetup();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                ScheduleAutoSetup();
        };
    }

    [MenuItem("Elarion/Meshy/Configurar Carvalho Meshy")]
    public static void ConfigureFromMenu()
    {
        Configure(true);
    }

    public static void ScheduleAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || setupScheduled)
            return;

        setupScheduled = true;
        EditorApplication.delayCall += () =>
        {
            setupScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Configure(false);
        };
    }

    public static void Configure(bool verbose)
    {
        if (!File.Exists(ModelPath))
            return;

        bool importersChanged = ConfigureImporters();
        Material material = EnsureMaterial();
        EnsureRuntimePrefab();

        AssetDatabase.SaveAssets();
        if (importersChanged || material != null)
            AssetDatabase.Refresh();

        if (verbose)
            Debug.Log("Carvalho Meshy configurado: importacao, material e prefab runtime atualizados.");
    }

    static bool ConfigureImporters()
    {
        bool changed = false;
        changed |= ConfigureFbxImporter(ModelPath);
        changed |= ConfigureTextureImporter(AlbedoPath, TextureImporterType.Default, true, 1024);
        changed |= ConfigureTextureImporter(EmissionPath, TextureImporterType.Default, true, 512);
        changed |= ConfigureTextureImporter(MetallicPath, TextureImporterType.Default, false, 512);
        changed |= ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false, 1024);
        changed |= ConfigureTextureImporter(RoughnessPath, TextureImporterType.Default, false, 512);
        return changed;
    }

    static bool ConfigureFbxImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return false;

        bool dirty = false;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            dirty = true;
        }

        if (importer.importAnimation)
        {
            importer.importAnimation = false;
            dirty = true;
        }

        if (importer.animationType != ModelImporterAnimationType.None)
        {
            importer.animationType = ModelImporterAnimationType.None;
            dirty = true;
        }

        if (importer.importCameras)
        {
            importer.importCameras = false;
            dirty = true;
        }

        if (importer.importLights)
        {
            importer.importLights = false;
            dirty = true;
        }

        if (importer.importVisibility)
        {
            importer.importVisibility = false;
            dirty = true;
        }

        if (importer.importBlendShapes)
        {
            importer.importBlendShapes = false;
            dirty = true;
        }

        if (importer.meshCompression != ModelImporterMeshCompression.High)
        {
            importer.meshCompression = ModelImporterMeshCompression.High;
            dirty = true;
        }

        if (importer.isReadable)
        {
            importer.isReadable = false;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static bool ConfigureTextureImporter(string assetPath, TextureImporterType type, bool sRgb, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool dirty = false;

        if (importer.textureType != type)
        {
            importer.textureType = type;
            dirty = true;
        }

        if (importer.sRGBTexture != sRgb)
        {
            importer.sRGBTexture = sRgb;
            dirty = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            dirty = true;
        }

        if (importer.maxTextureSize != maxSize)
        {
            importer.maxTextureSize = maxSize;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            dirty = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.ToNearest)
        {
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static Material EnsureMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MeshyOakTree_Material" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
        {
            material.shader = shader;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

        SetTexture(material, "_BaseMap", albedo);
        SetTexture(material, "_MainTex", albedo);
        SetTexture(material, "_EmissionMap", emission);
        SetTexture(material, "_MetallicGlossMap", metallic);
        SetTexture(material, "_BumpMap", normal);

        if (emission != null)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", Color.white * 0.25f);
        }

        if (normal != null)
            material.EnableKeyword("_NORMALMAP");
        if (metallic != null)
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureRuntimePrefab()
    {
        GameObject root = new GameObject("MeshyOakTree");
        MeshyOakTreeRuntimeVisual visual = root.AddComponent<MeshyOakTreeRuntimeVisual>();
        visual.targetWorldHeight = 18f;
        visual.trunkColliderWidth = 2.7f;
        visual.maxScaleMultiplier = 2000f;
        visual.Build();
        MeshyOakTreeRuntimeFactory.ConfigureResourceNode(root);

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
            collider = root.AddComponent<BoxCollider>();

        collider.center = new Vector3(0f, 8.3f, 0f);
        collider.size = new Vector3(2.7f, 16.6f, 2.7f);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void SetTexture(Material material, string property, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(property))
            material.SetTexture(property, texture);
    }
}

public class MeshyOakTreeAssetPostprocessor : AssetPostprocessor
{
    const string SourcePath = "Assets/Resources/World/OakTree/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_fbx";

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (ContainsOakTreeAsset(importedAssets) || ContainsOakTreeAsset(movedAssets))
            MeshyOakTreeSetup.ScheduleAutoSetup();
    }

    static bool ContainsOakTreeAsset(string[] paths)
    {
        if (paths == null)
            return false;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (!string.IsNullOrEmpty(path) && path.StartsWith(SourcePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
