using UnityEngine;

public class BossAreaAttackIndicator : MonoBehaviour
{
    float duration;
    float thickness;
    Color baseColor;
    Renderer cachedRenderer;
    Material runtimeMaterial;
    Vector3 baseScale;
    float elapsed;
    bool impactTriggered;

    public static BossAreaAttackIndicator Create(Vector3 center, float radius, float thickness, Color color, float duration)
    {
        GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicatorObject.name = "BossAreaAttackIndicator";
        indicatorObject.transform.position = center;
        indicatorObject.transform.localScale = new Vector3(radius * 2f, Mathf.Max(0.01f, thickness), radius * 2f);

        Collider collider = indicatorObject.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        BossAreaAttackIndicator indicator = indicatorObject.AddComponent<BossAreaAttackIndicator>();
        indicator.Initialize(color, duration, thickness);
        return indicator;
    }

    public void Initialize(Color color, float indicatorDuration, float indicatorThickness)
    {
        duration = Mathf.Max(0.1f, indicatorDuration);
        thickness = Mathf.Max(0.01f, indicatorThickness);
        baseColor = color;
        baseScale = transform.localScale;
        cachedRenderer = GetComponent<Renderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        runtimeMaterial = new Material(shader);
        runtimeMaterial.color = color;

        if (runtimeMaterial.HasProperty("_Surface"))
            runtimeMaterial.SetFloat("_Surface", 1f);
        if (runtimeMaterial.HasProperty("_Blend"))
            runtimeMaterial.SetFloat("_Blend", 0f);
        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", color);
        if (runtimeMaterial.HasProperty("_SrcBlend"))
            runtimeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (runtimeMaterial.HasProperty("_DstBlend"))
            runtimeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (runtimeMaterial.HasProperty("_ZWrite"))
            runtimeMaterial.SetInt("_ZWrite", 0);
        if (runtimeMaterial.HasProperty("_Cull"))
            runtimeMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        runtimeMaterial.renderQueue = 3000;

        if (cachedRenderer != null)
            cachedRenderer.sharedMaterial = runtimeMaterial;
    }

    public void TriggerImpact()
    {
        impactTriggered = true;
        elapsed = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        if (!impactTriggered)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = 0.85f + progress * 0.25f;
            transform.localScale = new Vector3(baseScale.x * pulse, thickness, baseScale.z * pulse);
            ApplyColor(new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(baseColor.a * 0.35f, baseColor.a, progress)));

            if (elapsed >= duration)
                TriggerImpact();

            return;
        }

        float impactProgress = Mathf.Clamp01(elapsed / 0.18f);
        transform.localScale = new Vector3(baseScale.x * (1f + impactProgress * 0.18f), thickness, baseScale.z * (1f + impactProgress * 0.18f));
        ApplyColor(new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(baseColor.a, 0f, impactProgress)));

        if (impactProgress >= 1f)
            Destroy(gameObject);
    }

    void ApplyColor(Color color)
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.color = color;
        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", color);
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
