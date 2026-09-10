using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MessageSystem : MonoBehaviour
{
    public static MessageSystem Instance;

    public GameObject messagePrefab;
    public Transform panel;

    void Awake()
    {
        Instance = this;
    }

    public void ShowMessage(string message)
    {
        if (messagePrefab == null || panel == null)
            return;

        GameObject obj = Instantiate(messagePrefab, panel);

        MessageItem item = obj.GetComponent<MessageItem>();
        if (item != null)
            item.Setup(message);
    }
}

public class PickupMessageSystem : MonoBehaviour
{
    static PickupMessageSystem instance;

    Canvas canvas;
    RectTransform canvasRect;
    int spawnCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        EnsureInstance();
    }

    public static void Show(string itemName, int amount, Vector3 worldPosition, Sprite icon = null)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return;

        PickupMessageSystem system = EnsureInstance();
        if (system == null)
            return;

        system.SpawnToast($"+{amount} {itemName}", worldPosition, icon);
    }

    static PickupMessageSystem EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<PickupMessageSystem>();
        if (instance != null)
            return instance;

        GameObject systemObject = new GameObject("PickupMessageSystem");
        DontDestroyOnLoad(systemObject);
        instance = systemObject.AddComponent<PickupMessageSystem>();
        return instance;
    }

    void Awake()
    {
        instance = this;
        EnsureCanvas();
    }

    void SpawnToast(string message, Vector3 worldPosition, Sprite icon)
    {
        EnsureCanvas();
        if (canvas == null)
            return;

        int lane = spawnCounter++ % 4;
        Vector2 offset = new Vector2(70f, 24f + lane * 30f);

        GameObject toastObject = new GameObject("PickupMessageToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(PickupMessageToast));
        toastObject.transform.SetParent(canvas.transform, false);

        RectTransform toastRect = toastObject.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 0.5f);
        toastRect.anchorMax = new Vector2(0.5f, 0.5f);
        toastRect.pivot = new Vector2(0f, 0.5f);
        toastRect.sizeDelta = new Vector2(308f, 54f);

        Image background = toastObject.GetComponent<Image>();
        background.color = new Color(0.06f, 0.055f, 0.035f, 0.82f);
        background.raycastTarget = false;

        Outline outline = toastObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.78f, 0.32f, 0.72f);
        outline.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layout = toastObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 14, 6, 6);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(toastObject.transform, false);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = icon != null ? 42f : 0f;
        iconLayout.preferredHeight = 42f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(toastObject.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = 21f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.95f, 0.72f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
        textLayout.preferredWidth = 230f;
        textLayout.preferredHeight = 42f;

        PickupMessageToast toast = toastObject.GetComponent<PickupMessageToast>();
        toast.Initialize(canvas, worldPosition, offset);
    }

    void EnsureCanvas()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("PickupMessageCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        canvasRect = canvasObject.GetComponent<RectTransform>();
        if (canvasRect == null)
            canvasRect = canvasObject.AddComponent<RectTransform>();
    }
}

public class PickupMessageToast : MonoBehaviour
{
    Canvas canvas;
    RectTransform canvasRect;
    RectTransform rect;
    CanvasGroup canvasGroup;
    Vector3 worldPosition;
    Vector2 baseOffset;
    float duration = 1.45f;
    float elapsed;

    public void Initialize(Canvas ownerCanvas, Vector3 targetWorldPosition, Vector2 offset)
    {
        canvas = ownerCanvas;
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        worldPosition = targetWorldPosition;
        baseOffset = offset;
        UpdatePosition(0f);
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        UpdatePosition(t);

        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, t)));

        if (t >= 1f)
            Destroy(gameObject);
    }

    void UpdatePosition(float t)
    {
        if (rect == null || canvasRect == null)
            return;

        Camera camera = RuntimeCameraCache.Main;
        if (camera == null)
            return;

        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint))
            return;

        Vector2 drift = new Vector2(12f, 42f) * Mathf.SmoothStep(0f, 1f, t);
        rect.anchoredPosition = localPoint + baseOffset + drift;
    }
}
