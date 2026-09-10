using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DamagePopup : MonoBehaviour
{
    float lifetime;
    float riseSpeed;
    float timer;
    TextMeshProUGUI popupText;
    RectTransform rectTransform;

    public void Initialize(float popupLifetime, float popupRiseSpeed)
    {
        lifetime = popupLifetime;
        riseSpeed = popupRiseSpeed;
        popupText = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (rectTransform != null)
            rectTransform.anchoredPosition += Vector2.up * riseSpeed * 20f * Time.deltaTime;

        if (popupText != null)
        {
            Color color = popupText.color;
            color.a = Mathf.Lerp(1f, 0f, timer / lifetime);
            popupText.color = color;
        }

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}

public class MobHealthBar : MonoBehaviour
{
    public Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);
    public float visibleDuration = 3f;
    public float popupLifetime = 0.8f;
    public float popupRiseSpeed = 1.4f;
    public Vector2 canvasSize = new Vector2(2.1f, 0.72f);
    public float canvasScale = 0.01f;

    Canvas worldCanvas;
    Image healthFillImage;
    TextMeshProUGUI healthText;
    float hideTime;

    public bool IsVisible => worldCanvas != null && worldCanvas.gameObject.activeSelf;

    public void Configure(Vector3 offset, float duration)
    {
        worldOffset = offset;
        visibleDuration = Mathf.Max(0.2f, duration);

        if (worldCanvas != null)
            worldCanvas.transform.localPosition = worldOffset;
    }

    public void Configure(Vector3 offset, float duration, float damageLifetime, float damageRiseSpeed, Vector2 size, float scale)
    {
        Configure(offset, duration);
        popupLifetime = Mathf.Max(0.1f, damageLifetime);
        popupRiseSpeed = Mathf.Max(0f, damageRiseSpeed);
        canvasSize = size;
        canvasScale = Mathf.Max(0.001f, scale);

        if (worldCanvas != null)
        {
            worldCanvas.transform.localScale = Vector3.one * canvasScale;
            RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
                canvasRect.sizeDelta = canvasSize;
        }
    }

    public void SetHealth(int currentHealth, int maxHealth, bool visible)
    {
        EnsureUI();
        UpdateHealth(currentHealth, maxHealth);

        if (visible)
            Show();
        else if (worldCanvas != null && Time.time > hideTime)
            worldCanvas.gameObject.SetActive(false);
    }

    public void ShowDamage(int currentHealth, int maxHealth, int damage)
    {
        EnsureUI();
        UpdateHealth(currentHealth, maxHealth);
        Show();

        if (damage > 0)
            CreateDamagePopup(damage);
    }

    public void Show()
    {
        EnsureUI();
        hideTime = Time.time + visibleDuration;
        worldCanvas.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (worldCanvas != null)
            worldCanvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (worldCanvas == null)
            return;

        Camera activeCamera = RuntimeCameraCache.Main;
        if (activeCamera != null)
        {
            worldCanvas.worldCamera = activeCamera;
            Vector3 direction = worldCanvas.transform.position - activeCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                worldCanvas.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        if (worldCanvas.gameObject.activeSelf && Time.time > hideTime)
            worldCanvas.gameObject.SetActive(false);
    }

    void EnsureUI()
    {
        if (worldCanvas != null)
            return;

        GameObject canvasObject = new GameObject("MobHealthBar");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = worldOffset;
        canvasObject.transform.localScale = Vector3.one * canvasScale;

        worldCanvas = canvasObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = RuntimeCameraCache.Main;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 30f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;

        GameObject bgObject = CreateUiObject("HealthBg", canvasObject.transform);
        Image bgImage = bgObject.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.055f, 0.04f, 0.92f);
        RectTransform bgRect = bgObject.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.1f, 0.48f);
        bgRect.anchorMax = new Vector2(0.9f, 0.72f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillObject = CreateUiObject("HealthFill", bgObject.transform);
        healthFillImage = fillObject.AddComponent<Image>();
        healthFillImage.type = Image.Type.Filled;
        healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject textObject = CreateUiObject("HealthText", canvasObject.transform);
        healthText = textObject.AddComponent<TextMeshProUGUI>();
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = 18f;
        healthText.fontStyle = FontStyles.Bold;
        healthText.color = new Color(1f, 0.96f, 0.86f, 1f);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.14f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        canvasObject.SetActive(false);
    }

    GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName);
        uiObject.transform.SetParent(parent, false);
        uiObject.AddComponent<RectTransform>();
        return uiObject;
    }

    void UpdateHealth(int currentHealth, int maxHealth)
    {
        int safeMax = Mathf.Max(1, maxHealth);
        int safeCurrent = Mathf.Clamp(currentHealth, 0, safeMax);
        float normalizedHealth = Mathf.Clamp01((float)safeCurrent / safeMax);

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = normalizedHealth;
            healthFillImage.color = Color.Lerp(
                new Color(0.78f, 0.08f, 0.04f, 1f),
                new Color(0.28f, 0.9f, 0.25f, 1f),
                normalizedHealth);
        }

        if (healthText != null)
            healthText.text = $"{safeCurrent}/{safeMax}";
    }

    void CreateDamagePopup(int damage)
    {
        GameObject popupObject = CreateUiObject($"Damage_{damage}", worldCanvas.transform);
        popupObject.transform.localPosition = new Vector3(Random.Range(-0.13f, 0.13f), 0.18f, 0f);

        TextMeshProUGUI popupText = popupObject.AddComponent<TextMeshProUGUI>();
        popupText.text = Mathf.Max(1, damage).ToString();
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.fontSize = 24f;
        popupText.fontStyle = FontStyles.Bold;
        popupText.color = new Color(1f, 0.9f, 0.28f, 1f);

        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 40f);

        DamagePopup popup = popupObject.AddComponent<DamagePopup>();
        popup.Initialize(popupLifetime, popupRiseSpeed);
    }
}

public static class RuntimeCameraCache
{
    const float RefreshInterval = 0.35f;

    static Camera cachedMainCamera;
    static float nextRefreshTime;

    public static Camera Main
    {
        get
        {
            if (cachedMainCamera == null || Time.unscaledTime >= nextRefreshTime)
            {
                cachedMainCamera = Camera.main;
                nextRefreshTime = Time.unscaledTime + RefreshInterval;
            }

            return cachedMainCamera;
        }
    }
}

public static class RuntimeMaterialUtility
{
    static Shader litShader;

    public static Material Create(string materialName, Color color)
    {
        Material material = new Material(GetLitShader());
        material.name = materialName;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else
            material.color = color;

        return material;
    }

    static Shader GetLitShader()
    {
        return litShader ??= Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
    }
}
