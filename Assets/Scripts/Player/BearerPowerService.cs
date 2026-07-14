using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BearerPowerService : MonoBehaviour
{
    public static event Action<BearerPowerService> PowerChanged;

    [SerializeField] string currentPowerId;
    [SerializeField] bool selectionCompleted;

    GameObject visualRoot;

    public string CurrentPowerId => currentPowerId;
    public bool HasPower => BearerPowerCatalog.Find(currentPowerId) != null;
    public bool HasCompletedSelection => selectionCompleted;
    public BearerPowerDefinition CurrentDefinition => BearerPowerCatalog.Find(currentPowerId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player != null && player.GetComponent<BearerPowerService>() == null)
            player.gameObject.AddComponent<BearerPowerService>();
    }

    void Start()
    {
        if (!LanMultiplayerManager.IsDedicatedRuntime &&
            GetComponent<PlayerMovement>() != null &&
            GetComponent<BearerPowerController>() == null)
        {
            gameObject.AddComponent<BearerPowerController>();
        }

        ApplyVisuals();
    }

    public void RequestPower(string stableId)
    {
        BearerPowerDefinition definition = BearerPowerCatalog.Find(stableId);
        if (definition == null)
        {
            MessageSystem.Instance?.ShowMessage("Este legado ainda nao esta disponivel.");
            return;
        }

        if (selectionCompleted)
        {
            MessageSystem.Instance?.ShowMessage("Voce ja carrega um legado.");
            return;
        }

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.Mode != LanMultiplayerManager.SessionMode.None)
        {
            manager.RequestBearerPowerClaim(definition.stableId);
            return;
        }

        AcceptPower(definition.stableId);
    }

    public void AcceptPower(string stableId)
    {
        BearerPowerDefinition definition = BearerPowerCatalog.Find(stableId);
        if (definition == null)
            return;

        currentPowerId = definition.stableId;
        selectionCompleted = true;
        ApplyVisuals();
        PowerChanged?.Invoke(this);
    }

    public void LoadState(string stableId, bool completed)
    {
        BearerPowerDefinition definition = BearerPowerCatalog.Find(stableId);
        currentPowerId = definition != null ? definition.stableId : string.Empty;
        selectionCompleted = completed || definition != null;
        ApplyVisuals();
        PowerChanged?.Invoke(this);
    }

    public void ResetForNewAdventure()
    {
        currentPowerId = string.Empty;
        selectionCompleted = false;
        ApplyVisuals();
        PowerChanged?.Invoke(this);
    }

    public void LosePower()
    {
        currentPowerId = string.Empty;
        ApplyVisuals();
        PowerChanged?.Invoke(this);
    }

    public List<BearerPowerAbilityDefinition> GetUnlockedAbilities(int playerLevel)
    {
        List<BearerPowerAbilityDefinition> unlocked = new List<BearerPowerAbilityDefinition>();
        BearerPowerDefinition definition = CurrentDefinition;
        if (definition?.abilities == null)
            return unlocked;

        for (int i = 0; i < definition.abilities.Count; i++)
        {
            BearerPowerAbilityDefinition ability = definition.abilities[i];
            if (ability != null && playerLevel >= ability.unlockLevel)
                unlocked.Add(ability);
        }

        return unlocked;
    }

    void ApplyVisuals()
    {
        if (visualRoot != null)
            Destroy(visualRoot);

        BearerPowerDefinition definition = CurrentDefinition;
        if (definition == null)
            return;

        visualRoot = BearerPowerVisualFactory.Create(transform, definition);
    }
}

