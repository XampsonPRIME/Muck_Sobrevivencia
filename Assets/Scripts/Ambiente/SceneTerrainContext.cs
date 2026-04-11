using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTerrainContext
{
    static string cachedSceneName;
    static SceneTerrainSettings cachedSettings;
    static WorldHeightmapData cachedHeightmap;
    static RoadMaskData cachedRoadMask;
    static SceneBiomeOverride cachedBiomeOverride;
    static float cachedTreeDensityMultiplier = 1f;
    static int cachedMaxTreesPerChunkOverride;
    static float cachedMinTreeDistanceOverride;
    static float cachedMushroomDensityMultiplier = 1f;
    static bool cachedPreferMagicForestTrees;
    static float cachedMagicForestTreeChance = 0.75f;

    public static WorldHeightmapData GetActiveHeightmap(WorldHeightmapData explicitOverride = null)
    {
        return GetHeightmapForScene(GetGameplayWorldScene(), explicitOverride);
    }

    public static SceneTerrainSettings GetActiveSettings()
    {
        return GetSettingsForScene(GetGameplayWorldScene());
    }

    public static SceneTerrainSettings GetSettingsForScene(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return cachedSettings;
    }

    public static WorldHeightmapData GetHeightmapForScene(Scene scene, WorldHeightmapData explicitOverride = null)
    {
        if (explicitOverride != null)
            return explicitOverride;

        RefreshCacheIfNeeded(scene);
        return cachedHeightmap;
    }

    public static RoadMaskData GetActiveRoadMask(RoadMaskData explicitOverride = null)
    {
        return GetRoadMaskForScene(GetGameplayWorldScene(), explicitOverride);
    }

    public static RoadMaskData GetRoadMaskForScene(Scene scene, RoadMaskData explicitOverride = null)
    {
        if (explicitOverride != null)
            return explicitOverride;

        RefreshCacheIfNeeded(scene);
        return cachedRoadMask;
    }

    public static SceneBiomeOverride GetActiveBiomeOverride()
    {
        return GetBiomeOverrideForScene(GetGameplayWorldScene());
    }

    public static SceneBiomeOverride GetBiomeOverrideForScene(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return cachedBiomeOverride;
    }

    public static float GetTreeDensityMultiplier(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return Mathf.Max(0f, cachedTreeDensityMultiplier);
    }

    public static int GetMaxTreesPerChunkOverride(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return Mathf.Max(0, cachedMaxTreesPerChunkOverride);
    }

    public static float GetMinTreeDistanceOverride(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return Mathf.Max(0f, cachedMinTreeDistanceOverride);
    }

    public static float GetMushroomDensityMultiplier(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return Mathf.Max(0f, cachedMushroomDensityMultiplier);
    }

    public static bool GetPreferMagicForestTrees(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return cachedPreferMagicForestTrees;
    }

    public static float GetMagicForestTreeChance(Scene scene)
    {
        RefreshCacheIfNeeded(scene);
        return Mathf.Clamp01(cachedMagicForestTreeChance);
    }

    public static Scene GetGameplayWorldScene()
    {
        WorldGenerator worldGenerator = WorldGenerator.Instance;
        if (worldGenerator != null && worldGenerator.gameObject.scene.IsValid())
            return worldGenerator.gameObject.scene;

        RiverSystem riverSystem = RiverSystem.Instance;
        if (riverSystem != null && riverSystem.gameObject.scene.IsValid())
            return riverSystem.gameObject.scene;

        return SceneManager.GetActiveScene();
    }

    static void RefreshCacheIfNeeded(Scene scene)
    {
        string activeSceneName = scene.IsValid() ? scene.name : string.Empty;

        if (cachedSceneName == activeSceneName)
            return;

        cachedSceneName = activeSceneName;
        cachedSettings = null;
        cachedHeightmap = null;
        cachedRoadMask = null;
        cachedBiomeOverride = SceneBiomeOverride.Default;
        cachedTreeDensityMultiplier = 1f;
        cachedMaxTreesPerChunkOverride = 0;
        cachedMinTreeDistanceOverride = 0f;
        cachedMushroomDensityMultiplier = 1f;
        cachedPreferMagicForestTrees = false;
        cachedMagicForestTreeChance = 0.75f;

        SceneTerrainSettings[] sceneSettings = Object.FindObjectsByType<SceneTerrainSettings>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneSettings.Length; i++)
        {
            SceneTerrainSettings settings = sceneSettings[i];
            if (settings == null || settings.gameObject.scene != scene)
                continue;

            cachedSettings = settings;

            if (settings.worldHeightmap != null)
                cachedHeightmap = settings.worldHeightmap;

            if (settings.roadMask != null)
                cachedRoadMask = settings.roadMask;

            cachedBiomeOverride = settings.biomeOverride;
            cachedTreeDensityMultiplier = settings.treeDensityMultiplier;
            cachedMaxTreesPerChunkOverride = settings.maxTreesPerChunkOverride;
            cachedMinTreeDistanceOverride = settings.minTreeDistanceOverride;
            cachedMushroomDensityMultiplier = settings.mushroomDensityMultiplier;
            cachedPreferMagicForestTrees = settings.preferMagicForestTrees;
            cachedMagicForestTreeChance = settings.magicForestTreeChance;

            break;
        }

        if (!string.IsNullOrWhiteSpace(activeSceneName))
        {
            cachedHeightmap ??= Resources.Load<WorldHeightmapData>($"World/Scenes/{activeSceneName}/HeightmapData");
            cachedRoadMask ??= Resources.Load<RoadMaskData>($"World/Scenes/{activeSceneName}/RoadMaskData");
        }

        cachedHeightmap ??= Resources.Load<WorldHeightmapData>("World/DefaultHeightmapData");
        cachedRoadMask ??= Resources.Load<RoadMaskData>("World/DefaultRoadMaskData");
    }
}
