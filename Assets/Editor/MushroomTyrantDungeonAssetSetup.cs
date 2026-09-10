using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MushroomTyrantDungeonAssetSetup
{
    const string KeyTexturePath = "Assets/Img/Key-Camara-do-Cogumelo-Tirano.png";
    const string KeyPrefabPath = "Assets/Resources/Items/Key-Camara-do-Cogumelo-Tirano.prefab";
    const string EntrancePrefabPath = "Assets/Resources/Dungeons/MushroomTyrantDungeonEntrance.prefab";

    static MushroomTyrantDungeonAssetSetup()
    {
        EditorApplication.delayCall += EnsureDungeonAssets;
    }

    [MenuItem("Tools/Dungeon/Preparar Calabouco do Cogumelo Tirano")]
    static void EnsureDungeonAssets()
    {
        EnsureDirectory("Assets/Resources");
        EnsureDirectory("Assets/Resources/Items");
        EnsureDirectory("Assets/Resources/Dungeons");

        Sprite keySprite = EnsureKeySprite();
        if (keySprite != null)
            EnsureKeyPrefab(keySprite);

        EnsureEntrancePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void EnsureDirectory(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(assetPath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            return;

        EnsureDirectory(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    static Sprite EnsureKeySprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(KeyTexturePath) as TextureImporter;
        if (importer == null)
            return null;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(KeyTexturePath);
    }

    static void EnsureKeyPrefab(Sprite keySprite)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
        GameObject root = existingPrefab != null ? PrefabUtility.LoadPrefabContents(KeyPrefabPath) : null;
        bool createdTransientRoot = false;

        if (root == null)
        {
            root = new GameObject("Key-Camara-do-Cogumelo-Tirano");
            createdTransientRoot = true;
        }

        root.name = "Key-Camara-do-Cogumelo-Tirano";
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = root.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = root.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = keySprite;

        BoxCollider boxCollider = root.GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = root.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;
        boxCollider.center = Vector3.zero;
        boxCollider.size = new Vector3(0.75f, 0.15f, 0.75f);

        Rigidbody rigidbody = root.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.mass = 0.2f;
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;

        Item item = root.GetComponent<Item>();
        if (item == null)
            item = root.AddComponent<Item>();
        item.itemName = MushroomTyrantDungeonKeyRegistry.ItemName;
        item.icon = keySprite;
        item.itemType = ItemType.Resource;
        item.toolType = ToolType.None;
        item.toolDamage = 0;

        PrefabUtility.SaveAsPrefabAsset(root, KeyPrefabPath);

        if (createdTransientRoot)
            Object.DestroyImmediate(root);
        else
            PrefabUtility.UnloadPrefabContents(root);
    }

    static void EnsureEntrancePrefab()
    {
        GameObject source = FindEntranceSourcePrefab();
        if (source == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            return;

        instance.name = "MushroomTyrantDungeonEntrance";
        EnsureEntranceCollider(instance);
        PrefabUtility.SaveAsPrefabAsset(instance, EntrancePrefabPath);
        Object.DestroyImmediate(instance);
    }

    static GameObject FindEntranceSourcePrefab()
    {
        string[] guids = AssetDatabase.FindAssets("Calabol t:GameObject", new[] { "Assets/Prefabs" });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    static void EnsureEntranceCollider(GameObject target)
    {
        if (target == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds localBounds = default;
        bool initialized = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Vector3 localCenter = target.transform.InverseTransformPoint(renderer.bounds.center);
            Bounds converted = new Bounds(localCenter, renderer.bounds.size);
            if (!initialized)
            {
                localBounds = converted;
                initialized = true;
            }
            else
            {
                localBounds.Encapsulate(converted.min);
                localBounds.Encapsulate(converted.max);
            }
        }

        if (!initialized)
            return;

        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (collider == null)
            collider = target.AddComponent<BoxCollider>();

        collider.isTrigger = false;
        collider.center = localBounds.center;
        collider.size = localBounds.size + new Vector3(0.5f, 0.5f, 0.5f);
    }
}
