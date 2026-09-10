using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shared presentation for the prefab and runtime-built lobby. Owns no save/network state.</summary>
public sealed class LobbyPresentation : MonoBehaviour
{
    public static readonly Color Ivory = new Color(0.91f, 0.89f, 0.81f);
    static readonly Color Gold = new Color(0.77f, 0.62f, 0.36f);
    readonly Dictionary<Canvas, bool> hiddenCanvases = new Dictionary<Canvas, bool>();
    readonly List<RectTransform> motes = new List<RectTransform>();
    Canvas menuCanvas;
    RectTransform artwork;
    CanvasGroup content;
    GameObject popup;
    Button online;
    Button primary;
    TextMeshProUGUI status;
    float startedAt;
    float nextCanvasScan;
    bool reducedMotion;
    Texture2D backgroundTexture;
    WorldLibraryUI worldLibrary;

    public void Initialize(Canvas canvas, GameObject menu, GameObject backdrop, GameObject panel,
        TextMeshProUGUI statusText, Button continueButton, Button newButton, Button onlineButton)
    {
        menuCanvas = canvas;
        popup = backdrop;
        online = onlineButton;
        primary = continueButton;
        status = statusText;
        startedAt = Time.unscaledTime;
        reducedMotion = PlayerPrefs.GetInt("lobby.reduced_motion", 0) == 1;
        canvas.sortingOrder = 500;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Transform overlay = menu.transform.parent;
        var overlayImage = overlay.GetComponent<Image>();
        if (overlayImage != null) overlayImage.color = new Color(0.018f, 0.03f, 0.035f, 1);

        // Retain the existing functional controls, replace only their presentation.
        foreach (Transform child in overlay)
            if (child != menu.transform && child != backdrop.transform && child != statusText.transform)
                child.gameObject.SetActive(false);
        foreach (Transform child in menu.transform)
            if (child != continueButton.transform && child != newButton.transform && child != onlineButton.transform)
                child.gameObject.SetActive(false);

        artwork = Rect("Sanctuary artwork", overlay, Vector2.zero, Vector2.zero);
        Stretch(artwork);
        artwork.SetAsFirstSibling();
        var art = artwork.gameObject.AddComponent<RawImage>();
        backgroundTexture = Resources.Load<Texture2D>("MainMenu/Sanctuary");
        art.texture = backgroundTexture;
        art.raycastTarget = false;
        var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = backgroundTexture != null ? (float)backgroundTexture.width / backgroundTexture.height : 16f / 9f;

        var atmosphere = Rect("Atmosphere", overlay, Vector2.zero, Vector2.zero);
        Stretch(atmosphere);
        atmosphere.SetSiblingIndex(1);
        // Gradient is vertex based: no fullscreen temporary textures or per-frame allocations.
        atmosphere.gameObject.AddComponent<LobbyShade>();
        for (int i = 0; i < 32; i++)
        {
            var mote = Rect("Ember", atmosphere, Vector2.zero, new Vector2(i % 4 == 0 ? 3 : 1.5f, i % 4 == 0 ? 3 : 1.5f));
            var img = mote.gameObject.AddComponent<Image>();
            img.color = new Color(0.96f, 0.68f, 0.3f, 0.3f);
            img.raycastTarget = false;
            mote.localRotation = Quaternion.Euler(0, 0, 45);
            motes.Add(mote);
        }

        // A centered safe area keeps the composition usable on 4:3 and ultrawide displays.
        RectTransform safe = Rect("Menu safe area", menu.transform, Vector2.zero, new Vector2(1440, 790));
        content = safe.gameObject.AddComponent<CanvasGroup>();
        Label(safe, "UMA ERA ESQUECIDA. UM NOVO LEGADO.", new Vector2(0, -84), new Vector2(670, 30), 13, Gold, 3);
        var title = Label(safe, "ELARION", new Vector2(-6, -119), new Vector2(740, 116), 94, Ivory, 8);
        title.fontStyle = FontStyles.Normal;
        Label(safe, "R E L I C S   O F   T H E   F O R G O T T E N", new Vector2(2, -232), new Vector2(700, 32), 16, Gold);
        Rule(safe, new Vector2(0, -296), new Vector2(74, 2), Gold);
        Label(safe, "O esquecimento é apenas o começo.", new Vector2(0, -318), new Vector2(600, 42), 23, Ivory);
        Label(safe, "Sobreviva. Descubra seu poder. Deixe seu legado.", new Vector2(0, -362), new Vector2(650, 35), 16, new Color(0.63f, 0.69f, 0.68f));
        PlaceButton(continueButton, safe, new Vector2(0, -445), true);
        PlaceButton(newButton, safe, new Vector2(0, -526), false);
        PlaceButton(onlineButton, safe, new Vector2(0, -607), false);
        newButton.GetComponentInChildren<TMP_Text>().text = "Nova jornada";
        onlineButton.GetComponentInChildren<TMP_Text>().text = "Jogar com amigos";
        Rule(safe, new Vector2(0, -733), new Vector2(1440, 1), new Color(0.77f, 0.62f, 0.36f, 0.24f));
        Label(safe, "ELARION  /  DEMO 0.1.0", new Vector2(0, -757), new Vector2(380, 24), 12, new Color(0.62f, 0.67f, 0.66f), 1);
        var footer = Rect("Atmosphere controls", safe, Vector2.zero, Vector2.zero);
        Pin(footer, new Vector2(966, -635), new Vector2(474, 149));
        footer.gameObject.AddComponent<Image>().color = new Color(0.018f, 0.038f, 0.043f, 0.96f);
        Rule(footer, Vector2.zero, new Vector2(474, 1), Gold);
        Label(footer, "Um mundo esquecido espera por você.", new Vector2(18, -17), new Vector2(438, 35), 20, Ivory);
        var motion = Rect("Motion preference", footer, Vector2.zero, Vector2.zero);
        Pin(motion, new Vector2(18, -68), new Vector2(438, 59));
        motion.gameObject.AddComponent<Image>();
        var motionButton = motion.gameObject.AddComponent<Button>();
        Label(motion, "Animação ambiente", new Vector2(18, -11), new Vector2(272, 36), 20, Ivory);
        var motionState = Label(motion, "", new Vector2(295, -11), new Vector2(127, 36), 17, Gold);
        motionState.alignment = TextAlignmentOptions.Center;
        StyleButton(motionButton, false);
        Rule(motion, Vector2.zero, new Vector2(3, 59), Gold);
        // Keep the state label inside the bundled font's glyph set; the color and button tint convey the active state.
        System.Action refreshMotion = () => { motionState.text = reducedMotion ? "DESATIVADA" : "ATIVA"; motionState.color = reducedMotion ? Ivory : Gold; };
        refreshMotion();
        motionButton.onClick.AddListener(() => {
            reducedMotion = !reducedMotion;
            PlayerPrefs.SetInt("lobby.reduced_motion", reducedMotion ? 1 : 0);
            PlayerPrefs.Save();
            refreshMotion();
        });

        StylePopup(panel);
        backdrop.transform.SetAsLastSibling();
        statusText.transform.SetAsLastSibling();
        statusText.fontSize = 16;
        statusText.color = Ivory;
        var sr = statusText.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0);
        sr.sizeDelta = new Vector2(1100, 46);
        sr.anchoredPosition = new Vector2(0, 28);
        HideGameplayCanvases();
    }

    static void PlaceButton(Button button, Transform parent, Vector2 position, bool featured)
    {
        button.transform.SetParent(parent, false);
        Pin((RectTransform)button.transform, position, new Vector2(440, 64));
        StyleButton(button, featured);
        var text = button.GetComponentInChildren<TMP_Text>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = 24;
        text.fontStyle = FontStyles.Normal;
        Stretch(text.rectTransform);
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.offsetMin = new Vector2(28, 0);
        text.rectTransform.offsetMax = new Vector2(-44, 0);
        Rule(button.transform, Vector2.zero, new Vector2(featured ? 3 : 1, 64), featured ? Gold : new Color(0.77f, 0.62f, 0.36f, 0.3f));
        Label(button.transform, "›", new Vector2(397, -13), new Vector2(28, 40), 26, Gold);
    }

    static void StyleButton(Button button, bool featured)
    {
        var img = button.GetComponent<Image>();
        img.color = Color.white;
        button.targetGraphic = img;
        var c = button.colors;
        c.normalColor = featured ? new Color(0.3f, 0.25f, 0.15f, 0.94f) : new Color(0.035f, 0.065f, 0.067f, 0.88f);
        c.highlightedColor = new Color(0.43f, 0.34f, 0.19f, 1);
        c.selectedColor = new Color(0.36f, 0.29f, 0.17f, 1);
        c.pressedColor = new Color(0.22f, 0.17f, 0.09f, 1);
        c.disabledColor = new Color(0.05f, 0.06f, 0.06f, 0.5f);
        c.fadeDuration = 0.18f;
        button.colors = c;
        button.transition = Selectable.Transition.ColorTint;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>()) { label.color = Ivory; label.raycastTarget = false; }
    }

    static void StylePopup(GameObject panel)
    {
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.058f, 0.99f);
        var rect = (RectTransform)panel.transform;
        rect.sizeDelta = new Vector2(1340, 640);
        rect.anchoredPosition = Vector2.zero;
        foreach (var img in panel.GetComponentsInChildren<Image>(true))
            if (img.gameObject != panel && img.GetComponent<Button>() == null)
                img.color = new Color(0.065f, 0.09f, 0.095f, 1);
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = Ivory;
            text.raycastTarget = false;
            if (text.GetComponentInParent<Button>() != null) text.fontSize = 25;
            text.text = text.text.Replace("Sessoes Multiplayer", "Jogar com amigos")
                .Replace("Sessoes encontradas", "Sessões disponíveis")
                .Replace("sessao", "sessão").Replace("sessoes", "sessões");
        }
        foreach (var button in panel.GetComponentsInChildren<Button>(true)) StyleButton(button, false);
        Rule(panel.transform, Vector2.zero, new Vector2(1340, 2), Gold);
    }

    void Update()
    {
        if (menuCanvas == null) return;
        if (worldLibrary == null) worldLibrary = GetComponent<WorldLibraryUI>();
        bool worldDialogOpen = worldLibrary != null && worldLibrary.IsOpen;
        content.interactable = !popup.activeSelf && !worldDialogOpen;
        content.blocksRaycasts = content.interactable;
        float t = Time.unscaledTime - startedAt;
        content.alpha = reducedMotion ? 1 : Mathf.SmoothStep(0, 1, t / 0.9f);
        float motionTime = reducedMotion ? 0 : t;
        Vector2 pointer = Mouse.current != null && !reducedMotion
            ? Mouse.current.position.ReadValue() / new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)) - Vector2.one * 0.5f
            : Vector2.zero;
        artwork.localScale = Vector3.one * (reducedMotion ? 1.025f : 1.035f + Mathf.Sin(t * 0.08f) * 0.008f);
        artwork.anchoredPosition = Vector2.Lerp(artwork.anchoredPosition, pointer * -12f, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3));
        for (int i = 0; i < motes.Count; i++)
        {
            float x = Mathf.Repeat(i * 137.31f + Mathf.Sin(motionTime * 0.17f + i) * 18, 1600) - 800;
            float y = Mathf.Repeat(i * 83.7f + motionTime * (7 + i % 5), 1000) - 500;
            motes[i].anchoredPosition = new Vector2(x, y);
        }
        // Network status belongs to the connection dialog; errors remain visible on the main screen.
        if (status != null) status.enabled = !worldDialogOpen && (popup.activeSelf || status.color.r > 0.95f);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && popup.activeSelf)
        {
            popup.SetActive(false);
            online.Select();
        }
        if (!worldDialogOpen && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && Keyboard.current != null &&
            (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
        {
            if (popup.activeSelf) popup.GetComponentInChildren<Button>().Select(); else primary.Select();
        }
    }

    void LateUpdate()
    {
        if (!GameState.IsInLobby) { RestoreCanvases(); return; }
        if (Time.unscaledTime >= nextCanvasScan)
        {
            nextCanvasScan = Time.unscaledTime + 0.25f;
            HideGameplayCanvases();
        }
        foreach (var pair in hiddenCanvases)
            if (pair.Key != null) pair.Key.enabled = false;
    }

    void HideGameplayCanvases()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == menuCanvas || c.transform.IsChildOf(menuCanvas.transform) || menuCanvas.transform.IsChildOf(c.transform)) continue;
            // Loading owns its own visibility during scene transitions.
            if (c.GetComponentInParent<WorldLoadingScreen>() != null) continue;
            if (!hiddenCanvases.ContainsKey(c)) hiddenCanvases.Add(c, c.enabled);
            c.enabled = false;
        }
    }

    void RestoreCanvases()
    {
        foreach (var pair in hiddenCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
        hiddenCanvases.Clear();
    }
    void OnDisable() { RestoreCanvases(); }
    void OnDestroy() { RestoreCanvases(); if (backgroundTexture != null) Resources.UnloadAsset(backgroundTexture); }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = pos;
        return r;
    }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Pin(RectTransform r, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = pos; r.sizeDelta = size; }
    static TextMeshProUGUI Label(Transform p, string value, Vector2 pos, Vector2 size, float fontSize, Color color, float spacing = 0)
    {
        var r = Rect(value, p, pos, size); Pin(r, pos, size);
        var text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = fontSize; text.color = color; text.characterSpacing = spacing;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    static void Rule(Transform p, Vector2 pos, Vector2 size, Color color)
    {
        var r = Rect("Accent", p, pos, size); Pin(r, pos, size);
        var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
    }
}
