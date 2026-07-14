using UnityEngine;

public static class RawChickenMeatVisualFactory
{
    public const string ModelPath = "World/RawChickenMeat/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture";
    public const string AlbedoPath = "World/RawChickenMeat/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture";
    public const string NormalPath = "World/RawChickenMeat/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture_normal";
    public const string MetallicPath = "World/RawChickenMeat/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture_fbx/Meshy_AI_Ancient_Verdant_Guard_0713000217_texture_metallic";

    const float TargetVisualSize = 0.56f;
    const float VisualBottomOffset = 0.05f;

    static Material runtimeMaterial;

    public static bool IsRawChickenMeat(string candidateItemName)
    {
        return !string.IsNullOrWhiteSpace(candidateItemName) &&
               string.Equals(candidateItemName.Trim(), RawChickenMeatItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static Sprite CreateThumbnailSprite(int textureSize = 96)
    {
        if (!Application.isPlaying)
            return null;

        GameObject modelPrefab = Resources.Load<GameObject>(ModelPath);
        if (modelPrefab == null)
            return null;

        int size = Mathf.Clamp(textureSize, 48, 256);
        Vector3 thumbnailOrigin = new Vector3(0f, -9700f, 0f);
        GameObject root = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            root = new GameObject("RawChickenMeatThumbnailRoot");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = thumbnailOrigin;

            GameObject model = Object.Instantiate(modelPrefab, root.transform);
            model.name = "RawChickenMeatThumbnailModel";
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            StripVisualGameplayComponents(model);
            ApplyMaterial(model);
            LayDownIfTall(root.transform);
            NormalizeThumbnailModel(root.transform, thumbnailOrigin);

            cameraObject = new GameObject("RawChickenMeatThumbnailCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 25f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.enabled = false;

            if (!TryGetRendererBounds(root.transform, out Bounds bounds))
                return null;

            Vector3 viewDirection = new Vector3(1.4f, 1.1f, -2.1f).normalized;
            cameraObject.transform.position = bounds.center - viewDirection * 4.2f;
            cameraObject.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);
            camera.orthographicSize = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.35f;

            lightObject = new GameObject("RawChickenMeatThumbnailLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.75f;
            light.color = new Color(1f, 0.93f, 0.86f, 1f);
            lightObject.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);

            renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();

            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply(false, false);
            texture.name = "CarneCruaGalinhaModelThumbnailTexture";

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "CarneCruaGalinhaModelThumbnailSprite";
            return sprite;
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.Destroy(renderTexture);
            }

            if (cameraObject != null)
                Object.Destroy(cameraObject);
            if (lightObject != null)
                Object.Destroy(lightObject);
            if (root != null)
                Object.Destroy(root);
        }
    }

    public static GameObject Spawn(Vector3 position, Item sourceItem, LayerMask groundMask, float collectRadius, Vector3 scatterImpulse)
    {
        GameObject drop = new GameObject("CarneCruaGalinhaDrop");
        drop.transform.position = position;
        drop.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        CopyItemData(drop, sourceItem != null ? sourceItem : RawChickenMeatItemRegistry.GetOrCreate());
        BuildVisual(drop.transform);

        BoxCollider collider = drop.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.12f, 0f);
        collider.size = new Vector3(0.54f, 0.3f, 0.42f);

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.mass = 0.08f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce(Vector3.up * 1.2f + scatterImpulse, ForceMode.Impulse);

        FloatingPickup floatingPickup = drop.AddComponent<FloatingPickup>();
        floatingPickup.groundMask = groundMask;
        floatingPickup.collectRadius = collectRadius;
        floatingPickup.hoverHeight = 0.54f;
        floatingPickup.bobHeight = 0.07f;
        floatingPickup.rotationSpeed = 50f;

