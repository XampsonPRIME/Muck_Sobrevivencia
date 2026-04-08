using UnityEngine;

public class MagicAreaEffect : MonoBehaviour
{
    public float duration = 0.6f;
    public float maxRadius = 8f;
    public float ringThickness = 0.18f;
    public float pulseHeight = 0.12f;
    public Color effectColor = new Color(0.24f, 0.86f, 1f, 0.9f);

    float elapsed;
    Transform outerRing;
    Transform innerPulse;
    Transform coreSphere;
    Material outerRingMaterial;
    Material innerPulseMaterial;
    Material coreMaterial;

    void Start()
    {
        BuildVisuals();
    }

    void Update()
    {
        if (outerRing == null || innerPulse == null || coreSphere == null)
            return;

        elapsed += Time.deltaTime;
        float t = duration > 0.01f ? Mathf.Clamp01(elapsed / duration) : 1f;
        float eased = 1f - Mathf.Pow(1f - t, 2f);

        float outerScale = Mathf.Lerp(0.2f, maxRadius * 2f, eased);
        float innerScale = Mathf.Lerp(0.15f, maxRadius * 1.35f, eased);
        float coreScale = Mathf.Lerp(0.7f, 0.1f, t);

        outerRing.localScale = new Vector3(outerScale, pulseHeight, outerScale);
        innerPulse.localScale = new Vector3(innerScale, pulseHeight * 0.7f, innerScale);
        coreSphere.localScale = Vector3.one * coreScale;

        float alpha = Mathf.Lerp(effectColor.a, 0f, t);
        SetAlpha(outerRingMaterial, alpha);
        SetAlpha(innerPulseMaterial, alpha * 0.7f);
        SetAlpha(coreMaterial, alpha * 0.9f);

        coreSphere.localPosition = new Vector3(0f, Mathf.Lerp(0.45f, 0.18f, t), 0f);

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    void BuildVisuals()
    {
        outerRing = CreateDisc("OuterRing", ringThickness);
        innerPulse = CreateDisc("InnerPulse", ringThickness * 1.2f);
        coreSphere = CreateCoreSphere();

        outerRingMaterial = CreateEffectMaterial(effectColor);
        innerPulseMaterial = CreateEffectMaterial(new Color(effectColor.r, effectColor.g, effectColor.b, effectColor.a * 0.8f));
        coreMaterial = CreateEffectMaterial(new Color(1f, 1f, 1f, effectColor.a));

        ApplyMaterial(outerRing, outerRingMaterial);
        ApplyMaterial(innerPulse, innerPulseMaterial);
        ApplyMaterial(coreSphere, coreMaterial);
    }

    Transform CreateDisc(string name, float height)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        disc.transform.localScale = new Vector3(0.1f, height, 0.1f);

        Collider collider = disc.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return disc.transform;
    }

    Transform CreateCoreSphere()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "CoreSphere";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        sphere.transform.localScale = Vector3.one * 0.7f;

        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return sphere.transform;
    }

    void ApplyMaterial(Transform target, Material material)
    {
        if (target == null || material == null)
            return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    Material CreateEffectMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        material.color = color;
        material.SetColor("_EmissionColor", color * 0.8f);
        return material;
    }

    void SetAlpha(Material material, float alpha)
    {
        if (material == null)
            return;

        Color color = material.color;
        color.a = alpha;
        material.color = color;
        material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, alpha) * 0.8f);
    }
}
