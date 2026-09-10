using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MeshyStoneResourceSetup
{
    const string Folder = "Assets/Resources/World/StoneResource/Meshy_AI_Faceted_Boulder_0730141124_texture_fbx";
    const string ModelPath = Folder + "/Meshy_AI_Faceted_Boulder_0730141124_texture.fbx";
    const string AlbedoPath = Folder + "/Meshy_AI_Faceted_Boulder_0730141124_texture.png";
    const string NormalPath = Folder + "/Meshy_AI_Faceted_Boulder_0730141124_texture_normal.png";
    const string MetallicPath = Folder + "/Meshy_AI_Faceted_Boulder_0730141124_texture_metallic.png";
    const string MaterialPath = Folder + "/MeshyStoneResource.mat";
    const string PrefabPath = "Assets/Prefabs/PedraLoot.prefab";

    [MenuItem("Elarion/Meshy/Configurar Recurso de Pedra Meshy")]
    public static void ConfigureFromMenu()
    {
        ConfigureImporters();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Material material = EnsureMaterial();
        if (model == null || material == null)
        {
            Debug.LogError("Modelo do recurso de pedra Meshy nao encontrado.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (Transform value in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(value.gameObject);

            MeshFilter oldFilter = root.GetComponent<MeshFilter>();
            MeshRenderer oldRenderer = root.GetComponent<MeshRenderer>();
            if (oldFilter != null) Object.DestroyImmediate(oldFilter);
            if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            root.transform.localScale = Vector3.one;
            GameObject visualRoot = new GameObject("MeshyStoneResourceVisual");
            visualRoot.transform.SetParent(root.transform, false);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, visualRoot.transform);
            visual.name = "MeshyStoneResourceModel";
            foreach (Collider component in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(component);
            foreach (Rigidbody component in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(component);
            foreach (Animator component in visual.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(component);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            Normalize(visualRoot.transform, 0.58f);

            SphereCollider collider = root.GetComponent<SphereCollider>();
            if (collider == null) collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = false;
            collider.center = new Vector3(0f, 0.18f, 0f);
            collider.radius = 0.32f;

            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body == null) body = root.AddComponent<Rigidbody>();
            body.mass = 0.18f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            FloatingPickup pickup = root.GetComponent<FloatingPickup>();
            if (pickup == null) pickup = root.AddComponent<FloatingPickup>();
            pickup.collectRadius = 1.35f;
            pickup.hoverHeight = 0.58f;
            pickup.bobHeight = 0.08f;
            pickup.rotationSpeed = 38f;
            pickup.autoCollect = false;

            Item item = root.GetComponent<Item>();
            if (item == null) item = root.AddComponent<Item>();
            item.itemName = StoneResourceItemRegistry.ItemName;
            item.itemType = ItemType.Resource;
            item.category = InventoryCategory.Resources;
            item.maxStack = Item.ResourceStackLimit;
            item.icon = null;
            if (root.GetComponent<StoneResourcePrefabBinder>() == null)
                root.AddComponent<StoneResourcePrefabBinder>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Prefab PedraLoot atualizado com o modelo Meshy do recurso de pedra.");
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
        Vector3 parentPosition = root.parent != null ? root.parent.position : Vector3.zero;
        root.position += new Vector3(
            parentPosition.x - bounds.center.x,
            parentPosition.y - bounds.min.y,
            parentPosition.z - bounds.center.z
        );
    }

    static void ConfigureImporters()
    {
        ModelImporter model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (model != null)
        {
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.SaveAndReimport();
        }
        ConfigureTexture(AlbedoPath, TextureImporterType.Default, true);
        ConfigureTexture(NormalPath, TextureImporterType.NormalMap, false);
        ConfigureTexture(MetallicPath, TextureImporterType.Default, false);
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
            material = new Material(shader) { name = "MeshyStoneResource" };
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