        return drop;
    }

    static void BuildVisual(Transform parent)
    {
        GameObject visualRoot = new GameObject("RawChickenMeatDropVisual");
        visualRoot.transform.SetParent(parent, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        GameObject modelPrefab = Resources.Load<GameObject>(ModelPath);
        if (modelPrefab != null)
        {
            GameObject model = Object.Instantiate(modelPrefab, visualRoot.transform);
            model.name = "MeshyRawChickenMeatModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            StripVisualGameplayComponents(model);
            ApplyMaterial(model);
            LayDownIfTall(visualRoot.transform);
            NormalizeVisual(visualRoot.transform, parent.position);
            return;
        }

        CreateFallbackMeat(visualRoot.transform);
    }

    static void LayDownIfTall(Transform visualRoot)
    {
        if (!TryGetRendererBounds(visualRoot, out Bounds bounds))
            return;

        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        if (bounds.size.y <= horizontal * 1.25f)
            return;

        visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    static void NormalizeVisual(Transform visualRoot, Vector3 parentPosition)
    {
        if (!TryGetRendererBounds(visualRoot, out Bounds bounds))
            return;

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension > 0.001f)
            visualRoot.localScale *= TargetVisualSize / maxDimension;

        if (!TryGetRendererBounds(visualRoot, out bounds))
            return;

        Vector3 offset = new Vector3(
            parentPosition.x - bounds.center.x,
            parentPosition.y + VisualBottomOffset - bounds.min.y,
            parentPosition.z - bounds.center.z);
        visualRoot.position += offset;
    }

    static void NormalizeThumbnailModel(Transform visualRoot, Vector3 targetCenter)
    {
        if (!TryGetRendererBounds(visualRoot, out Bounds bounds))
            return;

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension > 0.001f)
            visualRoot.localScale *= 1.5f / maxDimension;

        if (!TryGetRendererBounds(visualRoot, out bounds))
            return;

        visualRoot.position += targetCenter - bounds.center;
    }

    static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
        bounds = default;
        if (renderers == null || renderers.Length == 0)
            return false;

        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    static void StripVisualGameplayComponents(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                Object.Destroy(colliders[i]);
        }

        Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                Object.Destroy(bodies[i]);
        }

        Animator[] animators = visual.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                Object.Destroy(animators[i]);
        }
    }

    static void ApplyMaterial(GameObject visual)
    {
        Material material = GetMaterial();
        if (material == null)
            return;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].sharedMaterial = material;
        }
    }

    static Material GetMaterial()
    {
        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        runtimeMaterial = new Material(shader)
        {
            name = "RawChickenMeatDrop_RuntimeMaterial"
        };

        Texture2D albedo = Resources.Load<Texture2D>(AlbedoPath);
        Texture2D normal = Resources.Load<Texture2D>(NormalPath);
        Texture2D metallic = Resources.Load<Texture2D>(MetallicPath);

        SetTexture(runtimeMaterial, "_BaseMap", albedo);
        SetTexture(runtimeMaterial, "_MainTex", albedo);
        SetTexture(runtimeMaterial, "_BumpMap", normal);
        SetTexture(runtimeMaterial, "_MetallicGlossMap", metallic);

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", Color.white);
        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", Color.white);

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

    static void CreateFallbackMeat(Transform parent)
    {
        GameObject meat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        meat.name = "FallbackRawChickenMeat";
        meat.transform.SetParent(parent, false);
        meat.transform.localPosition = Vector3.zero;
        meat.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        meat.transform.localScale = new Vector3(0.38f, 0.18f, 0.28f);

        Renderer renderer = meat.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateFallbackMaterial();

        Collider collider = meat.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);
    }

    static Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        return new Material(shader)
        {
            name = "RawChickenMeatDrop_FallbackMaterial",
            color = new Color(0.72f, 0.18f, 0.16f, 1f)
        };
    }

    static void CopyItemData(GameObject target, Item source)
    {
        Item item = target.GetComponent<Item>();
        if (item == null)
            item = target.AddComponent<Item>();

        if (source == null)
            source = RawChickenMeatItemRegistry.GetOrCreate();

        item.itemName = source.itemName;
        item.icon = RawChickenMeatItemRegistry.GetSprite();
        item.itemType = source.itemType;
        item.category = source.GetCategory();
        item.rarity = source.GetRarity();
        item.description = source.GetDescription();
        item.weight = source.GetWeight();
        item.maxStack = source.GetMaxStack();
        item.toolType = source.toolType;
        item.toolDamage = source.toolDamage;
        item.equipmentSlot = source.GetEquipmentSlot();
        item.defense = source.GetDefense();
        item.durability = source.GetDurability();
        item.moveSpeedBonus = source.GetMoveSpeedBonus();
        item.buyPrice = source.GetBuyPrice();
        item.sellPrice = source.GetSellPrice();
    }
}
