using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MeshyStickSetup
{
    const string Folder = "Assets/Resources/World/StickResource/Meshy_AI_Low_Poly_Branch_0730175107_texture_fbx";
    const string ModelPath = Folder + "/Meshy_AI_Low_Poly_Branch_0730175107_texture.fbx";
    const string AlbedoPath = Folder + "/Meshy_AI_Low_Poly_Branch_0730175107_texture.png";
    const string NormalPath = Folder + "/Meshy_AI_Low_Poly_Branch_0730175107_texture_normal.png";
    const string MetallicPath = Folder + "/Meshy_AI_Low_Poly_Branch_0730175107_texture_metallic.png";
    const string MaterialPath = Folder + "/MeshyStickResource.mat";
    const string PrefabPath = "Assets/Prefabs/Graveto.prefab";

    [MenuItem("Elarion/Meshy/Configurar Graveto Meshy")]
    public static void ConfigureFromMenu()
    {
        if (!File.Exists(ModelPath))
        {
            Debug.LogError("FBX do graveto Meshy nao encontrado.");
            return;
        }

        ConfigureModelImporter();
        ConfigureTexture(AlbedoPath, TextureImporterType.Default, true);
        ConfigureTexture(NormalPath, TextureImporterType.NormalMap, false);
        ConfigureTexture(MetallicPath, TextureImporterType.Default, false);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Material material = EnsureMaterial();
        if (model == null || material == null)
        {
            Debug.LogError("Nao foi possivel carregar o modelo ou material do graveto.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RemoveMissingScripts(root);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            GameObject visualRoot = new GameObject("MeshyStickResourceVisual");
            visualRoot.transform.SetParent(root.transform, false);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, visualRoot.transform);
            visual.name = "MeshyStickResourceModel";

            foreach (Collider component in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(component);
            foreach (Rigidbody component in visual.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(component);
            foreach (Animator component in visual.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(component);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;

            Normalize(visualRoot.transform, 0.82f);

            BoxCollider collider = root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.14f, 0f);
            collider.size = new Vector3(0.9f, 0.42f, 0.55f);

            Item item = root.GetComponent<Item>() ?? root.AddComponent<Item>();
            item.itemName = StickResourceItemRegistry.ItemName;
            item.itemType = ItemType.Resource;
            item.category = InventoryCategory.Resources;
            item.maxStack = Item.ResourceStackLimit;
            item.icon = null;
            if (root.GetComponent<StickResourcePrefabBinder>() == null)
                root.AddComponent<StickResourcePrefabBinder>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Graveto Meshy configurado no prefab, drop e inventario.");
    }

    static void RemoveMissingScripts(GameObject root)
    {
        foreach (Transform value in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(value.gameObject);
    }

    static void Normalize(Transform root, float targetSize)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float max = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (max > 0.001f) root.localScale *= targetSize / max;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        root.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
    }

    static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();
    }

    static void ConfigureTexture(string path, TextureImporterType type, bool sRgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = type;
        importer.sRGBTexture = sRgb;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    static Material EnsureMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MeshyStickResource" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
        if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", metallic);
        if (normal != null) material.EnableKeyword("_NORMALMAP");
        if (metallic != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }
}
