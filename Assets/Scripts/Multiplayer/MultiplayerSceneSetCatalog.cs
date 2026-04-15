using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class MultiplayerSceneSetState
{
    public string sceneSetId;
    public string displayName;
    public string activeSceneName;
    public string[] sceneNames;
}

public static class MultiplayerSceneSetCatalog
{
    class SceneSetDefinition
    {
        public string id;
        public string displayName;
        public string activeSceneName;
        public string[] sceneNames;
    }

    static readonly SceneSetDefinition[] Definitions =
    {
        new SceneSetDefinition
        {
            id = "Overworld",
            displayName = "Overworld",
            activeSceneName = "Main",
            sceneNames = new[] { "Main", "PlayerTest" }
        },
        new SceneSetDefinition
        {
            id = "BossFight",
            displayName = "BossFight",
            activeSceneName = "Main",
            sceneNames = new[] { "Main", "PlayerTest", "Boss1" }
        },
        new SceneSetDefinition
        {
            id = "EnchantedForest",
            displayName = "Floresta Encantada",
            activeSceneName = "EnchantedForest",
            sceneNames = new[] { "EnchantedForest", "PlayerTest" }
        }
    };

    public static MultiplayerSceneSetState GetDefaultStartupState()
    {
        return CreateFromDefinition(FindById("Overworld"));
    }

