using UnityEngine;

public static class BoarTuskVisualFactory
{
    const string Root = "World/BoarTusk/Meshy_AI_Ivory_Horns_Bound_in__0715005544_texture_fbx/";
    const string ModelPath = Root + "Meshy_AI_Ivory_Horns_Bound_in__0715005544_texture";
    const string AlbedoPath = Root + "Meshy_AI_Ivory_Horns_Bound_in__0715005544_texture";
    const string NormalPath = Root + "Meshy_AI_Ivory_Horns_Bound_in__0715005544_texture_normal";
    const string MetallicPath = Root + "Meshy_AI_Ivory_Horns_Bound_in__0715005544_texture_metallic";
    static Material runtimeMaterial;

    public static bool IsBoarTusk(string itemName)
    {
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(itemName.Trim(), SharpTuskItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static GameObject Spawn(Vector3 position, Item source, LayerMask groundMask, float collectRadius, Vector3 impulse)
    {
        GameObject drop = new GameObject("PresaAfiadaJavaliDrop");
        drop.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        CopyItemData(drop, source ?? SharpTuskItemRegistry.GetOrCreate());
        BuildVisual(drop.transform, 0.64f, drop.transform.position);

        BoxCollider collider = drop.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.1f, 0f);
        collider.size = new Vector3(0.58f, 0.3f, 0.42f);
        Rigidbody body = drop.AddComponent<Rigidbody>();
        body.mass = 0.09f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.AddForce(Vector3.up * 1.25f + impulse, ForceMode.Impulse);

        FloatingPickup pickup = drop.AddComponent<FloatingPickup>();
        pickup.groundMask = groundMask;
        pickup.collectRadius = collectRadius;
        pickup.hoverHeight = 0.6f;
        pickup.bobHeight = 0.08f;
        pickup.rotationSpeed = 48f;
        return drop;
    }

    public static Sprite CreateThumbnailSprite(int textureSize = 96)
    {
        GameObject prefab = Application.isPlaying ? Resources.Load<GameObject>(ModelPath) : null;
        if (prefab == null) return null;

        int size = Mathf.Clamp(textureSize, 48, 256);
        Vector3 origin = new Vector3(0f, -9980f, 0f);
        GameObject root = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            root = new GameObject("BoarTuskThumbnailRoot");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;
            BuildVisual(root.transform, 1.5f, origin);
            if (!TryGetBounds(root.transform, out Bounds bounds)) return null;

            Vector3 direction = new Vector3(1f, 1.15f, -2.15f).normalized;
            cameraObject = new GameObject("BoarTuskThumbnailCamera");
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

            lightObject = new GameObject("BoarTuskThumbnailLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.85f;
            light.color = new Color(1f, 0.97f, 0.88f);
            lightObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply(false, false);
            texture.name = "PresaAfiadaJavaliModelThumbnailTexture";
            Sprite result = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            result.name = "PresaAfiadaJavaliModelThumbnailSprite";
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
        GameObject root = new GameObject("BoarTuskVisual");
        root.transform.SetParent(parent, false);
        GameObject model = Object.Instantiate(prefab, root.transform);
        model.name = "MeshyBoarTuskModel";
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
        runtimeMaterial = new Material(shader) { name = "BoarTusk_RuntimeMaterial" };
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
        item.icon = SharpTuskItemRegistry.GetSprite();
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