public static class BearerPowerVisualFactory
{
    public static GameObject Create(Transform owner, BearerPowerDefinition definition)
    {
        if (owner == null || definition == null)
            return null;

        GameObject root = new GameObject("BearerPowerVisual");
        root.transform.SetParent(owner, false);

        CreateAura(root.transform, definition);

        if (TryCreateMeshyPowerVisual(root.transform, owner, definition))
            return root;

        switch (definition.id)
        {
            case BearerPowerId.FlameHeir:
                CreateAdornment("FlameEyeL", root.transform, PrimitiveType.Sphere, new Vector3(-0.15f, 1.88f, 0.34f), Vector3.one * 0.085f, Color.red);
                CreateAdornment("FlameEyeR", root.transform, PrimitiveType.Sphere, new Vector3(0.15f, 1.88f, 0.34f), Vector3.one * 0.085f, Color.red);
                CreateAdornment("FlameCore", root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.18f, 0.27f), new Vector3(0.2f, 0.2f, 0.08f), definition.secondaryColor);
                CreateAdornment("FlameVeinL", root.transform, PrimitiveType.Cube, new Vector3(-0.22f, 1.2f, 0.29f), new Vector3(0.035f, 0.5f, 0.035f), definition.secondaryColor, Quaternion.Euler(0f, 0f, -24f));
                CreateAdornment("FlameVeinR", root.transform, PrimitiveType.Cube, new Vector3(0.22f, 1.2f, 0.29f), new Vector3(0.035f, 0.5f, 0.035f), definition.secondaryColor, Quaternion.Euler(0f, 0f, 24f));
                CreateAdornment("FlameVeinArmL", root.transform, PrimitiveType.Cube, new Vector3(-0.43f, 1.12f, 0.08f), new Vector3(0.03f, 0.42f, 0.03f), definition.primaryColor, Quaternion.Euler(0f, 0f, -8f));
                CreateAdornment("FlameVeinArmR", root.transform, PrimitiveType.Cube, new Vector3(0.43f, 1.12f, 0.08f), new Vector3(0.03f, 0.42f, 0.03f), definition.primaryColor, Quaternion.Euler(0f, 0f, 8f));
                break;

            case BearerPowerId.VoidWalker:
                CreateAdornment("VoidEyeL", root.transform, PrimitiveType.Sphere, new Vector3(-0.15f, 1.88f, 0.34f), Vector3.one * 0.085f, definition.secondaryColor);
                CreateAdornment("VoidEyeR", root.transform, PrimitiveType.Sphere, new Vector3(0.15f, 1.88f, 0.34f), Vector3.one * 0.085f, definition.secondaryColor);
                CreateAdornment("VoidShardL", root.transform, PrimitiveType.Cube, new Vector3(-0.35f, 1.25f, -0.12f), new Vector3(0.12f, 0.42f, 0.12f), definition.secondaryColor, Quaternion.Euler(20f, 0f, -28f));
                CreateAdornment("VoidShardR", root.transform, PrimitiveType.Cube, new Vector3(0.35f, 1.25f, -0.12f), new Vector3(0.12f, 0.42f, 0.12f), definition.secondaryColor, Quaternion.Euler(-20f, 0f, 28f));
                CreateAdornment("VoidCore", root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.2f, 0.25f), new Vector3(0.22f, 0.3f, 0.08f), definition.primaryColor);
                break;