    public static MultiplayerSceneSetState CaptureLoadedScenes()
    {
        List<string> loadedSceneNames = new List<string>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.name) || string.IsNullOrWhiteSpace(scene.path))
                continue;

            if (!loadedSceneNames.Contains(scene.name))
                loadedSceneNames.Add(scene.name);
        }

        if (loadedSceneNames.Count == 0)
            return null;

        string activeSceneName = SceneManager.GetActiveScene().name;
        SceneSetDefinition matchedDefinition = FindExactDefinition(loadedSceneNames);

        if (matchedDefinition != null)
        {
            return new MultiplayerSceneSetState
            {
                sceneSetId = matchedDefinition.id,
                displayName = matchedDefinition.displayName,
                activeSceneName = ResolveActiveSceneName(matchedDefinition.activeSceneName, matchedDefinition.sceneNames),
                sceneNames = (string[])matchedDefinition.sceneNames.Clone()
            };
        }

        return Normalize(new MultiplayerSceneSetState
        {
            activeSceneName = activeSceneName,
            sceneNames = loadedSceneNames.ToArray()
        });
    }

    public static MultiplayerSceneSetState ResolveStartupState(string sceneSetId, string sceneName)
    {
        MultiplayerSceneSetState defaultState = GetDefaultStartupState();

        if (!string.IsNullOrWhiteSpace(sceneSetId))
        {
            SceneSetDefinition definition = FindById(sceneSetId);
            if (definition != null)
                return CreateFromDefinition(definition);
        }

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            SceneSetDefinition mappedDefinition = FindByContainedScene(sceneName);
            if (mappedDefinition != null)
                return CreateFromDefinition(mappedDefinition);

            return Normalize(new MultiplayerSceneSetState
            {
                activeSceneName = sceneName,
                sceneNames = new[] { sceneName }
            });
        }

        return defaultState ?? CaptureLoadedScenes();
    }

    public static MultiplayerSceneSetState Normalize(MultiplayerSceneSetState state)
    {
        if (state == null)
            return null;

        List<string> uniqueScenes = new List<string>();
        if (state.sceneNames != null)
        {
            for (int i = 0; i < state.sceneNames.Length; i++)
            {
                string sceneName = state.sceneNames[i];
                if (string.IsNullOrWhiteSpace(sceneName))
                    continue;

                sceneName = sceneName.Trim();
                if (!uniqueScenes.Contains(sceneName))
                    uniqueScenes.Add(sceneName);
            }
        }

        if (uniqueScenes.Count == 0 && !string.IsNullOrWhiteSpace(state.activeSceneName))
            uniqueScenes.Add(state.activeSceneName.Trim());

        if (uniqueScenes.Count == 0)
            return null;

        SceneSetDefinition exactDefinition = FindExactDefinition(uniqueScenes);
        string activeSceneName = ResolveActiveSceneName(state.activeSceneName, uniqueScenes.ToArray());
        string displayName = string.IsNullOrWhiteSpace(state.displayName)
            ? BuildDisplayName(uniqueScenes)
            : state.displayName.Trim();
        string sceneSetId = string.IsNullOrWhiteSpace(state.sceneSetId) ? null : state.sceneSetId.Trim();

        if (exactDefinition != null)
        {
            sceneSetId = exactDefinition.id;
            displayName = exactDefinition.displayName;
            activeSceneName = ResolveActiveSceneName(exactDefinition.activeSceneName, exactDefinition.sceneNames);
            uniqueScenes = new List<string>(exactDefinition.sceneNames);
        }

        return new MultiplayerSceneSetState
        {
            sceneSetId = sceneSetId,
            displayName = displayName,
            activeSceneName = activeSceneName,
            sceneNames = uniqueScenes.ToArray()
        };
    }

    public static string BuildDisplayLabel(MultiplayerSceneSetState state)
    {
        MultiplayerSceneSetState normalized = Normalize(state);
        if (normalized == null)
            return "-";

        return string.IsNullOrWhiteSpace(normalized.displayName)
            ? BuildDisplayName(normalized.sceneNames)
            : normalized.displayName;
    }

    public static bool ApplyToRuntime(MultiplayerSceneSetState state)
    {
        MultiplayerSceneSetState normalized = Normalize(state);
        if (normalized == null || normalized.sceneNames == null || normalized.sceneNames.Length == 0)
            return false;

        string primarySceneName = ResolveActiveSceneName(normalized.activeSceneName, normalized.sceneNames);
        List<string> loadedSceneNamesBefore = GetLoadedSceneNames();
        bool primaryLoaded = IsSceneLoaded(primarySceneName);

        if (!primaryLoaded)
        {
            LoadSceneMode loadMode = loadedSceneNamesBefore.Count == 0
                ? LoadSceneMode.Single
                : LoadSceneMode.Additive;

            SceneManager.LoadScene(primarySceneName, loadMode);
        }

        for (int i = 0; i < normalized.sceneNames.Length; i++)
        {
            string sceneName = normalized.sceneNames[i];
            if (sceneName == primarySceneName)
                continue;

            if (!IsSceneLoaded(sceneName))
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }

        HashSet<string> targetScenes = new HashSet<string>(normalized.sceneNames, StringComparer.Ordinal);
        List<string> loadedSceneNames = GetLoadedSceneNames();
        for (int i = 0; i < loadedSceneNames.Count; i++)
        {
            string loadedSceneName = loadedSceneNames[i];
            if (loadedSceneName == primarySceneName || targetScenes.Contains(loadedSceneName))
                continue;

            SceneManager.UnloadSceneAsync(loadedSceneName);
        }

        Scene activeScene = SceneManager.GetSceneByName(primarySceneName);
        if (activeScene.IsValid() && activeScene.isLoaded)
            SceneManager.SetActiveScene(activeScene);

        return true;
    }

    public static bool LoadedScenesMatch(MultiplayerSceneSetState state)
    {
        MultiplayerSceneSetState normalized = Normalize(state);
        if (normalized == null)
            return false;

        List<string> loadedScenes = GetLoadedSceneNames();
        if (loadedScenes.Count != normalized.sceneNames.Length)
            return false;

        HashSet<string> loadedSet = new HashSet<string>(loadedScenes, StringComparer.Ordinal);
        for (int i = 0; i < normalized.sceneNames.Length; i++)
        {
            if (!loadedSet.Contains(normalized.sceneNames[i]))
                return false;
        }

        return true;
    }

    static List<string> GetLoadedSceneNames()
    {
        List<string> loadedScenes = new List<string>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.name) || string.IsNullOrWhiteSpace(scene.path))
                continue;

            loadedScenes.Add(scene.name);
        }

        return loadedScenes;
    }

    static string BuildDisplayName(IReadOnlyList<string> sceneNames)
    {
        if (sceneNames == null || sceneNames.Count == 0)
            return "-";

        return string.Join(" + ", sceneNames);
    }

    static bool IsSceneLoaded(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    static string ResolveActiveSceneName(string activeSceneName, IReadOnlyList<string> sceneNames)
    {
        if (!string.IsNullOrWhiteSpace(activeSceneName))
        {
            for (int i = 0; i < sceneNames.Count; i++)
            {
                if (string.Equals(sceneNames[i], activeSceneName, StringComparison.Ordinal))
                    return activeSceneName;
            }
        }

        return sceneNames.Count > 0 ? sceneNames[0] : "Main";
    }

    static SceneSetDefinition FindById(string sceneSetId)
    {
        for (int i = 0; i < Definitions.Length; i++)
        {
            if (string.Equals(Definitions[i].id, sceneSetId, StringComparison.OrdinalIgnoreCase))
                return Definitions[i];
        }

        return null;
    }

    static SceneSetDefinition FindByContainedScene(string sceneName)
    {
        for (int i = 0; i < Definitions.Length; i++)
        {
            string[] scenes = Definitions[i].sceneNames;
            for (int j = 0; j < scenes.Length; j++)
            {
                if (string.Equals(scenes[j], sceneName, StringComparison.Ordinal))
                    return Definitions[i];
            }
        }

        return null;
    }

    static SceneSetDefinition FindExactDefinition(IReadOnlyList<string> sceneNames)
    {
        HashSet<string> sceneSet = new HashSet<string>(sceneNames, StringComparer.Ordinal);

        for (int i = 0; i < Definitions.Length; i++)
        {
            string[] definitionScenes = Definitions[i].sceneNames;
            if (definitionScenes.Length != sceneSet.Count)
                continue;

            bool matches = true;
            for (int j = 0; j < definitionScenes.Length; j++)
            {
                if (!sceneSet.Contains(definitionScenes[j]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return Definitions[i];
        }

        return null;
    }

    static MultiplayerSceneSetState CreateFromDefinition(SceneSetDefinition definition)
    {
        if (definition == null)
            return null;

        return new MultiplayerSceneSetState
        {
            sceneSetId = definition.id,
            displayName = definition.displayName,
            activeSceneName = definition.activeSceneName,
            sceneNames = (string[])definition.sceneNames.Clone()
        };
    }
}
