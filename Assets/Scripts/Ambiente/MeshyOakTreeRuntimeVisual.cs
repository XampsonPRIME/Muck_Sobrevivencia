using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MeshyOakTreeRuntimeVisual : MonoBehaviour
{
    const string VisualName = "AncientVerdantGuardVisual";
    static readonly Quaternion[] OrientationCandidates =
    {
        Quaternion.Euler(-90f, 0f, 0f),
        Quaternion.Euler(0f, 0f, 90f),
        Quaternion.identity,
        Quaternion.Euler(90f, 0f, 0f),
        Quaternion.Euler(0f, 0f, -90f)
    };

    public float targetWorldHeight = 18f;
    public float trunkColliderWidth = 4.4f;
    public float maxScaleMultiplier = 2000f;

    GameObject visualInstance;

    void Awake()
    {
        Build();
    }

    public void Build()
    {
        if (visualInstance == null)
            visualInstance = FindExistingVisual();

        if (visualInstance == null)
            visualInstance = CreateVisualInstance();

        if (visualInstance == null)
        {
            EnsureFallbackCollider();
            return;
        }

        ResetVisualTransform();
        ApplyMaterial();
        NormalizeBounds();
        OptimizeRenderers();
        ConfigureCollider();
    }

    GameObject FindExistingVisual()
    {
        Transform namedChild = transform.Find(VisualName);
        if (namedChild != null)
            return namedChild.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.GetComponentInChildren<Renderer>(true) != null)
                return child.gameObject;
        }

        return null;
    }

    GameObject CreateVisualInstance()
    {
        GameObject modelAsset = MeshyOakTreeRuntimeFactory.LoadModel();
        if (modelAsset == null)
            return null;

        GameObject instance = Instantiate(modelAsset, transform);
        instance.name = VisualName;
        return instance;
    }

    void ResetVisualTransform()
    {
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localScale = Vector3.one;
        visualInstance.transform.localRotation = ChooseUprightRotation();
    }

    Quaternion ChooseUprightRotation()
    {
        Quaternion bestRotation = Quaternion.identity;
        float bestHeight = 0f;

        for (int i = 0; i < OrientationCandidates.Length; i++)
        {
            Quaternion candidate = OrientationCandidates[i];
            visualInstance.transform.localRotation = candidate;

            if (TryGetBounds(out Bounds bounds) && bounds.size.y > bestHeight + 0.001f)
            {
                bestHeight = bounds.size.y;
                bestRotation = candidate;
            }
        }

        return bestRotation;
    }

    void ApplyMaterial()
    {
        Material material = MeshyOakTreeRuntimeFactory.GetMaterial();
        if (material == null || visualInstance == null)
            return;

        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].sharedMaterial = material;
        }
    }

    void NormalizeBounds()
    {
        if (targetWorldHeight <= 0.1f || !TryGetBounds(out Bounds bounds) || bounds.size.y <= 0.01f)
            return;

        float scaleMultiplier = Mathf.Clamp(targetWorldHeight / bounds.size.y, 0.01f, Mathf.Max(1f, maxScaleMultiplier));
        visualInstance.transform.localScale *= scaleMultiplier;

        if (!TryGetBounds(out bounds))
            return;

        visualInstance.transform.position += Vector3.up * (transform.position.y - bounds.min.y);
    }

    void OptimizeRenderers()
    {
        if (visualInstance == null)
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
        }

        Collider[] childColliders = visualInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            if (childColliders[i] != null)
                childColliders[i].enabled = false;
        }

        Light[] lights = visualInstance.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = false;
        }
    }

    void ConfigureCollider()
    {
        if (!TryGetBounds(out Bounds bounds))
        {
            EnsureFallbackCollider();
            return;
        }

        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider == null)
            collider = gameObject.AddComponent<BoxCollider>();

        float height = Mathf.Max(2.5f, bounds.size.y * 0.92f);
        float boundsWidth = Mathf.Min(bounds.size.x, bounds.size.z) * 0.28f;
        float width = Mathf.Clamp(Mathf.Max(trunkColliderWidth, boundsWidth), 1.6f, 5.4f);
        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        collider.center = new Vector3(localCenter.x, height * 0.5f, localCenter.z);
        collider.size = new Vector3(width, height, width);
    }

    void EnsureFallbackCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 2.3f, 0f);
        collider.size = new Vector3(3.2f, 4.6f, 3.2f);
    }

    bool TryGetBounds(out Bounds combinedBounds)
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
}