            case BearerPowerId.WildSummoner:
                CreateAdornment("WildHornL", root.transform, PrimitiveType.Cylinder, new Vector3(-0.22f, 1.92f, 0f), new Vector3(0.08f, 0.28f, 0.08f), definition.secondaryColor, Quaternion.Euler(0f, 0f, -28f));
                CreateAdornment("WildHornR", root.transform, PrimitiveType.Cylinder, new Vector3(0.22f, 1.92f, 0f), new Vector3(0.08f, 0.28f, 0.08f), definition.secondaryColor, Quaternion.Euler(0f, 0f, 28f));
                CreateAdornment("WildLeafChest", root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.28f, 0.3f), new Vector3(0.16f, 0.34f, 0.045f), definition.secondaryColor, Quaternion.Euler(0f, 0f, 35f));
                CreateAdornment("WildLeafL", root.transform, PrimitiveType.Sphere, new Vector3(-0.38f, 1.42f, 0.1f), new Vector3(0.12f, 0.28f, 0.045f), definition.primaryColor, Quaternion.Euler(8f, 0f, -42f));
                CreateAdornment("WildLeafR", root.transform, PrimitiveType.Sphere, new Vector3(0.38f, 1.15f, 0.1f), new Vector3(0.12f, 0.28f, 0.045f), definition.secondaryColor, Quaternion.Euler(-8f, 0f, 42f));
                break;

            case BearerPowerId.EarthTitan:
                CreateAdornment("StoneShoulderL", root.transform, PrimitiveType.Sphere, new Vector3(-0.48f, 1.48f, 0f), new Vector3(0.38f, 0.28f, 0.38f), definition.primaryColor);
                CreateAdornment("StoneShoulderR", root.transform, PrimitiveType.Sphere, new Vector3(0.48f, 1.48f, 0f), new Vector3(0.38f, 0.28f, 0.38f), definition.primaryColor);
                CreateAdornment("StoneArmLUpper", root.transform, PrimitiveType.Sphere, new Vector3(-0.48f, 1.08f, 0f), new Vector3(0.28f, 0.36f, 0.28f), definition.primaryColor);
                CreateAdornment("StoneArmLLower", root.transform, PrimitiveType.Sphere, new Vector3(-0.48f, 0.72f, 0.02f), new Vector3(0.25f, 0.3f, 0.25f), definition.primaryColor);
                CreateAdornment("StoneArmRUpper", root.transform, PrimitiveType.Sphere, new Vector3(0.48f, 1.08f, 0f), new Vector3(0.28f, 0.36f, 0.28f), definition.primaryColor);
                CreateAdornment("StoneArmRLower", root.transform, PrimitiveType.Sphere, new Vector3(0.48f, 0.72f, 0.02f), new Vector3(0.25f, 0.3f, 0.25f), definition.primaryColor);
                CreateAdornment("EarthCrystal", root.transform, PrimitiveType.Cube, new Vector3(0f, 1.55f, -0.32f), new Vector3(0.18f, 0.55f, 0.18f), definition.secondaryColor, Quaternion.Euler(24f, 0f, 45f));
                CreateAdornment("EarthCrystalL", root.transform, PrimitiveType.Cube, new Vector3(-0.24f, 1.42f, -0.3f), new Vector3(0.12f, 0.38f, 0.12f), definition.secondaryColor, Quaternion.Euler(16f, 0f, 28f));
                CreateAdornment("EarthCrystalR", root.transform, PrimitiveType.Cube, new Vector3(0.24f, 1.42f, -0.3f), new Vector3(0.12f, 0.38f, 0.12f), definition.secondaryColor, Quaternion.Euler(-16f, 0f, 62f));
                break;
        }

        return root;
    }

    static bool TryCreateMeshyPowerVisual(Transform root, Transform owner, BearerPowerDefinition definition)
    {
        PlayerMovement movement = owner != null ? owner.GetComponent<PlayerMovement>() : null;
        if (movement == null || definition == null)
            return false;

        MeshyHeroRuntimeVisual powerVisual = root.gameObject.AddComponent<MeshyHeroRuntimeVisual>();
        if (!ConfigureMeshyPowerVisual(powerVisual, definition))
        {
            UnityEngine.Object.Destroy(powerVisual);
            return false;
        }

        if (powerVisual.ActivateTitan(movement))
            return true;

        UnityEngine.Object.Destroy(powerVisual);
        Debug.LogWarning($"Nao foi possivel ativar o modelo 3D de {definition.displayName}. Usando visual antigo como fallback.");
        return false;
    }

    static bool ConfigureMeshyPowerVisual(MeshyHeroRuntimeVisual visual, BearerPowerDefinition definition)
    {
        if (visual == null || definition == null)
            return false;

        visual.visualLocalPosition = Vector3.zero;
        visual.visualLocalEuler = Vector3.zero;
        visual.visualLocalScale = Vector3.one;
        visual.targetWorldHeight = 2.12f;
        visual.pauseAllAnimations = false;
        visual.disableAnimatorWhenPaused = false;
        visual.useDedicatedTitanController = true;
        visual.useRuntimeOverrideFallback = false;
        visual.maxSkinQuality = SkinQuality.Bone2;

        switch (definition.id)
        {
            case BearerPowerId.FlameHeir:
                visual.characterPrefabPath = "Characters/FlameHeir/Meshy_AI_Savage_Elf_Invoker_biped/Meshy_AI_Savage_Elf_Invoker_biped_Character_output";
                visual.materialPath = "Characters/FlameHeir/FlameHeir_Material";
                visual.controllerPath = "Characters/FlameHeir/FlameHeir";
                visual.visualInstanceName = "FlameHeirVisual";
                return true;

            case BearerPowerId.VoidWalker:
                visual.characterPrefabPath = "Characters/VoidWalker/Meshy_AI_Savage_Elf_Invoker_biped/Meshy_AI_Savage_Elf_Invoker_biped_Character_output";
                visual.materialPath = "Characters/VoidWalker/VoidWalker_Material";
                visual.controllerPath = "Characters/VoidWalker/VoidWalker";
                visual.visualInstanceName = "VoidWalkerVisual";
                return true;

            case BearerPowerId.WildSummoner:
                visual.characterPrefabPath = "Characters/SavageElfInvoker/Meshy_AI_Savage_Elf_Invoker_biped/Meshy_AI_Savage_Elf_Invoker_biped_Character_output";
                visual.materialPath = "Characters/SavageElfInvoker/SavageElfInvoker_Material";
                visual.controllerPath = "Characters/SavageElfInvoker/SavageElfInvoker";
                visual.visualInstanceName = "SavageElfInvokerVisual";
                return true;

            case BearerPowerId.EarthTitan:
                visual.characterPrefabPath = "Characters/MeshyHero/Meshy_AI_Stylized_fantasy_male_biped/Meshy_AI_Stylized_fantasy_male_biped_Character_output";
                visual.materialPath = "Characters/MeshyHero/MeshyHero_Material";
                visual.controllerPath = "Characters/MeshyHero/MeshyTitan";
                visual.visualInstanceName = "EarthTitanVisual";
                visual.visualLocalScale = Vector3.one * 1.12f;
                visual.targetWorldHeight = 2.35f;
                return true;

            default:
                return false;
        }
    }

    static void CreateAura(Transform parent, BearerPowerDefinition definition)
    {
        GameObject particleObject = new GameObject("BearerAura");
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = 2f;
        float minLifetime = definition.id == BearerPowerId.VoidWalker ? 0.9f : 0.55f;
        float maxLifetime = definition.id == BearerPowerId.VoidWalker ? 1.7f : 1.2f;
        float minSpeed = definition.id == BearerPowerId.EarthTitan ? 0.08f : 0.2f;
        float maxSpeed = definition.id == BearerPowerId.FlameHeir ? 1.35f : 0.8f;
        float minSize = definition.id == BearerPowerId.VoidWalker ? 0.08f : 0.035f;
        float maxSize = definition.id == BearerPowerId.VoidWalker ? 0.22f : 0.11f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(definition.primaryColor, definition.secondaryColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = definition.id == BearerPowerId.EarthTitan ? 24 : 42;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = definition.id switch
        {
            BearerPowerId.FlameHeir => 16f,
            BearerPowerId.VoidWalker => 12f,
            BearerPowerId.WildSummoner => 10f,
            _ => 6f
        };

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.48f;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        float drift = definition.id == BearerPowerId.VoidWalker ? 0.24f : 0.08f;
        float minRise = definition.id == BearerPowerId.EarthTitan ? 0.12f : 0.35f;
        float maxRise = definition.id == BearerPowerId.FlameHeir ? 1.45f : 1.15f;
        velocity.x = new ParticleSystem.MinMaxCurve(-drift, drift);
        velocity.y = new ParticleSystem.MinMaxCurve(minRise, maxRise);
        velocity.z = new ParticleSystem.MinMaxCurve(-drift, drift);

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = $"BearerAura_{definition.stableId}"
        };
        material.color = definition.primaryColor;
        renderer.material = material;
        particles.Play();
    }

    static GameObject CreateAdornment(
        string objectName,
        Transform parent,
        PrimitiveType primitive,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        Quaternion? localRotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation ?? Quaternion.identity;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                name = $"{objectName}Material",
                color = color
            };

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
            }

            renderer.material = material;
        }

        return part;
    }
}
