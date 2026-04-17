using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class SceneWorldDataResolver
{
    static readonly Dictionary<string, WorldHeightmapData> HeightmapCache = new Dictionary<string, WorldHeightmapData>();
    static readonly Dictionary<string, RoadMaskData> RoadMaskCache = new Dictionary<string, RoadMaskData>();
    static readonly Dictionary<string, TreeExclusionMaskData> TreeExclusionMaskCache = new Dictionary<string, TreeExclusionMaskData>();

    public static WorldHeightmapData ResolveHeightmapData(Scene scene)
    {
        string sceneName = ResolveWorldSceneName(scene);
        if (!string.IsNullOrWhiteSpace(sceneName))
            return LoadSceneHeightmap(sceneName);

        return LoadDefaultHeightmap();
    }

    public static RoadMaskData ResolveRoadMaskData(Scene scene)
    {
        string sceneName = ResolveWorldSceneName(scene);
        if (!string.IsNullOrWhiteSpace(sceneName))
            return LoadSceneRoadMask(sceneName);

        return LoadDefaultRoadMask();
    }

    public static TreeExclusionMaskData ResolveTreeExclusionMaskData(Scene scene)
    {
        string sceneName = ResolveWorldSceneName(scene);
        if (!string.IsNullOrWhiteSpace(sceneName))
            return LoadSceneTreeExclusionMask(sceneName);

        return LoadDefaultTreeExclusionMask();
    }

    public static string ResolveWorldSceneName(Scene scene)
    {
        if (IsUsableWorldScene(scene))
            return scene.name;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (!IsUsableWorldScene(loadedScene))
                continue;

            if (SceneHasWorldGenerator(loadedScene))
                return loadedScene.name;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (IsUsableWorldScene(loadedScene))
                return loadedScene.name;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() ? activeScene.name : string.Empty;
    }

    static WorldHeightmapData LoadSceneHeightmap(string sceneName)
    {
        if (HeightmapCache.TryGetValue(sceneName, out WorldHeightmapData cached))
            return cached != null ? cached : LoadDefaultHeightmap();

        WorldHeightmapData sceneData = Resources.Load<WorldHeightmapData>($"World/Scenes/{sceneName}/HeightmapData");
        HeightmapCache[sceneName] = sceneData;
        return sceneData != null ? sceneData : LoadDefaultHeightmap();
    }

    static RoadMaskData LoadSceneRoadMask(string sceneName)
    {
        if (RoadMaskCache.TryGetValue(sceneName, out RoadMaskData cached))
            return cached != null ? cached : LoadDefaultRoadMask();

        RoadMaskData sceneData = Resources.Load<RoadMaskData>($"World/Scenes/{sceneName}/RoadMaskData");
        RoadMaskCache[sceneName] = sceneData;
        return sceneData != null ? sceneData : LoadDefaultRoadMask();
    }

    static TreeExclusionMaskData LoadSceneTreeExclusionMask(string sceneName)
    {
        if (TreeExclusionMaskCache.TryGetValue(sceneName, out TreeExclusionMaskData cached))
            return cached != null ? cached : LoadDefaultTreeExclusionMask();

        TreeExclusionMaskData sceneData = Resources.Load<TreeExclusionMaskData>($"World/Scenes/{sceneName}/TreeExclusionMaskData");
        TreeExclusionMaskCache[sceneName] = sceneData;
        return sceneData != null ? sceneData : LoadDefaultTreeExclusionMask();
    }

    static WorldHeightmapData LoadDefaultHeightmap()
    {
        const string defaultKey = "__default";
        if (!HeightmapCache.TryGetValue(defaultKey, out WorldHeightmapData cached))
        {
            cached = Resources.Load<WorldHeightmapData>("World/Scenes/DefaultHeightmapData");
            HeightmapCache[defaultKey] = cached;
        }

        return cached;
    }

    static RoadMaskData LoadDefaultRoadMask()
    {
        const string defaultKey = "__default";
        if (!RoadMaskCache.TryGetValue(defaultKey, out RoadMaskData cached))
        {
            cached = Resources.Load<RoadMaskData>("World/Scenes/DefaultRoadMaskData");
            RoadMaskCache[defaultKey] = cached;
        }

        return cached;
    }

    static TreeExclusionMaskData LoadDefaultTreeExclusionMask()
    {
        const string defaultKey = "__default";
        if (!TreeExclusionMaskCache.TryGetValue(defaultKey, out TreeExclusionMaskData cached))
        {
            cached = Resources.Load<TreeExclusionMaskData>("World/Scenes/DefaultTreeExclusionMaskData");
            TreeExclusionMaskCache[defaultKey] = cached;
        }

        return cached;
    }

    static bool IsUsableWorldScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.name))
            return false;

        return !IsUtilitySceneName(scene.name);
    }

    static bool IsUtilitySceneName(string sceneName)
    {
        return sceneName == "PlayerTest" ||
               sceneName == "DontDestroyOnLoad" ||
               sceneName == "InitTestScene";
    }

    static bool SceneHasWorldGenerator(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;

            if (roots[i].GetComponentInChildren<WorldGenerator>(true) != null)
                return true;
        }

        return false;
    }
}
