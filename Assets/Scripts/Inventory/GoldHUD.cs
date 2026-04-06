using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoldHUD : MonoBehaviour
{
    public Vector2 panelAnchorPosition = new Vector2(28f, -72f);
    public Vector2 panelSize = new Vector2(280f, 164f);

    Inventory inventory;
    DayNightCycle cycle;

    RectTransform rootRect;
    TextMeshProUGUI dayValueText;
    TextMeshProUGUI hourValueText;
    TextMeshProUGUI goldValueText;
    Image goldIcon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<GoldHUD>() != null)
            return;

        GameObject hudObject = new GameObject("Gold HUD");
        hudObject.AddComponent<GoldHUD>();
    }

    void Start()
    {
        inventory = FindFirstObjectByType<Inventory>();
        cycle = FindFirstObjectByType<DayNightCycle>();
        EnsureUI();
        HideOriginalClockTexts();
        Refresh();
    }

    void Update()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (cycle == null)
            cycle = FindFirstObjectByType<DayNightCycle>();

        EnsureUI();

        HideOriginalClockTexts();
        Refresh();
    }

    void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null)
            return;

        Transform existingRoot = canvas.transform.Find("HudInfoPanel");
        GameObject rootObject;

        if (existingRoot != null)
        {
            rootObject = existingRoot.gameObject;
        }
        else
        {
            rootObject = new GameObject("HudInfoPanel");
            rootObject.transform.SetParent(canvas.transform, false);
            rootObject.AddComponent<CanvasRenderer>();
        }

        rootRect = rootObject.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = rootObject.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = panelAnchorPosition;
        rootRect.sizeDelta = panelSize;

        Image background = rootObject.GetComponent<Image>();
        if (background == null)
            background = rootObject.AddComponent<Image>();
        background.color = new Color(0.17f, 0.14f, 0.06f, 0.88f);

        Outline outline = rootObject.GetComponent<Outline>();
        if (outline == null)
            outline = rootObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.87f, 0.44f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        dayValueText = EnsureText(rootObject.transform, "DayValue", new Vector2(18f, -18f), new Vector2(236f, 34f));
        hourValueText = EnsureText(rootObject.transform, "HourValue", new Vector2(18f, -64f), new Vector2(236f, 34f));
        goldIcon = EnsureIcon(rootObject.transform, "GoldIcon", new Vector2(18f, -112f), new Vector2(44f, 44f));
        goldValueText = EnsureText(rootObject.transform, "GoldValue", new Vector2(72f, -116f), new Vector2(180f, 36f));

        dayValueText.fontSize = 30f;
        dayValueText.color = Color.white;

        hourValueText.fontSize = 30f;
        hourValueText.color = Color.white;

        goldValueText.fontSize = 30f;
        goldValueText.fontStyle = FontStyles.Bold;
        goldValueText.color = new Color(1f, 0.95f, 0.68f, 1f);
    }

    Canvas ResolveCanvas()
    {
        if (cycle != null && cycle.dayText != null)
            return cycle.dayText.canvas != null ? cycle.dayText.canvas.rootCanvas : null;

        return FindFirstObjectByType<Canvas>();
    }

    TextMeshProUGUI EnsureText(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject;
        TextMeshProUGUI text;

        if (existing != null)
        {
            textObject = existing.gameObject;
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.AddComponent<CanvasRenderer>();
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.alignment = TextAlignmentOptions.Left;
        text.margin = Vector4.zero;
        return text;
    }

    Image EnsureIcon(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = parent.Find(objectName);
        GameObject iconObject;
        Image image;

        if (existing != null)
        {
            iconObject = existing.gameObject;
            image = iconObject.GetComponent<Image>();
        }
        else
        {
            iconObject = new GameObject(objectName);
            iconObject.transform.SetParent(parent, false);
            iconObject.AddComponent<CanvasRenderer>();
            image = iconObject.AddComponent<Image>();
        }

        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        image.sprite = GoldItemRegistry.GetGoldSprite();
        image.preserveAspect = true;
        image.color = Color.white;
        return image;
    }

    void HideOriginalClockTexts()
    {
        if (cycle == null)
            return;

        if (cycle.dayText != null)
            cycle.dayText.gameObject.SetActive(false);

        if (cycle.hourText != null)
            cycle.hourText.gameObject.SetActive(false);
    }

    void Refresh()
    {
        if (dayValueText == null || hourValueText == null || goldValueText == null)
            return;

        int goldAmount = 0;
        if (inventory != null)
        {
            InventoryItem goldItem = inventory.GetItem("Gold");
            if (goldItem != null)
                goldAmount = goldItem.quantity;
        }

        if (cycle != null)
        {
            dayValueText.text = $"Dia: {cycle.CurrentDay}";
            hourValueText.text = $"Hora: {cycle.CurrentTimeFormatted}";
        }

        goldValueText.text = $"Gold: {goldAmount}";

        if (goldIcon != null && goldIcon.sprite == null)
            goldIcon.sprite = GoldItemRegistry.GetGoldSprite();
    }
}
