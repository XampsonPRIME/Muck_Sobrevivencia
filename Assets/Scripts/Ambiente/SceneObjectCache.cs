using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneObjectCache
{
    static readonly Dictionary<string, Component> Cache = new Dictionary<string, Component>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Cache.Clear();
    }

    public static T Find<T>(Scene preferredScene, bool includeInactive = false) where T : Component
    {
        string key = BuildKey<T>(preferredScene.name, includeInactive);
        if (TryGetCached(key, out T cached))
            return cached;

        T resolved = FindInScene<T>(preferredScene, includeInactive);
        if (resolved == null)
            resolved = FindInLoadedScenes<T>(preferredScene, includeInactive);

        Cache[key] = resolved;
        return resolved;
    }

    public static T Find<T>(bool includeInactive = false) where T : Component
    {
        return Find<T>(SceneManager.GetActiveScene(), includeInactive);
    }

    static bool TryGetCached<T>(string key, out T component) where T : Component
    {
        if (Cache.TryGetValue(key, out Component cached) && cached != null)
        {
            component = cached as T;
            if (component != null)
                return true;
        }

        component = null;
        return false;
    }

    static T FindInLoadedScenes<T>(Scene preferredScene, bool includeInactive) where T : Component
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded || loadedScene == preferredScene)
                continue;

            T component = FindInScene<T>(loadedScene, includeInactive);
            if (component != null)
                return component;
        }

        if (includeInactive)
        {
            T[] inactiveObjects = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < inactiveObjects.Length; i++)
            {
                T candidate = inactiveObjects[i];
                if (candidate == null)
                    continue;

                Scene candidateScene = candidate.gameObject.scene;
                if (!candidateScene.IsValid() || !candidateScene.isLoaded)
                    continue;

                return candidate;
            }

            return null;
        }

        return Object.FindFirstObjectByType<T>();
    }

    static T FindInScene<T>(Scene scene, bool includeInactive) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;

            T component = roots[i].GetComponentInChildren<T>(includeInactive);
            if (component != null)
                return component;
        }

        return null;
    }

    static string BuildKey<T>(string sceneName, bool includeInactive)
    {
        return $"{typeof(T).FullName}|{sceneName}|{includeInactive}";
    }
}
