using UnityEngine;

public static class StoneResourceItemRegistry
{
    public const string ItemName = "Pedras";
    static Item item;
    static Sprite sprite;
    static bool spriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (item != null) return item;
        GameObject itemObject = new GameObject("PedrasItemData");
        Object.DontDestroyOnLoad(itemObject);
        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Resources;
        item.rarity = ItemRarity.Common;
        item.description = "Pedras obtidas ao minerar rochas. Usadas em ferramentas, flechas e construcoes.";
        item.weight = 0.4f;
        item.maxStack = Item.ResourceStackLimit;
        item.toolType = ToolType.None;
        item.toolDamage = 1;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null && (spriteIsModelThumbnail || !Application.isPlaying)) return sprite;
        Sprite thumbnail = StoneResourceVisualFactory.CreateThumbnailSprite();
        if (thumbnail != null)
        {
            sprite = thumbnail;
            spriteIsModelThumbnail = true;
            if (item != null) item.icon = sprite;
            return sprite;
        }
        if (sprite != null) return sprite;
        sprite = Resources.Load<Sprite>("Icons/Pedra");
        spriteIsModelThumbnail = false;
        return sprite;
    }
}

public static class StoneResourceVisualFactory
{
    const string Root = "World/StoneResource/Meshy_AI_Faceted_Boulder_0730141124_texture_fbx/";
    const string ModelPath = Root + "Meshy_AI_Faceted_Boulder_0730141124_texture";
    const string AlbedoPath = Root + "Meshy_AI_Faceted_Boulder_0730141124_texture";
    const string NormalPath = Root + "Meshy_AI_Faceted_Boulder_0730141124_texture_normal";
    const string MetallicPath = Root + "Meshy_AI_Faceted_Boulder_0730141124_texture_metallic";
    static Material runtimeMaterial;

    public static bool IsStoneResource(string itemName)
    {
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(itemName.Trim(), StoneResourceItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static GameObject Spawn(Vector3 position, Item source, LayerMask groundMask, float collectRadius, Vector3 impulse)
    {
        GameObject drop = new GameObject("PedrasDrop");
        drop.transform.SetPositionAndRotation(position, Random.rotation);
        CopyItemData(drop, source ?? StoneResourceItemRegistry.GetOrCreate());
        BuildVisual(drop.transform, 0.58f, drop.transform.position);

        BoxCollider collider = drop.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.1f, 0f);
        collider.size = new Vector3(0.56f, 0.4f, 0.52f);
        Rigidbody body = drop.AddComponent<Rigidbody>();
        body.mass = 0.18f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.AddForce(Vector3.up * 1.25f + impulse, ForceMode.Impulse);

        FloatingPickup pickup = drop.AddComponent<FloatingPickup>();
        pickup.groundMask = groundMask;
        pickup.collectRadius = collectRadius;
        pickup.hoverHeight = 0.64f;
        pickup.bobHeight = 0.08f;
        pickup.rotationSpeed = 38f;
        return drop;
    }

    public static Sprite CreateThumbnailSprite(int textureSize = 96)
    {
        GameObject prefab = Application.isPlaying ? Resources.Load<GameObject>(ModelPath) : null;
        if (prefab == null) return null;
        int size = Mathf.Clamp(textureSize, 48, 256);
        Vector3 origin = new Vector3(0f, -9990f, 0f);
        GameObject root = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            root = new GameObject("StoneResourceThumbnailRoot");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;
            BuildVisual(root.transform, 1.5f, origin);
            if (!TryGetBounds(root.transform, out Bounds bounds)) return null;

            Vector3 direction = new Vector3(1.25f, 1.15f, -2.1f).normalized;
            cameraObject = new GameObject("StoneResourceThumbnailCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.32f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 25f;
            camera.allowHDR = false;
            camera.enabled = false;
            cameraObject.transform.position = bounds.center - direction * 4.2f;
            cameraObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            lightObject = new GameObject("StoneResourceThumbnailLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.75f;
            light.color = new Color(1f, 0.94f, 0.84f);
            lightObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply(false, false);
            texture.name = "PedrasModelThumbnailTexture";
            Sprite result = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            result.name = "PedrasModelThumbnailSprite";
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (lightObject != null) Object.Destroy(lightObject);
            if (root != null) Object.Destroy(root);
        }
    }

    static void BuildVisual(Transform parent, float targetSize, Vector3 targetCenter)
    {
        GameObject prefab = Resources.Load<GameObject>(ModelPath);
        if (prefab == null) return;
        GameObject root = new GameObject("StoneResourceVisual");
        root.transform.SetParent(parent, false);
        GameObject model = Object.Instantiate(prefab, root.transform);
        model.name = "MeshyStoneResourceModel";
        foreach (Collider value in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(value);
        foreach (Rigidbody value in model.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(value);
        foreach (Animator value in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(value);
        ApplyMaterial(model);
        Normalize(root.transform, targetSize, targetCenter);
    }

    static void Normalize(Transform root, float targetSize, Vector3 targetCenter)
    {
        if (!TryGetBounds(root, out Bounds bounds)) return;
        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension > 0.001f) root.localScale *= targetSize / maxDimension;
        if (TryGetBounds(root, out bounds)) root.position += targetCenter - bounds.center;
    }

    static bool TryGetBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
        bounds = default;
        bool found = false;
        if (renderers == null) return false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    static void ApplyMaterial(GameObject visual)
    {
        Material material = GetMaterial();
        if (material == null) return;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
    }

    static Material GetMaterial()
    {
        if (runtimeMaterial != null) return runtimeMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        runtimeMaterial = new Material(shader) { name = "StoneResource_RuntimeMaterial" };
        SetTexture("_BaseMap", Resources.Load<Texture2D>(AlbedoPath));
        SetTexture("_MainTex", Resources.Load<Texture2D>(AlbedoPath));
        Texture2D normal = Resources.Load<Texture2D>(NormalPath);
        Texture2D metallic = Resources.Load<Texture2D>(MetallicPath);
        SetTexture("_BumpMap", normal);
        SetTexture("_MetallicGlossMap", metallic);
        if (normal != null) runtimeMaterial.EnableKeyword("_NORMALMAP");
        if (metallic != null) runtimeMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
        return runtimeMaterial;
    }

    static void SetTexture(string property, Texture texture)
    {
        if (texture != null && runtimeMaterial.HasProperty(property)) runtimeMaterial.SetTexture(property, texture);
    }

    static void CopyItemData(GameObject target, Item source)
    {
        Item stable = StoneResourceItemRegistry.GetOrCreate();
        Item item = target.AddComponent<Item>();
        item.itemName = StoneResourceItemRegistry.ItemName;
        item.icon = StoneResourceItemRegistry.GetSprite();
        item.itemType = source != null ? source.itemType : stable.itemType;
        item.category = stable.GetCategory();
        item.rarity = stable.GetRarity();
        item.description = stable.GetDescription();
        item.weight = stable.GetWeight();
        item.maxStack = stable.GetMaxStack();
        item.toolType = ToolType.None;
        item.toolDamage = source != null ? source.toolDamage : stable.toolDamage;
    }
}
