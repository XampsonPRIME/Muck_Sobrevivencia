using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BowCombatHUD : MonoBehaviour
{
    Canvas canvas;
    GameObject ammoPanel;
    GameObject reticleRoot;
    Image ammoIcon;
    TextMeshProUGUI ammoText;
    bool built;

    public void SetState(bool bowEquipped, bool aiming, int arrowCount, Sprite arrowSprite)
    {
        EnsureBuilt();

        if (canvas != null)
            canvas.enabled = bowEquipped;

        if (reticleRoot != null)
            reticleRoot.SetActive(bowEquipped && aiming);

        if (ammoPanel != null)
            ammoPanel.SetActive(bowEquipped);

        if (ammoIcon != null)
        {
            ammoIcon.sprite = arrowSprite;
            ammoIcon.enabled = arrowSprite != null;
        }

        if (ammoText != null)
            ammoText.text = Mathf.Max(0, arrowCount).ToString();
    }

    void EnsureBuilt()
    {
        if (built)
            return;

        built = true;
        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9500;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        reticleRoot = new GameObject("BowReticle", typeof(RectTransform));
        reticleRoot.transform.SetParent(transform, false);
        RectTransform reticleRect = reticleRoot.GetComponent<RectTransform>();
        reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
        reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
        reticleRect.pivot = new Vector2(0.5f, 0.5f);
        reticleRect.anchoredPosition = Vector2.zero;
        reticleRect.sizeDelta = new Vector2(58f, 58f);

        CreateReticleLine("Top", new Vector2(0f, 21f), new Vector2(4f, 16f));
        CreateReticleLine("Bottom", new Vector2(0f, -21f), new Vector2(4f, 16f));
        CreateReticleLine("Left", new Vector2(-21f, 0f), new Vector2(16f, 4f));
        CreateReticleLine("Right", new Vector2(21f, 0f), new Vector2(16f, 4f));

        ammoPanel = new GameObject("BowAmmo", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
        ammoPanel.transform.SetParent(transform, false);
        RectTransform ammoRect = ammoPanel.GetComponent<RectTransform>();
        ammoRect.anchorMin = new Vector2(0.5f, 0f);
        ammoRect.anchorMax = new Vector2(0.5f, 0f);
        ammoRect.pivot = new Vector2(0.5f, 0f);
        ammoRect.sizeDelta = new Vector2(152f, 56f);
        ammoRect.anchoredPosition = new Vector2(192f, 62f);

        Image panelImage = ammoPanel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.045f, 0.04f, 0.72f);

        HorizontalLayoutGroup layout = ammoPanel.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 7, 7);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("ArrowIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(ammoPanel.transform, false);
        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 42f;
        iconLayout.preferredHeight = 42f;
        ammoIcon = iconObject.GetComponent<Image>();
        ammoIcon.preserveAspect = true;

        GameObject textObject = new GameObject("ArrowCount", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(ammoPanel.transform, false);
        LayoutElement textLayout = textObject.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 70f;
        textLayout.preferredHeight = 42f;
        ammoText = textObject.GetComponent<TextMeshProUGUI>();
        ammoText.fontSize = 30f;
        ammoText.fontStyle = FontStyles.Bold;
        ammoText.alignment = TextAlignmentOptions.MidlineLeft;
        ammoText.color = new Color(1f, 0.94f, 0.78f, 1f);
        ammoText.raycastTarget = false;

        SetState(false, false, 0, null);
    }

    void CreateReticleLine(string lineName, Vector2 position, Vector2 size)
    {
        GameObject line = new GameObject(lineName, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(reticleRoot.transform, false);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = line.GetComponent<Image>();
        image.color = new Color(1f, 0.9f, 0.62f, 0.92f);
        image.raycastTarget = false;
    }
}
