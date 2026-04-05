#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CowPrefabCreator
{
    const string AnimalPrefabFolder = "Assets/Prefabs/Animals";
    const string ItemPrefabFolder = "Assets/Prefabs/Items";
    const string AnimalMaterialFolder = "Assets/Materials/Animals";

    const string CowPrefabPath = AnimalPrefabFolder + "/Cow.prefab";
    const string MeatPrefabPath = ItemPrefabFolder + "/Carne.prefab";

    const string BodyMaterialPath = AnimalMaterialFolder + "/CowBody.mat";
    const string SpotMaterialPath = AnimalMaterialFolder + "/CowSpot.mat";
    const string HoofMaterialPath = AnimalMaterialFolder + "/CowHoof.mat";
    const string MeatMaterialPath = AnimalMaterialFolder + "/Meat.mat";

    [MenuItem("Tools/Animals/Create Cow Prefab")]
    public static void CreateCowPrefab()
    {
        EnsureFolders();

        Material body = LoadOrCreateMaterial(BodyMaterialPath, new Color(0.94f, 0.93f, 0.88f));
        Material spot = LoadOrCreateMaterial(SpotMaterialPath, new Color(0.15f, 0.12f, 0.1f));
        Material hoof = LoadOrCreateMaterial(HoofMaterialPath, new Color(0.25f, 0.18f, 0.12f));

        GameObject cowObject = new GameObject("Cow");
        Cow cow = cowObject.AddComponent<Cow>();
        cow.bodyMaterial = body;
        cow.spotMaterial = spot;
        cow.hoofMaterial = hoof;
        cow.rebuildVisualOnStart = true;
        cow.BuildProceduralModel();

        PrefabUtility.SaveAsPrefabAsset(cowObject, CowPrefabPath);
        Object.DestroyImmediate(cowObject);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CowPrefabPath);
        Debug.Log("Prefab da vaca criado em " + CowPrefabPath);
    }

    [MenuItem("Tools/Animals/Create Meat Item Prefab")]
    public static void CreateMeatItemPrefab()
    {
        EnsureFolders();

        Material meatMaterial = LoadOrCreateMaterial(MeatMaterialPath, new Color(0.72f, 0.2f, 0.2f));

        GameObject meatObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meatObject.name = "Carne";
        meatObject.transform.localScale = new Vector3(0.28f, 0.18f, 0.18f);

        Renderer renderer = meatObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = meatMaterial;

        Rigidbody rb = meatObject.AddComponent<Rigidbody>();
        rb.mass = 0.2f;

        Item item = meatObject.AddComponent<Item>();
        item.itemName = "Carne";
        item.itemType = ItemType.Resource;
        item.toolType = ToolType.None;
        item.toolDamage = 0;

        PrefabUtility.SaveAsPrefabAsset(meatObject, MeatPrefabPath);
        Object.DestroyImmediate(meatObject);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MeatPrefabPath);
        Debug.Log("Prefab de carne criado em " + MeatPrefabPath);
    }

    [MenuItem("Tools/Animals/Create Cow Materials")]
    public static void CreateCowMaterials()
    {
        EnsureFolders();
        LoadOrCreateMaterial(BodyMaterialPath, new Color(0.94f, 0.93f, 0.88f));
        LoadOrCreateMaterial(SpotMaterialPath, new Color(0.15f, 0.12f, 0.1f));
        LoadOrCreateMaterial(HoofMaterialPath, new Color(0.25f, 0.18f, 0.12f));
        LoadOrCreateMaterial(MeatMaterialPath, new Color(0.72f, 0.2f, 0.2f));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Materiais da vaca criados em " + AnimalMaterialFolder);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(AnimalPrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Animals");

        if (!AssetDatabase.IsValidFolder(ItemPrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Items");

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        if (!AssetDatabase.IsValidFolder(AnimalMaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Animals");
    }

    static Material LoadOrCreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");

        material = new Material(shader);
        material.color = color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
