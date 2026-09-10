using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MeshyHeroSetup
{
    const string RootFolder = "Assets/Resources/Characters/MeshyHero";
    const string PackageFolder = "Meshy_AI_Stylized_fantasy_male_biped";
    const string TitanControllerPath = "Assets/Resources/Characters/MeshyHero/MeshyTitan.controller";
    const string MaterialPath = "Assets/Resources/Characters/MeshyHero/MeshyHero_Material.mat";
    const int HeroTextureMaxSize = 1024;
    const int MaskTextureMaxSize = 512;

    static bool setupScheduled;

    struct ActionAnimation
    {
        public readonly string stateName;
        public readonly string triggerName;
        public readonly string fbxToken;
        public readonly float exitTime;
        public readonly Vector3 position;

        public ActionAnimation(string stateName, string triggerName, string fbxToken, float exitTime, Vector3 position)
        {
            this.stateName = stateName;
            this.triggerName = triggerName;
            this.fbxToken = fbxToken;
            this.exitTime = exitTime;
            this.position = position;
        }
    }

    [MenuItem("Elarion/Meshy/Configurar Heroi Meshy")]
    public static void ConfigureFromMenu()
    {
        Configure(true);
    }

    public static void ScheduleAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (setupScheduled)
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
        if (!verbose && EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!Directory.Exists(RootFolder))
            return;

        bool importersChanged = ConfigureImporters();
        EnsureHeroMaterial();
        ConfigureAnimatorController(verbose);

        AssetDatabase.SaveAssets();
        if (importersChanged)
            AssetDatabase.Refresh();

        if (verbose)
            Debug.Log("Meshy Titan configurado: modelo, material e MeshyTitan.controller atualizados.");
    }

    static bool ConfigureImporters()
    {
        bool changed = false;
        foreach (string fbxPath in FindFiles("*.fbx"))
            changed |= ConfigureFbxImporter(fbxPath);

        foreach (string texturePath in FindFiles("*.png"))
            changed |= ConfigureTextureImporter(texturePath);

        return changed;
    }

    static bool ConfigureFbxImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return false;

        bool isCharacterModel = Path.GetFileNameWithoutExtension(assetPath)
            .EndsWith("_Character_output", StringComparison.OrdinalIgnoreCase);
        bool dirty = false;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
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

        if (importer.skinWeights != ModelImporterSkinWeights.Custom)
        {
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            dirty = true;
        }

        if (importer.maxBonesPerVertex != 2)
        {
            importer.maxBonesPerVertex = 2;
            dirty = true;
        }

        if (importer.animationCompression != ModelImporterAnimationCompression.Optimal)
        {
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.animationRotationError, 1f))
        {
            importer.animationRotationError = 1f;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.animationPositionError, 1f))
        {
            importer.animationPositionError = 1f;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.animationScaleError, 1f))
        {
            importer.animationScaleError = 1f;
            dirty = true;
        }

        if (!importer.removeConstantScaleCurves)
        {
            importer.removeConstantScaleCurves = true;
            dirty = true;
        }

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            dirty = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            dirty = true;
        }

        bool shouldImportAnimation = !isCharacterModel;
        if (importer.importAnimation != shouldImportAnimation)
        {
            importer.importAnimation = shouldImportAnimation;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static bool ConfigureTextureImporter(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool dirty = false;
        bool isNormal = assetPath.EndsWith("_normal.png", StringComparison.OrdinalIgnoreCase);
        bool isMask = assetPath.EndsWith("_metallic.png", StringComparison.OrdinalIgnoreCase) ||
                      assetPath.EndsWith("_roughness.png", StringComparison.OrdinalIgnoreCase);
        TextureImporterType desiredType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        int desiredMaxSize = isMask ? MaskTextureMaxSize : HeroTextureMaxSize;

        if (importer.textureType != desiredType)
        {
            importer.textureType = desiredType;
            dirty = true;
        }

        if (importer.maxTextureSize != desiredMaxSize)
        {
            importer.maxTextureSize = desiredMaxSize;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            dirty = true;
        }

        if (!importer.crunchedCompression)
        {
            importer.crunchedCompression = true;
            dirty = true;
        }

        if (importer.compressionQuality != 60)
        {
            importer.compressionQuality = 60;
            dirty = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static void EnsureHeroMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexturePath("_texture_0.png"));
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexturePath("_normal.png"));
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexturePath("_metallic.png"));

        SetTexture(material, "_BaseMap", albedo);
        SetTexture(material, "_MainTex", albedo);
        SetTexture(material, "_BumpMap", normal);
        SetTexture(material, "_MetallicGlossMap", metallic);

        if (normal != null)
            material.EnableKeyword("_NORMALMAP");
        if (metallic != null)
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

        EditorUtility.SetDirty(material);
    }

    static void SetTexture(Material material, string property, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(property))
            material.SetTexture(property, texture);
    }

    static void ConfigureAnimatorController(bool verbose)
    {
        AnimatorController controller = LoadOrCreateTitanController();
        if (controller == null)
        {
            if (verbose)
                Debug.LogWarning($"Nao foi possivel criar o AnimatorController do Tita em {TitanControllerPath}.");
            return;
        }

        if (controller.layers == null || controller.layers.Length == 0)
            return;

        EnsureParameter(controller, PlayerAnimationBridge.SpeedParameter, AnimatorControllerParameterType.Float);
        EnsureTriggerParameters(controller);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = EnsureLocomotionState(controller, stateMachine);
        if (locomotion == null)
            return;

        stateMachine.defaultState = locomotion;
        ConfigureLocomotion(controller, locomotion);
        ConfigureActionStates(controller, stateMachine, locomotion);

        EditorUtility.SetDirty(controller);
    }

    static AnimatorController LoadOrCreateTitanController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(TitanControllerPath);
        if (controller != null)
            return controller;

        return AnimatorController.CreateAnimatorControllerAtPath(TitanControllerPath);
    }

    static void EnsureTriggerParameters(AnimatorController controller)
    {
        EnsureParameter(controller, PlayerAnimationBridge.LegacyChopTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.AttackTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.CutWoodTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.MineTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.PickupTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.PowerStoneWallTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.PowerDiveTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.Power1Trigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.Power2Trigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.Power3Trigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.VictoryTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.DieTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.JumpTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.RunJumpTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.FallTrigger, AnimatorControllerParameterType.Trigger);
    }

    static AnimatorState EnsureLocomotionState(AnimatorController controller, AnimatorStateMachine stateMachine)
    {
        AnimatorState locomotion = FindState(stateMachine, "Locomotion");
        if (locomotion == null)
            locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 0f, 0f));

        if (locomotion.motion is BlendTree)
            return locomotion;

        BlendTree tree = new BlendTree
        {
            name = "MeshyTitanLocomotion",
            hideFlags = HideFlags.HideInHierarchy
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        locomotion.motion = tree;
        EditorUtility.SetDirty(locomotion);
        return locomotion;
    }

    static void ConfigureLocomotion(AnimatorController controller, AnimatorState locomotion)
    {
        AnimationClip idle = LoadClip("Animation_Idle_03", true);
        AnimationClip walk = LoadClip("Animation_Walking", true);
        AnimationClip run = LoadClip("Animation_Running", true);
        if (idle == null || walk == null || run == null)
            return;

        BlendTree tree = locomotion.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree
            {
                name = "MeshyTitanLocomotion",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            locomotion.motion = tree;
        }

        tree.name = "MeshyTitanLocomotion";
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = PlayerAnimationBridge.SpeedParameter;
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
            new ChildMotion { motion = walk, threshold = 0.5f, timeScale = 1f },
            new ChildMotion { motion = run, threshold = 1f, timeScale = 1f }
        };

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(locomotion);
    }

    static void ConfigureActionStates(AnimatorController controller, AnimatorStateMachine stateMachine, AnimatorState locomotion)
    {
        ActionAnimation[] actions =
        {
            new ActionAnimation("axe_hit", PlayerAnimationBridge.LegacyChopTrigger, "Animation_Charged_Upward_Slash", 0.86f, new Vector3(520f, -180f, 0f)),
            new ActionAnimation("MeshyAttack", PlayerAnimationBridge.AttackTrigger, "Animation_Charged_Upward_Slash", 0.86f, new Vector3(520f, -240f, 0f)),
            new ActionAnimation("MeshyCutWood", PlayerAnimationBridge.CutWoodTrigger, "Animation_Right_Hand_Sword_Slash", 0.88f, new Vector3(520f, -300f, 0f)),
            new ActionAnimation("MeshyMine", PlayerAnimationBridge.MineTrigger, "Animation_Charged_Ground_Slam", 0.88f, new Vector3(520f, -360f, 0f)),
            new ActionAnimation("MeshyPickup", PlayerAnimationBridge.PickupTrigger, "Animation_Collect_Object", 0.82f, new Vector3(520f, -420f, 0f)),
            new ActionAnimation("MeshyPowerStoneWall", PlayerAnimationBridge.PowerStoneWallTrigger, "Animation_Angry_Ground_Stomp_2", 0.88f, new Vector3(820f, -180f, 0f)),
            new ActionAnimation("MeshyPowerDive", PlayerAnimationBridge.PowerDiveTrigger, "Animation_Dive_Down_and_Land_2", 0.94f, new Vector3(820f, -240f, 0f)),
            new ActionAnimation("MeshyPower1", PlayerAnimationBridge.Power1Trigger, "Animation_Charged_Ground_Slam", 0.88f, new Vector3(820f, -300f, 0f)),
            new ActionAnimation("MeshyPower2", PlayerAnimationBridge.Power2Trigger, "Animation_Angry_Ground_Stomp_2", 0.88f, new Vector3(820f, -360f, 0f)),
            new ActionAnimation("MeshyPower3", PlayerAnimationBridge.Power3Trigger, "Animation_Dive_Down_and_Land_2", 0.94f, new Vector3(820f, -420f, 0f)),
            new ActionAnimation("MeshyVictory", PlayerAnimationBridge.VictoryTrigger, "Animation_Idle_11", 0.88f, new Vector3(1120f, -180f, 0f)),
            new ActionAnimation("MeshyDie", PlayerAnimationBridge.DieTrigger, "Animation_Dead", 0.96f, new Vector3(1120f, -240f, 0f)),
            new ActionAnimation("MeshyJump", PlayerAnimationBridge.JumpTrigger, "Animation_Regular_Jump", 0.9f, new Vector3(1120f, -300f, 0f)),
            new ActionAnimation("MeshyRunJump", PlayerAnimationBridge.RunJumpTrigger, "Animation_Run_and_Leap", 0.92f, new Vector3(1120f, -360f, 0f)),
            new ActionAnimation("MeshyFall", PlayerAnimationBridge.FallTrigger, "Animation_falling_down", 0.9f, new Vector3(1120f, -420f, 0f))
        };

        for (int i = 0; i < actions.Length; i++)
            ConfigureActionState(controller, stateMachine, locomotion, actions[i]);
    }

    static void ConfigureActionState(AnimatorController controller, AnimatorStateMachine stateMachine, AnimatorState locomotion, ActionAnimation action)
    {
        AnimationClip clip = LoadClip(action.fbxToken, false);
        if (clip == null)
            return;

        AnimatorState state = FindState(stateMachine, action.stateName);
        if (state == null)
            state = stateMachine.AddState(action.stateName, action.position);

        state.motion = clip;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(state);

        RemoveAnyStateTransitionsForTrigger(stateMachine, action.triggerName);
        AnimatorStateTransition anyTransition = stateMachine.AddAnyStateTransition(state);
        anyTransition.hasExitTime = false;
        anyTransition.hasFixedDuration = true;
        anyTransition.duration = 0.08f;
        anyTransition.canTransitionToSelf = false;
        anyTransition.AddCondition(AnimatorConditionMode.If, 0f, action.triggerName);

        RemoveStateTransitions(state);
        AnimatorStateTransition returnTransition = state.AddTransition(locomotion);
        returnTransition.hasExitTime = true;
        returnTransition.exitTime = Mathf.Clamp01(action.exitTime);
        returnTransition.hasFixedDuration = true;
        returnTransition.duration = 0.12f;

        EditorUtility.SetDirty(anyTransition);
        EditorUtility.SetDirty(returnTransition);
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter != null && parameter.name == name)
                return;
        }

        controller.AddParameter(name, type);
    }

    static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        if (stateMachine == null || string.IsNullOrEmpty(stateName))
            return null;

        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state != null && state.name == stateName)
                return state;
        }

        return null;
    }

    static void RemoveAnyStateTransitionsForTrigger(AnimatorStateMachine stateMachine, string triggerName)
    {
        List<AnimatorStateTransition> transitionsToRemove = new List<AnimatorStateTransition>();
        AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition != null && HasCondition(transition, triggerName))
                transitionsToRemove.Add(transition);
        }

        for (int i = 0; i < transitionsToRemove.Count; i++)
            stateMachine.RemoveAnyStateTransition(transitionsToRemove[i]);
    }

    static void RemoveStateTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        for (int i = transitions.Length - 1; i >= 0; i--)
            state.RemoveTransition(transitions[i]);
    }

    static bool HasCondition(AnimatorStateTransition transition, string parameterName)
    {
        AnimatorCondition[] conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].parameter == parameterName)
                return true;
        }

        return false;
    }

    static AnimationClip LoadClip(string fbxToken, bool loop)
    {
        string path = FindFbxPath(fbxToken);
        if (string.IsNullOrEmpty(path))
            return null;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            SetClipLooping(clip, loop);
            return clip;
        }

        return null;
    }

    static void SetClipLooping(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loop)
            return;

        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    static string FindFbxPath(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        foreach (string path in FindFiles("*.fbx"))
        {
            if (Path.GetFileNameWithoutExtension(path).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return path;
        }

        return null;
    }

    static string FindTexturePath(string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
            return null;

        foreach (string path in FindFiles("*.png"))
        {
            string fileName = Path.GetFileName(path);
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    static IEnumerable<string> FindFiles(string searchPattern)
    {
        if (!Directory.Exists(RootFolder))
            yield break;

        string packagePath = Path.Combine(RootFolder, PackageFolder);
        string searchRoot = Directory.Exists(packagePath) ? packagePath : RootFolder;
        foreach (string path in Directory.GetFiles(searchRoot, searchPattern, SearchOption.AllDirectories))
            yield return path.Replace('\\', '/');
    }
}

public class MeshyHeroAssetPostprocessor : AssetPostprocessor
{
    const string GeneratedControllerPath = "Assets/Resources/Characters/MeshyHero/MeshyTitan.controller";
    const string GeneratedMaterialPath = "Assets/Resources/Characters/MeshyHero/MeshyHero_Material.mat";

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (ContainsMeshyHeroAsset(importedAssets) || ContainsMeshyHeroAsset(movedAssets))
            MeshyHeroSetup.ScheduleAutoSetup();
    }

    static bool ContainsMeshyHeroAsset(string[] paths)
    {
        if (paths == null)
            return false;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrEmpty(path) ||
                string.Equals(path, GeneratedControllerPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, GeneratedMaterialPath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (path.StartsWith("Assets/Resources/Characters/MeshyHero", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public static class MeshyInvokerSetup
{
    const string RootFolder = "Assets/Resources/Characters/SavageElfInvoker";
    const string PackageFolder = "Meshy_AI_Savage_Elf_Invoker_biped";
    const string ControllerPath = "Assets/Resources/Characters/SavageElfInvoker/SavageElfInvoker.controller";
    const string MaterialPath = "Assets/Resources/Characters/SavageElfInvoker/SavageElfInvoker_Material.mat";
    const int TextureMaxSize = 1024;
    const int MaskTextureMaxSize = 512;

    static bool setupScheduled;

    struct ActionAnimation
    {
        public readonly string stateName;
        public readonly string triggerName;
        public readonly string fbxToken;
        public readonly float exitTime;
        public readonly Vector3 position;

        public ActionAnimation(string stateName, string triggerName, string fbxToken, float exitTime, Vector3 position)
        {
            this.stateName = stateName;
            this.triggerName = triggerName;
            this.fbxToken = fbxToken;
            this.exitTime = exitTime;
            this.position = position;
        }
    }

    [MenuItem("Elarion/Meshy/Configurar Invocador Selvagem")]
    public static void ConfigureFromMenu()
    {
        Configure(true);
    }

    public static void ScheduleAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (setupScheduled)
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
        if (!verbose && EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!Directory.Exists(RootFolder))
            return;

        bool importersChanged = ConfigureImporters();
        EnsureMaterial();
        ConfigureAnimatorController(verbose);

        AssetDatabase.SaveAssets();
        if (importersChanged)
            AssetDatabase.Refresh();

        if (verbose)
            Debug.Log("Invocador Selvagem configurado: modelo, material e SavageElfInvoker.controller atualizados.");
    }

    static bool ConfigureImporters()
    {
        bool changed = false;
        foreach (string fbxPath in FindFiles("*.fbx"))
            changed |= ConfigureFbxImporter(fbxPath);

        foreach (string texturePath in FindFiles("*.png"))
            changed |= ConfigureTextureImporter(texturePath);

        return changed;
    }

    static bool ConfigureFbxImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return false;

        bool isCharacterModel = Path.GetFileNameWithoutExtension(assetPath)
            .EndsWith("_Character_output", StringComparison.OrdinalIgnoreCase);
        bool dirty = false;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
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

        if (importer.skinWeights != ModelImporterSkinWeights.Custom)
        {
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            dirty = true;
        }

        if (importer.maxBonesPerVertex != 2)
        {
            importer.maxBonesPerVertex = 2;
            dirty = true;
        }

        if (importer.animationCompression != ModelImporterAnimationCompression.Optimal)
        {
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.animationRotationError, 1f))
        {
            importer.animationRotationError = 1f;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.animationPositionError, 1f))
        {
            importer.animationPositionError = 1f;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.animationScaleError, 1f))
        {
            importer.animationScaleError = 1f;
            dirty = true;
        }

        if (!importer.removeConstantScaleCurves)
        {
            importer.removeConstantScaleCurves = true;
            dirty = true;
        }

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            dirty = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            dirty = true;
        }

        bool shouldImportAnimation = !isCharacterModel;
        if (importer.importAnimation != shouldImportAnimation)
        {
            importer.importAnimation = shouldImportAnimation;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static bool ConfigureTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool dirty = false;
        bool isNormal = assetPath.EndsWith("_normal.png", StringComparison.OrdinalIgnoreCase);
        bool isMask = assetPath.EndsWith("_metallic.png", StringComparison.OrdinalIgnoreCase) ||
                      assetPath.EndsWith("_roughness.png", StringComparison.OrdinalIgnoreCase);
        TextureImporterType desiredType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        int desiredMaxSize = isMask ? MaskTextureMaxSize : TextureMaxSize;

        if (importer.textureType != desiredType)
        {
            importer.textureType = desiredType;
            dirty = true;
        }

        if (importer.maxTextureSize != desiredMaxSize)
        {
            importer.maxTextureSize = desiredMaxSize;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            dirty = true;
        }

        if (!importer.crunchedCompression)
        {
            importer.crunchedCompression = true;
            dirty = true;
        }

        if (importer.compressionQuality != 60)
        {
            importer.compressionQuality = 60;
            dirty = true;
        }

        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();

        return dirty;
    }

    static void EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexturePath("_texture_0.png"));
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexturePath("_normal.png"));
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexturePath("_metallic.png"));

        SetTexture(material, "_BaseMap", albedo);
        SetTexture(material, "_MainTex", albedo);
        SetTexture(material, "_BumpMap", normal);
        SetTexture(material, "_MetallicGlossMap", metallic);

        if (normal != null)
            material.EnableKeyword("_NORMALMAP");
        if (metallic != null)
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

        EditorUtility.SetDirty(material);
    }

    static void ConfigureAnimatorController(bool verbose)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        if (controller == null)
        {
            if (verbose)
                Debug.LogWarning($"Nao foi possivel criar o AnimatorController do Invocador em {ControllerPath}.");
            return;
        }

        EnsureParameter(controller, PlayerAnimationBridge.SpeedParameter, AnimatorControllerParameterType.Float);
        EnsureParameter(controller, PlayerAnimationBridge.DirectionXParameter, AnimatorControllerParameterType.Float);
        EnsureTriggerParameters(controller);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = EnsureLocomotionState(controller, stateMachine);
        if (locomotion == null)
            return;

        stateMachine.defaultState = locomotion;
        ConfigureLocomotion(controller, locomotion);
        ConfigureActionStates(controller, stateMachine, locomotion);
        EditorUtility.SetDirty(controller);
    }

    static void EnsureTriggerParameters(AnimatorController controller)
    {
        EnsureParameter(controller, PlayerAnimationBridge.LegacyChopTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.AttackTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.CutWoodTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.MineTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.PickupTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.PowerStoneWallTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.PowerDiveTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.Power1Trigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.Power2Trigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.Power3Trigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.VictoryTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.DieTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.JumpTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.RunJumpTrigger, AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, PlayerAnimationBridge.FallTrigger, AnimatorControllerParameterType.Trigger);
    }

    static AnimatorState EnsureLocomotionState(AnimatorController controller, AnimatorStateMachine stateMachine)
    {
        AnimatorState locomotion = FindState(stateMachine, "Locomotion");
        if (locomotion == null)
            locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 0f, 0f));

        if (locomotion.motion is BlendTree)
            return locomotion;

        BlendTree tree = new BlendTree
        {
            name = "SavageElfInvokerLocomotion",
            hideFlags = HideFlags.HideInHierarchy
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        locomotion.motion = tree;
        EditorUtility.SetDirty(locomotion);
        return locomotion;
    }

    static void ConfigureLocomotion(AnimatorController controller, AnimatorState locomotion)
    {
        AnimationClip idle = LoadClip("Animation_Idle_11", true);
        AnimationClip walk = LoadClip("Animation_Walking", true);
        AnimationClip run = LoadClip("Animation_Running", true);
        AnimationClip walkRight = LoadClip("Animation_Walk_Turn_Right", true) ?? walk;
        AnimationClip walkLeft = LoadClip("Animation_Walk_Turn_Left", true) ?? walk;
        AnimationClip runRight = LoadClip("Animation_Run_Turn_Right", true) ?? run;
        AnimationClip runLeft = LoadClip("Animation_Run_Turn_Left", true) ?? run;
        if (idle == null || walk == null || run == null)
            return;

        BlendTree tree = locomotion.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree
            {
                name = "SavageElfInvokerLocomotion",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            locomotion.motion = tree;
        }

        tree.name = "SavageElfInvokerLocomotion";
        tree.blendType = BlendTreeType.FreeformCartesian2D;
        tree.blendParameter = PlayerAnimationBridge.DirectionXParameter;
        tree.blendParameterY = PlayerAnimationBridge.SpeedParameter;
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            new ChildMotion { motion = idle, position = new Vector2(0f, 0f), timeScale = 1f },
            new ChildMotion { motion = walkLeft, position = new Vector2(-1f, 0.5f), timeScale = 1f },
            new ChildMotion { motion = walk, position = new Vector2(0f, 0.5f), timeScale = 1f },
            new ChildMotion { motion = walkRight, position = new Vector2(1f, 0.5f), timeScale = 1f },
            new ChildMotion { motion = runLeft, position = new Vector2(-1f, 1f), timeScale = 1f },
            new ChildMotion { motion = run, position = new Vector2(0f, 1f), timeScale = 1f },
            new ChildMotion { motion = runRight, position = new Vector2(1f, 1f), timeScale = 1f }
        };

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(locomotion);
    }

    static void ConfigureActionStates(AnimatorController controller, AnimatorStateMachine stateMachine, AnimatorState locomotion)
    {
        ActionAnimation[] actions =
        {
            new ActionAnimation("InvokerHit", PlayerAnimationBridge.LegacyChopTrigger, "Animation_mage_soell_cast_7", 0.84f, new Vector3(520f, -180f, 0f)),
            new ActionAnimation("InvokerAttack", PlayerAnimationBridge.AttackTrigger, "Animation_mage_soell_cast_7", 0.84f, new Vector3(520f, -240f, 0f)),
            new ActionAnimation("InvokerCut", PlayerAnimationBridge.CutWoodTrigger, "Animation_Right_Hand_Sword_Slash", 0.86f, new Vector3(520f, -300f, 0f)),
            new ActionAnimation("InvokerMine", PlayerAnimationBridge.MineTrigger, "Animation_Right_Hand_Sword_Slash", 0.86f, new Vector3(520f, -360f, 0f)),
            new ActionAnimation("InvokerPickup", PlayerAnimationBridge.PickupTrigger, "Animation_Female_Bend_Over_Pick_Up_Inspect", 0.82f, new Vector3(520f, -420f, 0f)),
            new ActionAnimation("InvokerPowerStoneWall", PlayerAnimationBridge.PowerStoneWallTrigger, "Animation_mage_soell_cast_4", 0.86f, new Vector3(820f, -180f, 0f)),
            new ActionAnimation("InvokerPowerDive", PlayerAnimationBridge.PowerDiveTrigger, "Animation_Jump_Over_Obstacle_2", 0.9f, new Vector3(820f, -240f, 0f)),
            new ActionAnimation("InvokerPowerQ", PlayerAnimationBridge.Power1Trigger, "Animation_Charged_Ground_Slam", 0.86f, new Vector3(820f, -300f, 0f)),
            new ActionAnimation("InvokerPowerR", PlayerAnimationBridge.Power2Trigger, "Animation_mage_soell_cast_4", 0.86f, new Vector3(820f, -360f, 0f)),
            new ActionAnimation("InvokerPowerF", PlayerAnimationBridge.Power3Trigger, "Animation_mage_soell_cast_frame", 0.86f, new Vector3(820f, -420f, 0f)),
            new ActionAnimation("InvokerVictory", PlayerAnimationBridge.VictoryTrigger, "Animation_Indoor_Swing", 0.88f, new Vector3(1120f, -180f, 0f)),
            new ActionAnimation("InvokerDie", PlayerAnimationBridge.DieTrigger, "Animation_Strangled_and_Fall_Forward", 0.96f, new Vector3(1120f, -240f, 0f)),
            new ActionAnimation("InvokerJump", PlayerAnimationBridge.JumpTrigger, "Animation_Regular_Jump", 0.9f, new Vector3(1120f, -300f, 0f)),
            new ActionAnimation("InvokerRunJump", PlayerAnimationBridge.RunJumpTrigger, "Animation_Jump_Over_Obstacle_2", 0.9f, new Vector3(1120f, -360f, 0f)),
            new ActionAnimation("InvokerFall", PlayerAnimationBridge.FallTrigger, "Animation_Regular_Jump", 0.82f, new Vector3(1120f, -420f, 0f))
        };

        for (int i = 0; i < actions.Length; i++)
            ConfigureActionState(stateMachine, locomotion, actions[i]);
    }

    static void ConfigureActionState(AnimatorStateMachine stateMachine, AnimatorState locomotion, ActionAnimation action)
    {
        AnimationClip clip = LoadClip(action.fbxToken, false);
        if (clip == null)
            return;

        AnimatorState state = FindState(stateMachine, action.stateName);
        if (state == null)
            state = stateMachine.AddState(action.stateName, action.position);

        state.motion = clip;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(state);

        RemoveAnyStateTransitionsForTrigger(stateMachine, action.triggerName);
        AnimatorStateTransition anyTransition = stateMachine.AddAnyStateTransition(state);
        anyTransition.hasExitTime = false;
        anyTransition.hasFixedDuration = true;
        anyTransition.duration = 0.08f;
        anyTransition.canTransitionToSelf = false;
        anyTransition.AddCondition(AnimatorConditionMode.If, 0f, action.triggerName);

        RemoveStateTransitions(state);
        AnimatorStateTransition returnTransition = state.AddTransition(locomotion);
        returnTransition.hasExitTime = true;
        returnTransition.exitTime = Mathf.Clamp01(action.exitTime);
        returnTransition.hasFixedDuration = true;
        returnTransition.duration = 0.12f;

        EditorUtility.SetDirty(anyTransition);
        EditorUtility.SetDirty(returnTransition);
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter != null && parameter.name == name)
                return;
        }

        controller.AddParameter(name, type);
    }

    static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state != null && state.name == stateName)
                return state;
        }

        return null;
    }

    static void RemoveAnyStateTransitionsForTrigger(AnimatorStateMachine stateMachine, string triggerName)
    {
        List<AnimatorStateTransition> transitionsToRemove = new List<AnimatorStateTransition>();
        AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition != null && HasCondition(transition, triggerName))
                transitionsToRemove.Add(transition);
        }

        for (int i = 0; i < transitionsToRemove.Count; i++)
            stateMachine.RemoveAnyStateTransition(transitionsToRemove[i]);
    }

    static void RemoveStateTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        for (int i = transitions.Length - 1; i >= 0; i--)
            state.RemoveTransition(transitions[i]);
    }

    static bool HasCondition(AnimatorStateTransition transition, string parameterName)
    {
        AnimatorCondition[] conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].parameter == parameterName)
                return true;
        }

        return false;
    }

    static AnimationClip LoadClip(string fbxToken, bool loop)
    {
        string path = FindFbxPath(fbxToken);
        if (string.IsNullOrEmpty(path))
            return null;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            SetClipLooping(clip, loop);
            return clip;
        }

        return null;
    }

    static void SetClipLooping(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loop)
            return;

        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    static string FindFbxPath(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        foreach (string path in FindFiles("*.fbx"))
        {
            if (Path.GetFileNameWithoutExtension(path).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return path;
        }

        return null;
    }

    static string FindTexturePath(string suffix)
    {
        foreach (string path in FindFiles("*.png"))
        {
            string fileName = Path.GetFileName(path);
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    static void SetTexture(Material material, string property, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(property))
            material.SetTexture(property, texture);
    }

    static IEnumerable<string> FindFiles(string searchPattern)
    {
        if (!Directory.Exists(RootFolder))
            yield break;

        string packagePath = Path.Combine(RootFolder, PackageFolder);
        string searchRoot = Directory.Exists(packagePath) ? packagePath : RootFolder;
        foreach (string path in Directory.GetFiles(searchRoot, searchPattern, SearchOption.AllDirectories))
            yield return path.Replace('\\', '/');
    }
}

public class MeshyInvokerAssetPostprocessor : AssetPostprocessor
{
    const string GeneratedControllerPath = "Assets/Resources/Characters/SavageElfInvoker/SavageElfInvoker.controller";
    const string GeneratedMaterialPath = "Assets/Resources/Characters/SavageElfInvoker/SavageElfInvoker_Material.mat";

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (ContainsInvokerAsset(importedAssets) || ContainsInvokerAsset(movedAssets))
            MeshyInvokerSetup.ScheduleAutoSetup();
    }

    static bool ContainsInvokerAsset(string[] paths)
    {
        if (paths == null)
            return false;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrEmpty(path) ||
                string.Equals(path, GeneratedControllerPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, GeneratedMaterialPath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (path.StartsWith("Assets/Resources/Characters/SavageElfInvoker", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
