using UnityEngine;

public static class CookedChickenMeatVisualFactory
{
    const string AssetRoot = "World/CookedChickenMeat/Meshy_AI_Ancient_Verdant_Guard_0714163805_texture_fbx/";
    public const string ModelPath = AssetRoot + "Meshy_AI_Ancient_Verdant_Guard_0714163805_texture";
    const string AlbedoPath = AssetRoot + "Meshy_AI_Ancient_Verdant_Guard_0714163805_texture";
    const string NormalPath = AssetRoot + "Meshy_AI_Ancient_Verdant_Guard_0714163805_texture_normal";
    const string MetallicPath = AssetRoot + "Meshy_AI_Ancient_Verdant_Guard_0714163805_texture_metallic";

    static Material runtimeMaterial;

    public static void AttachTo(Transform itemRoot)
    {
        if (itemRoot == null || itemRoot.Find("CookedChickenMeatVisual") != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(ModelPath);
        if (prefab == null)
            return;

        GameObject visualRoot = new GameObject("CookedChickenMeatVisual");
        visualRoot.transform.SetParent(itemRoot, false);

        GameObject model = Object.Instantiate(prefab, visualRoot.transform);
        model.name = "MeshyCookedChickenMeatModel";
        StripGameplayComponents(model);
        ApplyMaterial(model);
        Normalize(visualRoot.transform, 0.22f, Vector3.zero);
    }

    public static Sprite CreateThumbnailSprite(int textureSize = 96)
    {
        if (!Application.isPlaying || Resources.Load<GameObject>(ModelPath) == null)
            return null;

        int size = Mathf.Clamp(textureSize, 48, 256);
        Vector3 origin = new Vector3(0f, -9800f, 0f);
        GameObject root = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            root = new GameObject("CookedChickenMeatThumbnailRoot");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = origin;

            GameObject model = Object.Instantiate(Resources.Load<GameObject>(ModelPath), root.transform);
            StripGameplayComponents(model);
            ApplyMaterial(model);
            Normalize(root.transform, 1.5f, origin);

            if (!TryGetBounds(root.transform, out Bounds bounds))
                return null;

            Vector3 viewDirection = new Vector3(1.4f, 1.1f, -2.1f).normalized;
            cameraObject = new GameObject("CookedChickenMeatThumbnailCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.35f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 25f;
            camera.allowHDR = false;
            camera.enabled = false;
            cameraObject.transform.position = bounds.center - viewDirection * 4.2f;
            cameraObject.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);

            lightObject = new GameObject("CookedChickenMeatThumbnailLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.75f;
            light.color = new Color(1f, 0.9f, 0.78f);
            lightObject.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);

            target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply(false, false);
            texture.name = "CarneCozidaGalinhaModelThumbnailTexture";

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "CarneCozidaGalinhaModelThumbnailSprite";
            return sprite;
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

    static void Normalize(Transform root, float targetSize, Vector3 targetCenter)
    {
        if (!TryGetBounds(root, out Bounds bounds))
            return;

        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        if (bounds.size.y > horizontal * 1.25f)
            root.localRotation = Quaternion.Euler(0f, 0f, 90f);

        if (!TryGetBounds(root, out bounds))
            return;

        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDimension > 0.001f)
            root.localScale *= targetSize / maxDimension;

        if (TryGetBounds(root, out bounds))
            root.position += targetCenter - bounds.center;
    }

    static bool TryGetBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
        bounds = default;
        bool found = false;
        if (renderers == null)
            return false;

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
        foreach (Collider component in visual.GetComponentsInChildren<Collider>(true)) Object.Destroy(component);
        foreach (Rigidbody component in visual.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(component);
        foreach (Animator component in visual.GetComponentsInChildren<Animator>(true)) Object.Destroy(component);
    }

    static void ApplyMaterial(GameObject visual)
    {
        Material material = GetMaterial();
        if (material == null)
            return;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;
    }

    static Material GetMaterial()
    {
        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        runtimeMaterial = new Material(shader) { name = "CookedChickenMeat_RuntimeMaterial" };
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
        if (texture != null && runtimeMaterial.HasProperty(property))
            runtimeMaterial.SetTexture(property, texture);
    }
}
