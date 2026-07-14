using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WorldMapOverlay : MonoBehaviour
{
    const int CanvasSortingOrder = 85;
    const float MarkerRefreshInterval = 0.6f;
    const float MapRenderInterval = 0.25f;

    public float mapWorldRadius = 80f;
    public float mapCameraHeight = 180f;
    public int renderTextureSize = 256;
    public bool showWorldMarkers = true;
    public bool showNearbyResourceMarkers = true;
    public bool showBossMarkers = true;
    public int maxVisibleMarkers = 72;
    public bool openOnGameStart = false;

    static WorldMapOverlay instance;

    readonly List<Image> markerPool = new List<Image>();

    InputAction toggleMapAction;
    Canvas canvas;
    RectTransform panelRect;
    RectTransform mapViewportRect;
    RectTransform markerRootRect;
    RectTransform playerIconRect;
    RawImage mapImage;
    Camera mapCamera;
    RenderTexture mapTexture;
    PlayerMovement player;
    Sprite solidSprite;
    Sprite markerSprite;
    Sprite playerArrowSprite;
    bool isOpen;
    bool wantsOpen;
    float nextMarkerRefreshTime;
    float nextMapRenderTime;

    public bool IsOpen => isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (instance != null)
            return;

        WorldMapOverlay existing = FindFirstObjectByType<WorldMapOverlay>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject overlayObject = new GameObject("WorldMapOverlay");
        DontDestroyOnLoad(overlayObject);
        instance = overlayObject.AddComponent<WorldMapOverlay>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        toggleMapAction = new InputAction("ToggleMap", binding: "<Keyboard>/m");
        EnsureSprites();
        EnsureUi();
        EnsureMapCamera();
        wantsOpen = openOnGameStart;
        SetOpen(false);
    }

    void OnEnable()
    {
        toggleMapAction?.Enable();
    }

    void OnDisable()
    {
        toggleMapAction?.Disable();
        if (isOpen)
            SetOpen(false);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        GameState.IsMapOpen = false;
        ReleaseRenderTexture();
    }

    void Update()
    {
        if (toggleMapAction != null && toggleMapAction.WasPressedThisFrame())
        {
            if (isOpen)
            {
                wantsOpen = false;
                SetOpen(false);
            }
            else if (CanOpenMap())
            {
                wantsOpen = true;
                SetOpen(true);
            }
            else
            {
                wantsOpen = false;
            }
        }

        if (wantsOpen && !isOpen && CanOpenMap())
            SetOpen(true);

        if (!isOpen)
            return;

        if (!CanKeepMapOpen())
        {
            SetOpen(false);
            return;
        }

        if (Time.unscaledTime >= nextMarkerRefreshTime)
            RefreshMarkers();
    }

    void LateUpdate()
    {
        if (!isOpen)
            return;

        ResolvePlayer();
        if (player == null)
        {
            SetOpen(false);
            return;
        }

        UpdatePlayerIcon();

        if (Time.unscaledTime < nextMapRenderTime)
            return;

        UpdateMapCamera();
        RenderMapTexture();
        nextMapRenderTime = Time.unscaledTime + MapRenderInterval;
    }

    bool CanOpenMap()
    {
        if (GameState.IsPaused || GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsPowerSelectionOpen || GameState.IsPlayerDead)
            return false;

        ResolvePlayer();
        return player != null;
    }

    bool CanKeepMapOpen()
    {
        return CanOpenMap();
    }

    void SetOpen(bool open)
    {
        if (open && !CanOpenMap())
            return;

        isOpen = open;
        GameState.IsMapOpen = open;

        EnsureUi();
        EnsureMapCamera();

        if (panelRect != null)
            panelRect.gameObject.SetActive(open);

        if (mapCamera != null)
            mapCamera.enabled = false;

        if (open)
        {
            UpdateMapCamera();
            RenderMapTexture();
            UpdatePlayerIcon();
            nextMarkerRefreshTime = 0f;
            nextMapRenderTime = Time.unscaledTime + MapRenderInterval;
            RefreshMarkers();
        }
        else
        {
            HideUnusedMarkers(0);
        }
    }

    void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
            return;

        player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null)
            player = SceneObjectCache.Find<PlayerMovement>(true);
    }

    void EnsureSprites()
    {
        if (solidSprite == null)
            solidSprite = CreateSolidSprite();

        if (markerSprite == null)
            markerSprite = CreateCircleSprite(24, Color.white);

        if (playerArrowSprite == null)
            playerArrowSprite = CreatePlayerArrowSprite();
    }

    void EnsureUi()
    {
        if (canvas != null)
            return;

        EnsureSprites();

        GameObject canvasObject = new GameObject("WorldMapCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = CreateUiObject("MapPanel", canvas.transform);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(390f, 430f);
        panelRect.anchoredPosition = new Vector2(-28f, -28f);
        AddImage(panelObject, new Color(0.035f, 0.03f, 0.025f, 0.58f));

        CreateText("Title", panelRect, "MAPA", 24f, new Color(0.95f, 0.82f, 0.56f, 0.95f), new Vector2(0f, -18f), new Vector2(300f, 34f));

        GameObject frameObject = CreateUiObject("MapFrame", panelRect);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 1f);
        frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.sizeDelta = new Vector2(340f, 320f);
        frameRect.anchoredPosition = new Vector2(0f, -60f);
        AddImage(frameObject, new Color(0.24f, 0.14f, 0.07f, 0.9f));
        AddFrameCorners(frameRect);

        GameObject viewportObject = CreateUiObject("MapViewport", frameRect);
        mapViewportRect = viewportObject.GetComponent<RectTransform>();
        StretchToParent(mapViewportRect, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        viewportObject.AddComponent<RectMask2D>();
        AddImage(viewportObject, new Color(0.02f, 0.025f, 0.02f, 0.9f));

        GameObject rawImageObject = CreateUiObject("MapRender", mapViewportRect);
        RectTransform rawImageRect = rawImageObject.GetComponent<RectTransform>();
        StretchToParent(rawImageRect, Vector2.zero, Vector2.zero);
        mapImage = rawImageObject.AddComponent<RawImage>();
        mapImage.color = new Color(1f, 1f, 1f, 0.86f);
        mapImage.raycastTarget = false;

        GameObject markerRootObject = CreateUiObject("MarkerRoot", mapViewportRect);
        markerRootRect = markerRootObject.GetComponent<RectTransform>();
        StretchToParent(markerRootRect, Vector2.zero, Vector2.zero);

        playerIconRect = CreateIcon("PlayerIcon", mapViewportRect, playerArrowSprite, new Color(0.95f, 0.95f, 0.9f, 1f), 32f);
        playerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        playerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        playerIconRect.anchoredPosition = Vector2.zero;
        playerIconRect.SetAsLastSibling();

        CreateText("NorthLabel", mapViewportRect, "N", 18f, new Color(0.94f, 0.86f, 0.65f, 0.9f), new Vector2(0f, -16f), new Vector2(44f, 24f), TextAlignmentOptions.Center, true);
        CreateText("CloseHint", panelRect, "M para fechar", 18f, new Color(0.92f, 0.86f, 0.74f, 0.92f), new Vector2(0f, 24f), new Vector2(260f, 30f), TextAlignmentOptions.Center, false);

        panelRect.gameObject.SetActive(false);
    }

    void EnsureMapCamera()
    {
        EnsureRenderTexture();

        if (mapCamera == null)
        {
            GameObject cameraObject = new GameObject("WorldMapCamera");
            cameraObject.transform.SetParent(transform, false);
            mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.04f, 0.05f, 0.035f, 1f);
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;
            mapCamera.depth = -100f;
            mapCamera.enabled = false;

            AudioListener listener = cameraObject.GetComponent<AudioListener>();
            if (listener != null)
                Destroy(listener);

            int cullingMask = ~0;
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                cullingMask &= ~(1 << uiLayer);

            mapCamera.cullingMask = cullingMask;
        }

        mapCamera.targetTexture = mapTexture;
        mapCamera.enabled = false;
    }

    void EnsureRenderTexture()
    {
        int safeSize = Mathf.Clamp(renderTextureSize, 128, 2048);
        if (mapTexture != null && mapTexture.width == safeSize && mapTexture.height == safeSize)
            return;

        ReleaseRenderTexture();

        mapTexture = new RenderTexture(safeSize, safeSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "WorldMapRenderTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        mapTexture.Create();

        if (mapImage != null)
            mapImage.texture = mapTexture;
    }

    void ReleaseRenderTexture()
    {
        if (mapCamera != null)
            mapCamera.targetTexture = null;

        if (mapTexture == null)
            return;

        mapTexture.Release();
        Destroy(mapTexture);
        mapTexture = null;
    }

    void UpdateMapCamera()
    {
        if (mapCamera == null || player == null)
            return;

        float cameraHeight = Mathf.Max(60f, mapCameraHeight);
        mapCamera.transform.position = player.transform.position + Vector3.up * cameraHeight;
        mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mapCamera.orthographicSize = Mathf.Max(24f, mapWorldRadius);
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = cameraHeight + 260f;
    }

    void RenderMapTexture()
    {
        if (mapCamera == null || mapTexture == null)
            return;

        if (mapCamera.targetTexture != mapTexture)
            mapCamera.targetTexture = mapTexture;

        mapCamera.enabled = false;
        mapCamera.Render();
    }

    void UpdatePlayerIcon()
    {
        if (playerIconRect == null || player == null)
            return;

        playerIconRect.anchoredPosition = Vector2.zero;
        playerIconRect.localRotation = Quaternion.Euler(0f, 0f, -player.transform.eulerAngles.y);
    }

    void RefreshMarkers()
    {
        nextMarkerRefreshTime = Time.unscaledTime + MarkerRefreshInterval;

        if (!isOpen || player == null || markerRootRect == null)
            return;

        int usedMarkers = 0;
        int markerBudget = Mathf.Max(0, maxVisibleMarkers);

        if (showWorldMarkers)
            AddWorldMapMarkers(ref usedMarkers, markerBudget);

        if (showBossMarkers)
            AddBossMarkers(ref usedMarkers, markerBudget);

        if (showNearbyResourceMarkers)
            AddResourceMarkers(ref usedMarkers, markerBudget);

        HideUnusedMarkers(usedMarkers);

        if (playerIconRect != null)
            playerIconRect.SetAsLastSibling();
    }

    void AddWorldMapMarkers(ref int usedMarkers, int markerBudget)
    {
        IReadOnlyList<WorldMapMarker> markers = WorldMapMarker.ActiveMarkers;
        for (int i = 0; i < markers.Count && usedMarkers < markerBudget; i++)
        {
            WorldMapMarker marker = markers[i];
            if (marker == null || !marker.showOnMap)
                continue;

            TryPlaceMarker(marker.WorldPosition, marker.markerColor, marker.markerSize, ref usedMarkers);
        }
    }

    void AddBossMarkers(ref int usedMarkers, int markerBudget)
    {
        IReadOnlyList<BossEnemy> bosses = BossEnemy.ActiveBosses;
        for (int i = 0; i < bosses.Count && usedMarkers < markerBudget; i++)
        {
            BossEnemy boss = bosses[i];
            if (boss == null || boss.IsPendingDestroy || !boss.gameObject.activeInHierarchy)
                continue;

            TryPlaceMarker(boss.transform.position, new Color(0.92f, 0.18f, 0.12f, 0.95f), 13f, ref usedMarkers);
        }
    }

    void AddResourceMarkers(ref int usedMarkers, int markerBudget)
    {
        IReadOnlyList<ResourceNode> resources = ResourceNode.ActiveNodes;
        for (int i = 0; i < resources.Count && usedMarkers < markerBudget; i++)
        {
            ResourceNode resource = resources[i];
            if (resource == null || resource.IsDepleted || !resource.gameObject.activeInHierarchy)
                continue;

            TryPlaceMarker(resource.transform.position, GetResourceMarkerColor(resource), 7f, ref usedMarkers);
        }
    }

    bool TryPlaceMarker(Vector3 worldPosition, Color color, float size, ref int usedMarkers)
    {
        if (player == null || markerRootRect == null)
            return false;

        Vector3 delta = worldPosition - player.transform.position;
        float halfHeight = Mathf.Max(1f, mapWorldRadius);
        float halfWidth = halfHeight;

        float x = delta.x / halfWidth;
        float y = delta.z / halfHeight;
        if (Mathf.Abs(x) > 1f || Mathf.Abs(y) > 1f)
            return false;

        Vector2 mapSize = markerRootRect.rect.size;
        if (mapSize.x <= 1f || mapSize.y <= 1f)
            mapSize = new Vector2(316f, 296f);

        Image markerImage = GetMarkerImage(usedMarkers);
        RectTransform markerRect = markerImage.rectTransform;
        markerRect.anchoredPosition = new Vector2(x * mapSize.x * 0.5f, y * mapSize.y * 0.5f);
        markerRect.sizeDelta = Vector2.one * Mathf.Clamp(size, 4f, 20f);
        markerImage.color = color;
        markerImage.gameObject.SetActive(true);
        usedMarkers++;
        return true;
    }

    Image GetMarkerImage(int index)
    {
        while (markerPool.Count <= index)
        {
            RectTransform markerRect = CreateIcon($"Marker_{markerPool.Count + 1}", markerRootRect, markerSprite, Color.white, 8f);
            Image image = markerRect.GetComponent<Image>();
            markerPool.Add(image);
        }

        return markerPool[index];
    }

    void HideUnusedMarkers(int usedMarkers)
    {
        for (int i = usedMarkers; i < markerPool.Count; i++)
        {
            if (markerPool[i] != null)
                markerPool[i].gameObject.SetActive(false);
        }
    }

    Color GetResourceMarkerColor(ResourceNode resource)
    {
        string itemName = resource.itemName ?? string.Empty;
        string objectName = resource.gameObject.name ?? string.Empty;

        if (ContainsAny(itemName, objectName, "iron", "ferro"))
            return new Color(0.95f, 0.55f, 0.22f, 0.95f);

        if (resource.requiredTool == ToolType.Pickaxe || ContainsAny(itemName, objectName, "rock", "stone", "pedra"))
            return new Color(0.72f, 0.72f, 0.68f, 0.9f);

        if (ContainsAny(itemName, objectName, "mushroom", "cogumelo"))
            return new Color(0.78f, 0.42f, 0.95f, 0.9f);

        if (ContainsAny(itemName, objectName, "wheat", "trigo"))
            return new Color(0.96f, 0.72f, 0.22f, 0.92f);

        if (resource.requiredTool == ToolType.Axe || ContainsAny(itemName, objectName, "wood", "tree", "tora", "madeira", "arvore"))
            return new Color(0.32f, 0.82f, 0.34f, 0.9f);

        return new Color(0.95f, 0.84f, 0.52f, 0.88f);
    }

    bool ContainsAny(string a, string b, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (a.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                b.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    Image AddImage(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = solidSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    RectTransform CreateIcon(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        GameObject iconObject = CreateUiObject(name, parent);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = Vector2.zero;

        Image image = iconObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, Color color, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool anchorTop = true)
    {
        GameObject textObject = CreateUiObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.enableAutoSizing = true;
        textComponent.fontSizeMin = Mathf.Max(10f, fontSize * 0.65f);
        textComponent.fontSizeMax = fontSize;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    void AddFrameCorners(RectTransform parent)
    {
        AddCorner(parent, "CornerTL", new Vector2(0f, 1f), new Vector2(0f, 1f));
        AddCorner(parent, "CornerTR", new Vector2(1f, 1f), new Vector2(1f, 1f));
        AddCorner(parent, "CornerBL", new Vector2(0f, 0f), new Vector2(0f, 0f));
        AddCorner(parent, "CornerBR", new Vector2(1f, 0f), new Vector2(1f, 0f));
    }

    void AddCorner(RectTransform parent, string name, Vector2 anchor, Vector2 pivot)
    {
        GameObject cornerObject = CreateUiObject(name, parent);
        RectTransform rect = cornerObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(28f, 28f);
        rect.anchoredPosition = Vector2.zero;
        AddImage(cornerObject, new Color(0.38f, 0.23f, 0.1f, 0.95f));
    }

    void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "WorldMapSolidSprite";
        texture.filterMode = FilterMode.Point;
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
    }

    Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "WorldMapCircleSprite";
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        float radius = size * 0.42f;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, distance <= radius ? color : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Sprite CreatePlayerArrowSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "WorldMapPlayerArrowSprite";
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inNeedle = y >= 22 && y <= 58 && Mathf.Abs(x - center.x) <= Mathf.Lerp(17f, 2f, (y - 22f) / 36f);
                bool inTail = y >= 8 && y < 28 && Mathf.Abs(x - center.x) <= 6f;
                bool inCenter = Vector2.Distance(new Vector2(x, y), center) <= 10f;
                texture.SetPixel(x, y, inNeedle || inTail || inCenter ? fill : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
