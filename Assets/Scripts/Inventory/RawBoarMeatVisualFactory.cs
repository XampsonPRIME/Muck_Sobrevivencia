using UnityEngine;

public static class RawBoarMeatVisualFactory
{
    const string Root = "World/RawBoarMeat/Meshy_AI_Raw_ribeye_roast_0714194927_texture_fbx/";
    const string ModelPath = Root + "Meshy_AI_Raw_ribeye_roast_0714194927_texture";
    const string AlbedoPath = Root + "Meshy_AI_Raw_ribeye_roast_0714194927_texture";
    const string NormalPath = Root + "Meshy_AI_Raw_ribeye_roast_0714194927_texture_normal";
    const string MetallicPath = Root + "Meshy_AI_Raw_ribeye_roast_0714194927_texture_metallic";

    static Material runtimeMaterial;

    public static bool IsRawBoarMeat(string itemName)
    {
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(itemName.Trim(), BoarMeatItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static GameObject Spawn(Vector3 position, Item source, LayerMask groundMask, float collectRadius, Vector3 impulse)
    {
        GameObject drop = new GameObject("CarneCruaJavaliDrop");
        drop.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        CopyItemData(drop, source ?? BoarMeatItemRegistry.GetOrCreate());
        BuildVisual(drop.transform, 0.62f, drop.transform.position);

        BoxCollider collider = drop.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.1f, 0f);
        collider.size = new Vector3(0.56f, 0.28f, 0.44f);

        Rigidbody body = drop.AddComponent<Rigidbody>();
        body.mass = 0.12f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.AddForce(Vector3.up * 1.2f + impulse, ForceMode.Impulse);

        FloatingPickup pickup = drop.AddComponent<FloatingPickup>();
        pickup.groundMask = groundMask;
        pickup.collectRadius = collectRadius;
        pickup.hoverHeight = 0.58f;
        pickup.bobHeight = 0.08f;
        pickup.rotationSpeed = 46f;
        return drop;
    }

    public static Sprite CreateThumbnailSprite(int textureSize = 96)
    {
        GameObject prefab = Application.isPlaying ? Resources.Load<GameObject>(ModelPath) : null;
        if (prefab == null)
            return null;

        int size = Mathf.Clamp(textureSize, 48, 256);
        Vector3 origin = new Vector3(0f, -9960f, 0f);
        GameObject root = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            root = new GameObject("RawBoarMeatThumbnailRoot");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;
            BuildVisual(root.transform, 1.5f, origin);
            if (!TryGetBounds(root.transform, out Bounds bounds))
                return null;

            Vector3 direction = new Vector3(1.2f, 1.05f, -2.15f).normalized;
            cameraObject = new GameObject("RawBoarMeatThumbnailCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.3f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 25f;
            camera.allowHDR = false;
            camera.enabled = false;
            cameraObject.transform.position = bounds.center - direction * 4.2f;
            cameraObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            lightObject = new GameObject("RawBoarMeatThumbnailLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.color = new Color(1f, 0.9f, 0.8f);
            lightObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply(false, false);
            texture.name = "CarneCruaJavaliModelThumbnailTexture";
            Sprite result = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            result.name = "CarneCruaJavaliModelThumbnailSprite";
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
        GameObject root = new GameObject("RawBoarMeatVisual");
        root.transform.SetParent(parent, false);
        GameObject model = Object.Instantiate(prefab, root.transform);
        model.name = "MeshyRawBoarMeatModel";
        StripGameplayComponents(model);
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

    static void StripGameplayComponents(GameObject visual)
    {
        foreach (Collider value in visual.GetComponentsInChildren<Collider>(true)) Object.Destroy(value);
        foreach (Rigidbody value in visual.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(value);
        foreach (Animator value in visual.GetComponentsInChildren<Animator>(true)) Object.Destroy(value);
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
        runtimeMaterial = new Material(shader) { name = "RawBoarMeat_RuntimeMaterial" };
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
        Item item = target.AddComponent<Item>();
        item.itemName = source.itemName;
        item.icon = BoarMeatItemRegistry.GetSprite();
        item.itemType = source.itemType;
        item.category = source.GetCategory();
        item.rarity = source.GetRarity();
        item.description = source.GetDescription();
        item.weight = source.GetWeight();
        item.maxStack = source.GetMaxStack();
        item.toolType = source.toolType;
        item.toolDamage = source.toolDamage;
        item.buyPrice = source.GetBuyPrice();
        item.sellPrice = source.GetSellPrice();
    }
}
