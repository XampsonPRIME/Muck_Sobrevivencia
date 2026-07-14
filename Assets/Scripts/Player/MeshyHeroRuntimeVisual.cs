using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MeshyHeroRuntimeVisual : MonoBehaviour
{
    const string DefaultCharacterPrefabPath = "Characters/MeshyHero/Meshy_AI_Stylized_fantasy_male_biped/Meshy_AI_Stylized_fantasy_male_biped_Character_output";
    const string DefaultHeroMaterialPath = "Characters/MeshyHero/MeshyHero_Material";
    const string DefaultControllerPath = "Characters/MeshyHero/MeshyTitan";

    public string characterPrefabPath = DefaultCharacterPrefabPath;
    public string materialPath = DefaultHeroMaterialPath;
    public string controllerPath = DefaultControllerPath;
    public string visualInstanceName = "MeshyTitanVisual";
    public Vector3 visualLocalPosition = Vector3.zero;
    public Vector3 visualLocalEuler = Vector3.zero;
    public Vector3 visualLocalScale = Vector3.one * 1.12f;
    public bool normalizeVisualBounds = true;
    public float targetWorldHeight = 2.35f;
    public bool pauseAllAnimations = false;
    public bool disableAnimatorWhenPaused = true;
    public bool useDedicatedTitanController = true;
    public bool useRuntimeOverrideFallback = true;
    public bool optimizeRuntimeRenderers = true;
    public SkinQuality maxSkinQuality = SkinQuality.Bone1;

    PlayerMovement ownerMovement;
    PlayerInteraction ownerInteraction;
    GameObject visualInstance;
    Animator activeAnimator;
    Animator previousAnimator;
    GameObject previousAnimatorObject;
    bool previousAnimatorWasActive;
    bool initialized;
    bool restored;

    public Animator ActiveAnimator => activeAnimator;

    public bool ActivateTitan(PlayerMovement movement)
    {
        if (initialized)
            return activeAnimator != null;

        ownerMovement = movement != null ? movement : GetComponentInParent<PlayerMovement>();
        if (ownerMovement == null)
            return false;

        ownerInteraction = ownerMovement.GetComponent<PlayerInteraction>();
        previousAnimator = ownerMovement.VisualAnimator != null
            ? ownerMovement.VisualAnimator
            : ownerMovement.GetComponentInChildren<Animator>(true);

        previousAnimatorObject = previousAnimator != null ? previousAnimator.gameObject : null;
        previousAnimatorWasActive = previousAnimatorObject != null && previousAnimatorObject.activeSelf;

        GameObject prefab = Resources.Load<GameObject>(ResolvePath(characterPrefabPath, DefaultCharacterPrefabPath));
        if (prefab == null)
            return false;

        visualInstance = Instantiate(prefab, transform);
        visualInstance.name = string.IsNullOrWhiteSpace(visualInstanceName) ? "MeshyVisual" : visualInstanceName;
        visualInstance.transform.localPosition = visualLocalPosition;
        visualInstance.transform.localRotation = Quaternion.Euler(visualLocalEuler);
        visualInstance.transform.localScale = visualLocalScale;
        NormalizeVisualBounds();
        OptimizeRuntimeVisual();

        activeAnimator = visualInstance.GetComponentInChildren<Animator>(true);
        if (activeAnimator == null)
            activeAnimator = visualInstance.AddComponent<Animator>();

        RuntimeAnimatorController previousController = previousAnimator != null
            ? previousAnimator.runtimeAnimatorController
            : null;

        activeAnimator.applyRootMotion = false;
        activeAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        activeAnimator.fireEvents = false;
        activeAnimator.updateMode = AnimatorUpdateMode.Normal;
        activeAnimator.speed = 1f;

        if (pauseAllAnimations)
        {
            RuntimeAnimatorController staticController = previousController != null
                ? previousController
                : activeAnimator.runtimeAnimatorController;

            if (staticController != null)
                activeAnimator.runtimeAnimatorController = staticController;

            activeAnimator.speed = 0f;
            activeAnimator.Rebind();
            activeAnimator.Update(0f);
            activeAnimator.enabled = !disableAnimatorWhenPaused;
        }
        else
        {
            RuntimeAnimatorController titanController = Resources.Load<RuntimeAnimatorController>(ResolvePath(controllerPath, DefaultControllerPath));
            if (useDedicatedTitanController && titanController != null)
            {
                activeAnimator.runtimeAnimatorController = titanController;
            }
            else if (!useRuntimeOverrideFallback)
            {
                if (previousController != null)
                    activeAnimator.runtimeAnimatorController = previousController;
            }
            else
            {
                MeshyTitanAnimatorDriver titanDriver = visualInstance.GetComponent<MeshyTitanAnimatorDriver>() ??
                                                       visualInstance.AddComponent<MeshyTitanAnimatorDriver>();

                if (!titanDriver.Initialize(activeAnimator, previousController))
                {
                    if (titanController != null)
                        activeAnimator.runtimeAnimatorController = titanController;
                    else if (previousController != null)
                        activeAnimator.runtimeAnimatorController = previousController;
                }
            }
        }

        ApplyHeroMaterial();
        HidePreviousVisual();

        ownerMovement.OverrideVisualAnimator(activeAnimator);
        ownerInteraction?.RefreshEquippedVisuals();

        initialized = true;
        restored = false;
        return activeAnimator != null;
    }

    void NormalizeVisualBounds()
    {
        if (!normalizeVisualBounds || visualInstance == null || targetWorldHeight <= 0.1f)
            return;

        if (!TryGetVisualBounds(out Bounds bounds) || bounds.size.y <= 0.01f)
            return;

        float scaleMultiplier = Mathf.Clamp(targetWorldHeight / bounds.size.y, 0.02f, 4f);
        visualInstance.transform.localScale *= scaleMultiplier;

        if (!TryGetVisualBounds(out bounds))
            return;

        float groundDelta = ownerMovement != null
            ? ownerMovement.transform.position.y - bounds.min.y
            : transform.position.y - bounds.min.y;

        visualInstance.transform.position += Vector3.up * groundDelta;
    }

    bool TryGetVisualBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;

        if (visualInstance == null)
            return false;

        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    public void DeactivateTitan()
    {
        RestorePreviousVisual(true);

        if (visualInstance != null)
            Destroy(visualInstance);

        visualInstance = null;
        activeAnimator = null;
        initialized = false;
    }

    void OnDestroy()
    {
        RestorePreviousVisual(false);
    }

    void ApplyHeroMaterial()
    {
        Material material = Resources.Load<Material>(ResolvePath(materialPath, DefaultHeroMaterialPath));
        if (material == null || visualInstance == null)
            return;

        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
                renderer.sharedMaterial = material;
        }
    }

    static string ResolvePath(string configuredPath, string fallbackPath)
    {
        return string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath;
    }

    void OptimizeRuntimeVisual()
    {
        if (!optimizeRuntimeRenderers || visualInstance == null)
            return;

        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = true;

            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.updateWhenOffscreen = false;
                skinned.skinnedMotionVectors = false;
                skinned.quality = maxSkinQuality;
            }
        }

        Collider[] colliders = visualInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Light[] lights = visualInstance.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = false;
        }
    }

    void HidePreviousVisual()
    {
        if (previousAnimatorObject == null || previousAnimatorObject == visualInstance)
            return;

        previousAnimatorObject.SetActive(false);
    }

    void RestorePreviousVisual(bool refreshEquippedVisuals)
    {
        if (restored)
            return;

        restored = true;
        activeAnimator = null;

        if (previousAnimatorObject != null)
            previousAnimatorObject.SetActive(previousAnimatorWasActive);

        if (ownerMovement != null && previousAnimator != null)
            ownerMovement.OverrideVisualAnimator(previousAnimator);

        if (refreshEquippedVisuals)
            ownerInteraction?.RefreshEquippedVisuals();
    }
}
