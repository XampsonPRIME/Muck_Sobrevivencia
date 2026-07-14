using UnityEngine;

public static class MeshyOakTreeRuntimeFactory
{
    public const string OakWoodItemName = OakWoodItemRegistry.ItemName;
    public const float GroundSinkDepth = 1.35f;

    const string PrefabPath = "World/OakTree/MeshyOakTree";
    const string ModelPath = "World/OakTree/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture";
    const string MaterialPath = "World/OakTree/MeshyOakTree_Material";
    const string AlbedoPath = "World/OakTree/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture";
    const string NormalPath = "World/OakTree/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_normal";
    const string MetallicPath = "World/OakTree/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0703235245_texture_metallic";

    static Material runtimeMaterial;

    public static GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent, GameObject fallbackPrefab = null)
    {
        GameObject prefabAsset = LoadPrefab();
        GameObject tree = prefabAsset != null
            ? Object.Instantiate(prefabAsset, position, rotation, parent)
            : CreateMeshyTree(position, rotation, parent);

        ConfigureResourceNode(tree);
        return tree;
    }

    public static void ConfigureResourceNode(GameObject tree)
    {
        if (tree == null)
            return;

        ResourceNode resource = tree.GetComponent<ResourceNode>() ?? tree.GetComponentInChildren<ResourceNode>();
        if (resource == null)
            resource = tree.AddComponent<ResourceNode>();

        resource.itemName = OakWoodItemName;
        resource.itemData = Application.isPlaying ? OakWoodItemRegistry.GetOrCreate() : null;
        resource.maxHealth = Mathf.Max(resource.maxHealth, 4);
        resource.minDrop = Mathf.Max(resource.minDrop, 2);
        resource.maxDrop = 4;
        resource.requiredTool = ToolType.Axe;
        resource.allowEmptyHand = false;
        resource.emptyHandDamage = 0;
        resource.emptyHandMinDrop = 0;
        resource.emptyHandMaxDrop = 0;
    }

    static GameObject CreateMeshyTree(Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject tree = new GameObject("CarvalhoMeshy");
        tree.transform.SetPositionAndRotation(position, rotation);
        if (parent != null)
            tree.transform.SetParent(parent, true);

        MeshyOakTreeRuntimeVisual visual = tree.AddComponent<MeshyOakTreeRuntimeVisual>();
        visual.Build();
        return tree;
    }

    public static GameObject LoadPrefab()
    {
        return Resources.Load<GameObject>(PrefabPath);
    }

    public static GameObject LoadModel()
    {
        return Resources.Load<GameObject>(ModelPath);
    }

    public static Material GetMaterial()
    {
        Material material = Resources.Load<Material>(MaterialPath);
        if (material != null)
            return material;

        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        runtimeMaterial = new Material(shader)
        {
            name = "MeshyOakTree_RuntimeMaterial"
        };

        Texture2D albedo = Resources.Load<Texture2D>(AlbedoPath);
        Texture2D normal = Resources.Load<Texture2D>(NormalPath);
        Texture2D metallic = Resources.Load<Texture2D>(MetallicPath);

        SetTexture(runtimeMaterial, "_BaseMap", albedo);
        SetTexture(runtimeMaterial, "_MainTex", albedo);
        SetTexture(runtimeMaterial, "_BumpMap", normal);
        SetTexture(runtimeMaterial, "_MetallicGlossMap", metallic);

        if (normal != null)
            runtimeMaterial.EnableKeyword("_NORMALMAP");
        if (metallic != null)
            runtimeMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");

        return runtimeMaterial;
    }

    static void SetTexture(Material material, string property, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(property))
            material.SetTexture(property, texture);
    }
}
