using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BearerPowerHUD : MonoBehaviour
{
    sealed class SlotView
    {
        public BearerAbilitySlot slot;
        public Image background;
        public Image cooldownFill;
        public TMP_Text keyText;
        public TMP_Text nameText;
        public TMP_Text stateText;
    }

    BearerPowerController controller;
    Canvas canvas;
    TMP_Text powerTitle;
    readonly SlotView[] slots = new SlotView[4];

    public void Configure(BearerPowerController configuredController)
    {
        controller = configuredController;
        Build();
        Refresh();
    }

    public void Refresh()
    {
        if (canvas == null || controller == null)
            return;

        BearerPowerService service = controller.GetComponent<BearerPowerService>();
        BearerPowerDefinition definition = service != null ? service.CurrentDefinition : null;
        bool visible = definition != null &&
                       !GameState.IsPowerSelectionOpen &&
                       !GameState.IsWorldLoading &&
                       !GameState.IsInLobby &&
                       !GameState.IsPlayerDead;
        canvas.enabled = visible;
        if (!visible)
            return;

        powerTitle.text = definition.displayName.ToUpperInvariant();
        powerTitle.color = definition.secondaryColor;

        RefreshSlot(slots[0], controller.GetAbility(BearerAbilitySlot.Primary), definition);
        RefreshSlot(slots[1], controller.GetAbility(BearerAbilitySlot.Secondary), definition);
        RefreshSlot(slots[2], controller.GetAbility(BearerAbilitySlot.Utility), definition);
        RefreshSlot(slots[3], controller.GetAbility(BearerAbilitySlot.Transformation), definition);
    }

    void RefreshSlot(SlotView view, BearerPowerAbilityDefinition ability, BearerPowerDefinition power)
    {
        if (view == null)
            return;

        bool exists = ability != null;
        bool unlocked = exists && controller.CurrentLevel >= ability.unlockLevel;
        float cooldown = exists ? controller.GetCooldownRemaining(ability) : 0f;
        bool passive = exists && ability.slot == BearerAbilitySlot.Passive;

        view.nameText.text = exists ? ability.displayName : "-";
        view.background.color = unlocked
            ? new Color(power.primaryColor.r * 0.34f, power.primaryColor.g * 0.34f, power.primaryColor.b * 0.34f, 0.94f)
            : new Color(0.08f, 0.08f, 0.09f, 0.78f);
        view.nameText.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f);
        view.keyText.color = unlocked ? power.secondaryColor : new Color(0.45f, 0.45f, 0.48f, 1f);

        if (!exists)
        {
            view.stateText.text = string.Empty;
            view.cooldownFill.fillAmount = 0f;
            return;
        }

        if (!unlocked)
        {
            view.stateText.text = $"NIVEL {ability.unlockLevel}";
            view.cooldownFill.fillAmount = 1f;
        }
        else if (passive)
        {
            view.stateText.text = "PASSIVO";
            view.cooldownFill.fillAmount = 0f;
        }
        else if (view.slot == BearerAbilitySlot.Transformation && controller.IsTransformed)
        {
            view.stateText.text = $"{controller.GetTransformationRemaining():0.0}s";
            view.cooldownFill.fillAmount = 0f;
        }
        else if (cooldown > 0.01f)
        {
            view.stateText.text = $"{cooldown:0.0}s";
            view.cooldownFill.fillAmount = Mathf.Clamp01(cooldown / Mathf.Max(0.01f, ability.cooldown));
        }
        else
        {
            view.stateText.text = $"{ability.staminaCost:0} STA";
            view.cooldownFill.fillAmount = 0f;
        }
    }

    void Build()
    {
        if (canvas != null)
            return;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 820;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        GameObject root = CreateUiObject("AbilityBar", transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 112f);
        rootRect.sizeDelta = new Vector2(760f, 92f);

        Image backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0.025f, 0.022f, 0.025f, 0.72f);
        backdrop.raycastTarget = false;

        powerTitle = CreateText("PowerTitle", root.transform, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform titleRect = powerTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -4f);
        titleRect.sizeDelta = new Vector2(0f, 24f);

        BearerAbilitySlot[] slotTypes =
        {
            BearerAbilitySlot.Primary,
            BearerAbilitySlot.Secondary,
            BearerAbilitySlot.Utility,
            BearerAbilitySlot.Transformation
        };
        string[] keys = { "Q", "R", "F", "V" };

        for (int i = 0; i < slots.Length; i++)
            slots[i] = CreateSlot(root.transform, slotTypes[i], keys[i], -280f + i * 187f);
    }

    SlotView CreateSlot(Transform parent, BearerAbilitySlot slot, string key, float x)
    {
        GameObject slotObject = CreateUiObject($"Slot_{slot}", parent);
        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 8f);
        rect.sizeDelta = new Vector2(176f, 58f);

        Image background = slotObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.12f, 0.94f);
        background.raycastTarget = false;

        GameObject fillObject = CreateUiObject("Cooldown", slotObject.transform);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        Stretch(fillRect);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = new Color(0f, 0f, 0f, 0.62f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.raycastTarget = false;

        TMP_Text keyText = CreateText("Key", slotObject.transform, 23f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform keyRect = keyText.rectTransform;
        keyRect.anchorMin = new Vector2(0f, 0f);
        keyRect.anchorMax = new Vector2(0f, 1f);
        keyRect.pivot = new Vector2(0f, 0.5f);
        keyRect.anchoredPosition = new Vector2(8f, 0f);
        keyRect.sizeDelta = new Vector2(30f, 0f);
        keyText.text = key;

        TMP_Text name = CreateText("Name", slotObject.transform, 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        RectTransform nameRect = name.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(42f, -2f);
        nameRect.offsetMax = new Vector2(-5f, -3f);
        name.enableAutoSizing = true;
        name.fontSizeMin = 10f;
        name.fontSizeMax = 14f;

        TMP_Text state = CreateText("State", slotObject.transform, 12f, FontStyles.Normal, TextAlignmentOptions.Left);
        RectTransform stateRect = state.rectTransform;
        stateRect.anchorMin = new Vector2(0f, 0f);
        stateRect.anchorMax = new Vector2(1f, 0.5f);
        stateRect.offsetMin = new Vector2(42f, 3f);
        stateRect.offsetMax = new Vector2(-5f, 2f);
        state.color = new Color(0.82f, 0.82f, 0.84f, 1f);

        return new SlotView
        {
            slot = slot,
            background = background,
            cooldownFill = fill,
            keyText = keyText,
            nameText = name,
            stateText = state
        };
    }

    static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject created = new GameObject(objectName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    static TMP_Text CreateText(string objectName, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
