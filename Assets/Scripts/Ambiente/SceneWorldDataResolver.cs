using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneWorldDataResolver
{
    public static WorldHeightmapData ResolveHeightmapData(Scene scene)
    {
        string sceneName = GetSceneName(scene);
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            WorldHeightmapData sceneData = Resources.Load<WorldHeightmapData>($"World/Scenes/{sceneName}/HeightmapData");
            if (sceneData != null)
                return sceneData;
        }

        return Resources.Load<WorldHeightmapData>("World/Scenes/DefaultHeightmapData");
    }

    public static RoadMaskData ResolveRoadMaskData(Scene scene)
    {
        string sceneName = GetSceneName(scene);
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            RoadMaskData sceneData = Resources.Load<RoadMaskData>($"World/Scenes/{sceneName}/RoadMaskData");
            if (sceneData != null)
                return sceneData;
        }

        return Resources.Load<RoadMaskData>("World/Scenes/DefaultRoadMaskData");
    }

    static string GetSceneName(Scene scene)
    {
        if (scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.name))
            return scene.name;

        return SceneManager.GetActiveScene().name;
    }
}
