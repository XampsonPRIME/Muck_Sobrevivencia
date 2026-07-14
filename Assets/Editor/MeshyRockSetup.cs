using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MeshyRockSetup
{
    const string SourceFolder = "Assets/Modelos3d/MeshyFantasyRock/Meshy_AI_Stylized_fantasy_rock_0630231017_texture_fbx";
    const string ModelPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_rock_0630231017_texture.fbx";
    const string AlbedoPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_rock_0630231017_texture.png";
    const string EmissionPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_rock_0630231017_texture_emission.png";
    const string MetallicPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_rock_0630231017_texture_metallic.png";
    const string NormalPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_rock_0630231017_texture_normal.png";
    const string RoughnessPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_rock_0630231017_texture_roughness.png";
    const string MaterialPath = "Assets/Materials/MeshyFantasyRock.mat";
    const string VisualName = "MeshyFantasyRockVisual";
    const string AutoSetupKey = "Elarion.MeshyRockSetup.AutoConfigured.v1";

    static bool setupScheduled;

    struct RockPrefabConfig
    {
        public readonly string prefabPath;
        public readonly float targetDiameter;
        public readonly float rootScale;

        public RockPrefabConfig(string prefabPath, float targetDiameter, float rootScale = 1f)
        {
            this.prefabPath = prefabPath;
            this.targetDiameter = targetDiameter;
            this.rootScale = rootScale;
        }
    }

    static readonly RockPrefabConfig[] Prefabs =
    {
        new RockPrefabConfig("Assets/Prefabs/rock_01_p.prefab", 2.55f, 0.55f),
        new RockPrefabConfig("Assets/Prefabs/rock_02_p.prefab", 3.75f, 0.55f),
        new RockPrefabConfig("Assets/Prefabs/rock_03_p.prefab", 2.85f, 0.55f),
        new RockPrefabConfig("Assets/Prefabs/rock.prefab", 2.55f),
        new RockPrefabConfig("Assets/Prefabs/rock_m.prefab", 4.1f),
        new RockPrefabConfig("Assets/Prefabs/rock_g.prefab", 6.05f),
    };

    static MeshyRockSetup()
    {
        ScheduleAutoSetup();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                ScheduleAutoSetup();
        };
    }

    [MenuItem("Elarion/Meshy/Configurar Pedra Meshy")]
    public static void ConfigureFromMenu()
    {
        Configure(true);
    }

    public static void ScheduleAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || setupScheduled)
            return;

        setupScheduled = true;
        EditorApplication.delayCall += () =>
        {
            setupScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Configure(false);
        };
    }

    public static void Configure(bool verbose)
    {
        if (!verbose && SessionState.GetBool(AutoSetupKey, false))
            return;

        if (!File.Exists(ModelPath))
            return;

        bool importersChanged = ConfigureImporters();
        Material material = EnsureMaterial();
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        }

        if (modelAsset == null || material == null)
            return;

        for (int i = 0; i < Prefabs.Length; i++)
            ApplyPrefab(Prefabs[i], modelAsset, material);

        AssetDatabase.SaveAssets();
        if (importersChanged)
            AssetDatabase.Refresh();

        SessionState.SetBool(AutoSetupKey, true);

        if (verbose)
            Debug.Log("Pedra Meshy configurada nos prefabs de pedra do mundo.");
    }

    static bool ConfigureImporters()
    {
        bool changed = false;
        changed |= ConfigureFbxImporter(ModelPath);
        changed |= ConfigureTextureImporter(AlbedoPath, TextureImporterType.Default, true, 1024);
        changed |= ConfigureTextureImporter(EmissionPath, TextureImporterType.Default, true, 512);
        changed |= ConfigureTextureImporter(MetallicPath, TextureImporterType.Default, false, 512);
        changed |= ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false, 1024);
        changed |= ConfigureTextureImporter(RoughnessPath, TextureImporterType.Default, false, 512);
        return changed;
    }

    static bool ConfigureFbxImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return false;

        bool dirty = false;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            dirty = true;
        }

        if (importer.importAnimation)
        {
            importer.importAnimation = false;
            dirty = true;
        }

        if (importer.animationType != ModelImporterAnimationType.None)
        {
            importer.animationType = ModelImporterAnimationType.None;
            dirty = true;
        }

        if (importer.importCameras)
        {
            importer.importCameras = false;
            dirty = true;
        }

        if (importer.importLights)
        {
            importer.importLights = false;
            dirty = true;
        }

        if (importer.importVisibility)
        {
            importer.importVisibility = false;
            dirty = true;
        }

        if (importer.importBlendShapes)
        {
            importer.importBlendShapes = false;
            dirty = true;
        }

        if (importer.meshCompression != ModelImporterMeshCompression.Medium)
        {
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            dirty = true;
        }

        if (importer.isReadable)
        {
            importer.isReadable = false;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static bool ConfigureTextureImporter(string assetPath, TextureImporterType type, bool sRgb, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool dirty = false;

        if (importer.textureType != type)
        {
            importer.textureType = type;
            dirty = true;
        }

        if (importer.sRGBTexture != sRgb)
        {
            importer.sRGBTexture = sRgb;
            dirty = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            dirty = true;
        }

        if (importer.maxTextureSize != maxSize)
        {
            importer.maxTextureSize = maxSize;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            dirty = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.ToNearest)
        {
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static Material EnsureMaterial()
    {
        EnsureFolder("Assets/Materials");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MeshyFantasyRock" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
        {
            material.shader = shader;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);

        SetTexture(material, "_BaseMap", albedo);
        SetTexture(material, "_MainTex", albedo);
        SetColor(material, "_BaseColor", Color.white);
        SetColor(material, "_Color", Color.white);

        SetTexture(material, "_BumpMap", normal);
        if (normal != null)
            material.EnableKeyword("_NORMALMAP");

        SetTexture(material, "_MetallicGlossMap", metallic);
        if (metallic != null)
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

        SetFloat(material, "_Metallic", 0.15f);
        SetFloat(material, "_Smoothness", 0.32f);
        SetFloat(material, "_Glossiness", 0.32f);

        SetTexture(material, "_EmissionMap", emission);
        if (emission != null)
        {
            material.EnableKeyword("_EMISSION");
            SetColor(material, "_EmissionColor", new Color(0.35f, 0.45f, 0.35f));
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplyPrefab(RockPrefabConfig config, GameObject modelAsset, Material material)
    {
        if (!File.Exists(config.prefabPath))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(config.prefabPath);
        if (root == null)
            return;

        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            root.name = Path.GetFileNameWithoutExtension(config.prefabPath);
            root.transform.localScale = Vector3.one * Mathf.Max(0.01f, config.rootScale);
            RemoveRootVisualComponents(root);

            Transform visualTransform = root.transform.Find(VisualName);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child != visualTransform)
                    Object.DestroyImmediate(child.gameObject);
            }

            GameObject visual = visualTransform != null ? visualTransform.gameObject : null;
            if (visual == null)
            {
                visual = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
                if (visual == null)
                    visual = Object.Instantiate(modelAsset, root.transform);

                visual.name = VisualName;
            }

            NormalizeVisual(root.transform, visual, config.targetDiameter);
            ApplyMaterial(visual, material);
            FitCollider(root);

            PrefabUtility.SaveAsPrefabAsset(root, config.prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void RemoveRootVisualComponents(GameObject root)
    {
        RemoveComponents<Renderer>(root);
        RemoveComponents<MeshFilter>(root);
        RemoveComponents<Animator>(root);
        RemoveComponents<Animation>(root);
    }

    static void RemoveComponents<T>(GameObject root) where T : Component
    {
        T[] components = root.GetComponents<T>();
        for (int i = components.Length - 1; i >= 0; i--)
            Object.DestroyImmediate(components[i]);
    }

    static void NormalizeVisual(Transform root, GameObject visual, float targetDiameter)
    {
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        visual.transform.localScale = Vector3.one;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (!TryCalculateLocalBounds(root, renderers, out Bounds bounds))
            return;

        float currentDiameter = Mathf.Max(bounds.size.x, bounds.size.z);
        if (currentDiameter > 0.001f)
            visual.transform.localScale = Vector3.one * (targetDiameter / currentDiameter);

        if (!TryCalculateLocalBounds(root, renderers, out bounds))
            return;

        visual.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
    }

    static void ApplyMaterial(GameObject visual, Material material)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                materials = new[] { material };
            }
            else
            {
                for (int j = 0; j < materials.Length; j++)
                    materials[j] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    static void FitCollider(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (!TryCalculateLocalBounds(root.transform, renderers, out Bounds bounds))
            return;

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
            collider = root.AddComponent<BoxCollider>();

        Vector3 size = bounds.size;
        size.x = Mathf.Max(0.25f, size.x * 0.9f);
        size.y = Mathf.Max(0.25f, size.y * 0.95f);
        size.z = Mathf.Max(0.25f, size.z * 0.9f);

        collider.isTrigger = false;
        collider.center = bounds.center;
        collider.size = size;
    }

    static bool TryCalculateLocalBounds(Transform root, Renderer[] renderers, out Bounds localBounds)
    {
        localBounds = default;
        bool initialized = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, -extents.y, extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, extents.y, extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, -extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, -extents.y, extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, extents.y, extents.z)), ref localBounds, ref initialized);
        }

        return initialized;
    }

    static void Encapsulate(Vector3 point, ref Bounds bounds, ref bool initialized)
    {
        if (!initialized)
        {
            bounds = new Bounds(point, Vector3.zero);
            initialized = true;
            return;
        }

        bounds.Encapsulate(point);
    }

    static void SetTexture(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string folder = Path.GetFileName(assetPath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}

[InitializeOnLoad]
public static class MeshyIronSetup
{
    const string SourceFolder = "Assets/Resources/World/IronOre/MeshyIronOre/Meshy_AI_Stylized_fantasy_mini_0630231038_texture_fbx";
    const string ModelPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_mini_0630231038_texture.fbx";
    const string AlbedoPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_mini_0630231038_texture.png";
    const string EmissionPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_mini_0630231038_texture_emission.png";
    const string MetallicPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_mini_0630231038_texture_metallic.png";
    const string NormalPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_mini_0630231038_texture_normal.png";
    const string RoughnessPath = SourceFolder + "/Meshy_AI_Stylized_fantasy_mini_0630231038_texture_roughness.png";
    const string MaterialPath = "Assets/Resources/World/IronOre/MeshyIronOre/MeshyIronOre_Material.mat";
    const string AutoSetupKey = "Elarion.MeshyIronSetup.AutoConfigured.v1";

    static bool setupScheduled;

    static MeshyIronSetup()
    {
        ScheduleAutoSetup();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                ScheduleAutoSetup();
        };
    }

    [MenuItem("Elarion/Meshy/Configurar Ferro Meshy")]
    public static void ConfigureFromMenu()
    {
        Configure(true);
    }

    public static void ScheduleAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || setupScheduled)
            return;

        setupScheduled = true;
        EditorApplication.delayCall += () =>
        {
            setupScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Configure(false);
        };
    }

    public static void Configure(bool verbose)
    {
        if (!verbose && SessionState.GetBool(AutoSetupKey, false))
            return;

        if (!File.Exists(ModelPath))
            return;

        AssetDatabase.ImportAsset(SourceFolder, ImportAssetOptions.ImportRecursive);

        bool importersChanged = ConfigureImporters();
        EnsureMaterial();

        AssetDatabase.SaveAssets();
        if (importersChanged)
            AssetDatabase.Refresh();

        SessionState.SetBool(AutoSetupKey, true);

        if (verbose)
            Debug.Log("Ferro Meshy configurado para os nodes de minerio do mundo.");
    }

    static bool ConfigureImporters()
    {
        bool changed = false;
        changed |= ConfigureFbxImporter(ModelPath);
        changed |= ConfigureTextureImporter(AlbedoPath, TextureImporterType.Default, true, 1024);
        changed |= ConfigureTextureImporter(EmissionPath, TextureImporterType.Default, true, 512);
        changed |= ConfigureTextureImporter(MetallicPath, TextureImporterType.Default, false, 512);
        changed |= ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false, 1024);
        changed |= ConfigureTextureImporter(RoughnessPath, TextureImporterType.Default, false, 512);
        return changed;
    }

    static bool ConfigureFbxImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return false;

        bool dirty = false;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            dirty = true;
        }

        if (importer.importAnimation)
        {
            importer.importAnimation = false;
            dirty = true;
        }

        if (importer.animationType != ModelImporterAnimationType.None)
        {
            importer.animationType = ModelImporterAnimationType.None;
            dirty = true;
        }

        if (importer.importCameras)
        {
            importer.importCameras = false;
            dirty = true;
        }

        if (importer.importLights)
        {
            importer.importLights = false;
            dirty = true;
        }

        if (importer.importVisibility)
        {
            importer.importVisibility = false;
            dirty = true;
        }

        if (importer.importBlendShapes)
        {
            importer.importBlendShapes = false;
            dirty = true;
        }

        if (importer.meshCompression != ModelImporterMeshCompression.High)
        {
            importer.meshCompression = ModelImporterMeshCompression.High;
            dirty = true;
        }

        if (importer.isReadable)
        {
            importer.isReadable = false;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static bool ConfigureTextureImporter(string assetPath, TextureImporterType type, bool sRgb, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool dirty = false;

        if (importer.textureType != type)
        {
            importer.textureType = type;
            dirty = true;
        }

        if (importer.sRGBTexture != sRgb)
        {
            importer.sRGBTexture = sRgb;
            dirty = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            dirty = true;
        }

        if (importer.maxTextureSize != maxSize)
        {
            importer.maxTextureSize = maxSize;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            dirty = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.ToNearest)
        {
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static Material EnsureMaterial()
    {
        EnsureFolder("Assets/Resources/World/IronOre/MeshyIronOre");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MeshyIronOre_Material" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
        {
            material.shader = shader;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);

        SetTexture(material, "_BaseMap", albedo);
        SetTexture(material, "_MainTex", albedo);
        SetColor(material, "_BaseColor", Color.white);
        SetColor(material, "_Color", Color.white);

        SetTexture(material, "_BumpMap", normal);
        if (normal != null)
            material.EnableKeyword("_NORMALMAP");

        SetTexture(material, "_MetallicGlossMap", metallic);
        if (metallic != null)
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

        SetFloat(material, "_Metallic", 0.45f);
        SetFloat(material, "_Smoothness", 0.38f);
        SetFloat(material, "_Glossiness", 0.38f);

        SetTexture(material, "_EmissionMap", emission);
        if (emission != null)
        {
            material.EnableKeyword("_EMISSION");
            SetColor(material, "_EmissionColor", new Color(0.18f, 0.14f, 0.1f));
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetTexture(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string folder = Path.GetFileName(assetPath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
