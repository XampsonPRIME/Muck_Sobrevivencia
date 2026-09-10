using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PowerSelectionCave : MonoBehaviour
{
    static PowerSelectionCave instance;

    const string CaveLayerName = "PowerSelection";
    const int CaveLayerFallback = 31;
    const float CaveHeight = 240f;
    const float CaveFloorCenterY = 0f;
    const float CaveFloorThickness = 1f;
    const float CaveSpawnZ = -7.1f;
    const float CaveSpawnClearance = 0.15f;
    const float CaveFallRecoveryDepth = 2f;

    PlayerMovement player;
    BearerPowerService powerService;
    GameObject caveRoot;
    Canvas introCanvas;
    TMP_Text availabilityText;
    Camera caveCamera;
    Vector3 caveOrigin;
    Vector3 adventureSpawn;
    Quaternion adventureRotation;
    bool logCaveTransitions = false;
    int previousCameraCullingMask;
    CameraClearFlags previousCameraClearFlags;
    Color previousCameraBackgroundColor;
    bool hasAdventureSpawn;
    bool playerEntered;
    bool transitionRunning;
    bool caveCameraConfigured;
    bool selectionResolvedThisSession;
    float caveRecoveryCooldownUntil;
    string resolvedPowerId;
    readonly List<PowerSelectionPedestal> pedestals = new List<PowerSelectionPedestal>();
    readonly List<Camera> disabledGameplayCameras = new List<Camera>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (instance != null || FindFirstObjectByType<PowerSelectionCave>(FindObjectsInactive.Include) != null)
            return;

        GameObject caveObject = new GameObject("PowerSelectionCave");
        DontDestroyOnLoad(caveObject);
        caveObject.AddComponent<PowerSelectionCave>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (instance != this)
            return;

        BearerPowerService.PowerChanged += HandlePowerChanged;
        LanMultiplayerManager.BearerPowerAvailabilityChanged += RefreshPedestals;
    }

    void OnDisable()
    {
        BearerPowerService.PowerChanged -= HandlePowerChanged;
        LanMultiplayerManager.BearerPowerAvailabilityChanged -= RefreshPedestals;
        RestoreAdventureCamera();
        player?.SetVerticalPositionLock(false);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        ResolvePlayer();
        if (player == null || powerService == null)
            return;

        RestoreResolvedSelectionIfNeeded();

        if (selectionResolvedThisSession || powerService.HasCompletedSelection)
        {
            if (playerEntered && !transitionRunning)
                StartCoroutine(LeaveCaveRoutine());

            return;
        }

        if (!CanEnterSelection())
            return;

        EnsureCave();

        if (!playerEntered)
            EnterCave();

        RecoverPlayerIfBelowCave();
        RefreshAvailabilityText();
    }

    void LateUpdate()
    {
        if (!playerEntered || transitionRunning || player == null)
            return;

        ApplyCaveCameraSettings();
        player.SetVerticalPositionLock(true, GetCaveSpawnPosition().y);
    }

    void ResolvePlayer()
    {
        PlayerMovement activePlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (activePlayer != null && activePlayer != player)
        {
            player?.SetVerticalPositionLock(false);
            player = activePlayer;
            powerService = null;
        }

        if (player == null)
            return;

        if (powerService == null)
            powerService = player.GetComponent<BearerPowerService>() ?? player.gameObject.AddComponent<BearerPowerService>();
    }

    void RestoreResolvedSelectionIfNeeded()
    {
        if (!selectionResolvedThisSession ||
            powerService == null ||
            powerService.HasCompletedSelection ||
            BearerPowerCatalog.Find(resolvedPowerId) == null)
        {
            return;
        }

        powerService.LoadState(resolvedPowerId, true);
    }

    bool CanEnterSelection()
    {
        if (GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsPaused || GameState.IsPlayerDead)
            return false;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        return manager == null ||
               manager.Mode == LanMultiplayerManager.SessionMode.None ||
               manager.Mode == LanMultiplayerManager.SessionMode.Solo ||
               manager.IsSessionReady;
    }

    void EnsureCave()
    {
        if (caveRoot != null)
            return;

        caveRoot = new GameObject("CavernaDosPortadores");
        DontDestroyOnLoad(caveRoot);
        caveOrigin = GetCaveOrigin(player != null ? player.transform.position : Vector3.zero);
        caveRoot.transform.position = caveOrigin;

        Material rock = CreateMaterial("CaveRock", new Color(0.105f, 0.09f, 0.085f, 1f));
        Material darkRock = CreateMaterial("CaveDarkRock", new Color(0.045f, 0.038f, 0.045f, 1f));
        Material rune = CreateMaterial("CaveRune", new Color(0.12f, 0.56f, 0.62f, 1f), true);

        CreatePart(
            "Floor",
            PrimitiveType.Cube,
            caveRoot.transform,
            new Vector3(0f, CaveFloorCenterY, 0f),
            new Vector3(28f, CaveFloorThickness, 22f),
            rock);
        CreatePart("Ceiling", PrimitiveType.Cube, caveRoot.transform, new Vector3(0f, 8f, 0f), new Vector3(28f, 1f, 22f), darkRock);
        CreatePart("BackWall", PrimitiveType.Cube, caveRoot.transform, new Vector3(0f, 4f, 10.5f), new Vector3(28f, 8f, 1f), rock);
        CreatePart("FrontWall", PrimitiveType.Cube, caveRoot.transform, new Vector3(0f, 4f, -10.5f), new Vector3(28f, 8f, 1f), darkRock);
        CreatePart("LeftWall", PrimitiveType.Cube, caveRoot.transform, new Vector3(-13.5f, 4f, 0f), new Vector3(1f, 8f, 22f), rock);
        CreatePart("RightWall", PrimitiveType.Cube, caveRoot.transform, new Vector3(13.5f, 4f, 0f), new Vector3(1f, 8f, 22f), rock);

        CreateRockRing(rock, darkRock);
        CreateCentralRunes(rune);
        CreateLighting();
        CreateNpc(rock, rune);
        CreatePedestals(rock);
        BuildIntroUi();
        SetLayerRecursively(caveRoot, ResolveCaveLayer());
        RefreshPedestals();
    }

    void CreateRockRing(Material rock, Material darkRock)
    {
        Random.State previousState = Random.state;
        Random.InitState(17421);

        for (int i = 0; i < 34; i++)
        {
            float angle = i / 34f * Mathf.PI * 2f;
            float x = Mathf.Sin(angle) * Random.Range(11.6f, 13.1f);
            float z = Mathf.Cos(angle) * Random.Range(8.7f, 10.1f);
            float y = Random.Range(0.6f, 6.8f);
            Vector3 scale = new Vector3(Random.Range(1.1f, 2.8f), Random.Range(1.1f, 3.2f), Random.Range(1.1f, 2.6f));
            GameObject rockPart = CreatePart(
                $"NaturalRock_{i:00}",
                PrimitiveType.Sphere,
                caveRoot.transform,
                new Vector3(x, y, z),
                scale,
                i % 3 == 0 ? darkRock : rock);
            rockPart.transform.localRotation = Random.rotation;
        }

        Random.state = previousState;
    }

    void CreateCentralRunes(Material rune)
    {
        for (int i = 0; i < 9; i++)
        {
            float angle = i / 9f * Mathf.PI * 2f;
            Vector3 position = new Vector3(Mathf.Sin(angle) * 5.8f, 0.58f, Mathf.Cos(angle) * 4.2f);
            GameObject mark = CreatePart($"FloorRune_{i}", PrimitiveType.Cube, caveRoot.transform, position, new Vector3(0.12f, 0.04f, 1.3f), rune);
            mark.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            Collider markCollider = mark.GetComponent<Collider>();
            if (markCollider != null)
                Destroy(markCollider);
        }
    }

    void CreateLighting()
    {
        CreatePointLight("CaveLightCenter", new Vector3(0f, 5.7f, 0f), new Color(0.43f, 0.65f, 0.72f), 2.3f, 22f);
        CreatePointLight("CaveLightLeft", new Vector3(-9f, 2.6f, 4.5f), new Color(0.85f, 0.34f, 0.1f), 2f, 10f);
        CreatePointLight("CaveLightRight", new Vector3(9f, 2.6f, 4.5f), new Color(0.25f, 0.18f, 0.9f), 2f, 10f);
    }

    void CreatePointLight(string lightName, Vector3 localPosition, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(caveRoot.transform, false);
        lightObject.transform.localPosition = localPosition;
        Light point = lightObject.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = color;
        point.intensity = intensity;
        point.range = range;
        point.shadows = LightShadows.None;
    }

    void CreateNpc(Material rock, Material rune)
    {
        GameObject npc = new GameObject("VigiaSemNome");
        npc.transform.SetParent(caveRoot.transform, false);
        npc.transform.localPosition = new Vector3(0f, 0.55f, -2.2f);
        npc.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        CreatePart("NpcBody", PrimitiveType.Capsule, npc.transform, new Vector3(0f, 1f, 0f), new Vector3(0.85f, 1.35f, 0.85f), rock);
        CreatePart("NpcHead", PrimitiveType.Sphere, npc.transform, new Vector3(0f, 2.55f, 0f), new Vector3(0.62f, 0.62f, 0.62f), rock);
        CreatePart("NpcEyeL", PrimitiveType.Sphere, npc.transform, new Vector3(-0.18f, 2.63f, 0.5f), new Vector3(0.09f, 0.07f, 0.06f), rune);
        CreatePart("NpcEyeR", PrimitiveType.Sphere, npc.transform, new Vector3(0.18f, 2.63f, 0.5f), new Vector3(0.09f, 0.07f, 0.06f), rune);

        CapsuleCollider interactionCollider = npc.AddComponent<CapsuleCollider>();
        interactionCollider.center = new Vector3(0f, 1.4f, 0f);
        interactionCollider.height = 3.4f;
        interactionCollider.radius = 0.8f;
        npc.AddComponent<PowerSelectionNpc>();
        CreateWorldLabel(npc.transform, "O VIGIA SEM NOME", new Vector3(0f, 3.45f, 0f), new Color(0.76f, 0.91f, 0.95f, 1f), 2.5f);
    }

    void CreatePedestals(Material rock)
    {
        float[] xPositions = { -9f, -3f, 3f, 9f };
        IReadOnlyList<BearerPowerDefinition> definitions = BearerPowerCatalog.All;

        for (int i = 0; i < definitions.Count && i < xPositions.Length; i++)
        {
            BearerPowerDefinition definition = definitions[i];
            GameObject pedestalObject = new GameObject($"Altar_{definition.stableId}");
            pedestalObject.transform.SetParent(caveRoot.transform, false);
            pedestalObject.transform.localPosition = new Vector3(xPositions[i], 0.55f, 4.9f);
            pedestalObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            CreatePart("Base", PrimitiveType.Cylinder, pedestalObject.transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.15f, 0.35f, 1.15f), rock);
            CreatePart("Column", PrimitiveType.Cylinder, pedestalObject.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.7f, 0.85f, 0.7f), rock);

            GameObject crystal = CreatePart(
                "PowerCrystal",
                PrimitiveType.Cube,
                pedestalObject.transform,
                new Vector3(0f, 2.25f, 0f),
                new Vector3(0.72f, 1.15f, 0.72f),
                CreateMaterial($"{definition.stableId}Crystal", definition.primaryColor, true));

            BoxCollider interactionCollider = pedestalObject.AddComponent<BoxCollider>();
            interactionCollider.center = new Vector3(0f, 1.45f, 0f);
            interactionCollider.size = new Vector3(2.4f, 3.2f, 2.4f);

            TMP_Text label = CreateWorldLabel(
                pedestalObject.transform,
                definition.displayName.ToUpperInvariant(),
                new Vector3(0f, 3.25f, 0f),
                definition.secondaryColor,
                1.85f);

            PowerSelectionPedestal pedestal = pedestalObject.AddComponent<PowerSelectionPedestal>();
            pedestal.Configure(definition.stableId, crystal.transform, label);
            pedestals.Add(pedestal);
        }
    }

    void BuildIntroUi()
    {
        GameObject canvasObject = new GameObject("PowerSelectionIntroUI");
        canvasObject.transform.SetParent(transform, false);
        introCanvas = canvasObject.AddComponent<Canvas>();
        introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        introCanvas.sortingOrder = 2500;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        DisplaySettingsManager.ConfigureCanvasScaler(scaler);

        Image vignette = CreateUiImage("Vignette", canvasObject.transform, new Color(0f, 0f, 0f, 0.18f));
        vignette.rectTransform.anchorMin = Vector2.zero;
        vignette.rectTransform.anchorMax = Vector2.one;
        vignette.rectTransform.offsetMin = Vector2.zero;
        vignette.rectTransform.offsetMax = Vector2.zero;
        vignette.raycastTarget = false;

        TMP_Text title = CreateUiText("Title", canvasObject.transform, "A CAVERNA DOS PORTADORES", 36, FontStyles.Bold);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -42f);
        title.rectTransform.sizeDelta = new Vector2(980f, 54f);
        title.color = new Color(0.84f, 0.91f, 0.9f, 1f);

        TMP_Text subtitle = CreateUiText("Subtitle", canvasObject.transform, "Aproxime-se de um legado e pressione E para absorve-lo", 20, FontStyles.Italic);
        subtitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -92f);
        subtitle.rectTransform.sizeDelta = new Vector2(980f, 38f);
        subtitle.color = new Color(0.68f, 0.78f, 0.78f, 1f);

        availabilityText = CreateUiText("Availability", canvasObject.transform, string.Empty, 17, FontStyles.Normal);
        availabilityText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        availabilityText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        availabilityText.rectTransform.pivot = new Vector2(0.5f, 0f);
        availabilityText.rectTransform.anchoredPosition = new Vector2(0f, 34f);
        availabilityText.rectTransform.sizeDelta = new Vector2(980f, 36f);
        availabilityText.color = new Color(0.82f, 0.78f, 0.64f, 1f);
    }

    void EnterCave()
    {
        if (!hasAdventureSpawn)
        {
            adventureSpawn = player.transform.position;
            adventureRotation = player.transform.rotation;
            player.TryGetSafeSpawnPosition(adventureSpawn, out adventureSpawn);
            hasAdventureSpawn = true;
        }

        caveRoot.SetActive(true);
        caveOrigin = GetCaveOrigin(adventureSpawn);
        caveRoot.transform.position = caveOrigin;
        SetLayerRecursively(caveRoot, ResolveCaveLayer());
        if (introCanvas != null)
            introCanvas.enabled = true;

        ConfigureCaveCamera();
        PlacePlayerAtCaveSpawn();
        playerEntered = true;
        caveRecoveryCooldownUntil = Time.unscaledTime + 0.5f;
        GameState.IsPowerSelectionOpen = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        MessageSystem.Instance?.ShowMessage("O Vigia observa em silencio. Os legados aguardam.");
    }

    static Vector3 GetCaveOrigin(Vector3 referencePosition)
    {
        return new Vector3(referencePosition.x, CaveHeight, referencePosition.z);
    }

    int ResolveCaveLayer()
    {
        int layer = LayerMask.NameToLayer(CaveLayerName);
        return layer >= 0 ? layer : CaveLayerFallback;
    }

    void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        Transform targetTransform = target.transform;
        for (int i = 0; i < targetTransform.childCount; i++)
            SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
    }

    void ConfigureCaveCamera()
    {
        Camera activeCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        if (activeCamera == null)
            activeCamera = RuntimeCameraCache.Main;

        if (activeCamera == null)
        {
            Debug.LogWarning("Caverna dos Portadores nao encontrou a camera do jogador.");
            return;
        }

        if (!caveCameraConfigured)
        {
            caveCamera = activeCamera;
            previousCameraCullingMask = caveCamera.cullingMask;
            previousCameraClearFlags = caveCamera.clearFlags;
            previousCameraBackgroundColor = caveCamera.backgroundColor;
            caveCameraConfigured = true;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null ||
                candidate == caveCamera ||
                !candidate.enabled ||
                candidate.targetTexture != null ||
                disabledGameplayCameras.Contains(candidate))
            {
                continue;
            }

            disabledGameplayCameras.Add(candidate);
            candidate.enabled = false;
        }

        ApplyCaveCameraSettings();
    }

    void ApplyCaveCameraSettings()
    {
        if (!caveCameraConfigured || caveCamera == null)
            return;

        caveCamera.cullingMask = 1 << ResolveCaveLayer();
        caveCamera.clearFlags = CameraClearFlags.SolidColor;
        caveCamera.backgroundColor = new Color(0.012f, 0.009f, 0.018f, 1f);
    }

    void RestoreAdventureCamera()
    {
        if (!caveCameraConfigured)
            return;

        if (caveCamera != null)
        {
            caveCamera.cullingMask = previousCameraCullingMask;
            caveCamera.clearFlags = previousCameraClearFlags;
            caveCamera.backgroundColor = previousCameraBackgroundColor;
        }

        for (int i = 0; i < disabledGameplayCameras.Count; i++)
        {
            if (disabledGameplayCameras[i] != null)
                disabledGameplayCameras[i].enabled = true;
        }

        disabledGameplayCameras.Clear();
        caveCamera = null;
        caveCameraConfigured = false;
    }

    void PlacePlayerAtCaveSpawn()
    {
        if (player == null)
            return;

        // The cave and its colliders are created in this same frame.
        Physics.SyncTransforms();
        Vector3 caveSpawn = GetCaveSpawnPosition();
        player.TeleportExact(caveSpawn, Quaternion.LookRotation(Vector3.forward, Vector3.up), false);
        player.SetVerticalPositionLock(true, caveSpawn.y);
        Physics.SyncTransforms();

        if (logCaveTransitions)
        {
            Transform floor = caveRoot != null ? caveRoot.transform.Find("Floor") : null;
            Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
            Camera activeCamera = caveCamera != null ? caveCamera : RuntimeCameraCache.Main;
            Debug.Log(
                $"Caverna dos Portadores pronta. Player={player.transform.position}, " +
                $"Camera={(activeCamera != null ? $"{activeCamera.name} {activeCamera.transform.position}" : "ausente")}, " +
                $"Piso={(floorRenderer != null ? floorRenderer.bounds.ToString() : "ausente")}, " +
                $"Layer={ResolveCaveLayer()}.");
        }
    }

    Vector3 GetCaveSpawnPosition()
    {
        float lowestPlayerPoint = -1f;
        CharacterController characterController = player != null ? player.GetComponent<CharacterController>() : null;
        if (characterController != null)
        {
            lowestPlayerPoint = Mathf.Min(
                lowestPlayerPoint,
                characterController.center.y - characterController.height * 0.5f);
        }

        CapsuleCollider[] capsules = player != null ? player.GetComponents<CapsuleCollider>() : null;
        if (capsules != null)
        {
            for (int i = 0; i < capsules.Length; i++)
            {
                CapsuleCollider capsule = capsules[i];
                if (capsule == null || !capsule.enabled || capsule.isTrigger)
                    continue;

                float verticalExtent = capsule.direction == 1 ? capsule.height * 0.5f : capsule.radius;
                lowestPlayerPoint = Mathf.Min(lowestPlayerPoint, capsule.center.y - verticalExtent);
            }
        }

        float floorTop = caveOrigin.y + CaveFloorCenterY + CaveFloorThickness * 0.5f;
        float playerY = floorTop - lowestPlayerPoint + CaveSpawnClearance;
        return new Vector3(caveOrigin.x, playerY, caveOrigin.z + CaveSpawnZ);
    }

    void RecoverPlayerIfBelowCave()
    {
        if (!playerEntered || player == null || Time.unscaledTime < caveRecoveryCooldownUntil)
            return;

        float floorTop = caveOrigin.y + CaveFloorCenterY + CaveFloorThickness * 0.5f;
        if (player.transform.position.y >= floorTop - CaveFallRecoveryDepth)
            return;

        caveRecoveryCooldownUntil = Time.unscaledTime + 0.5f;
        Debug.LogWarning("Jogador ficou abaixo do piso da Caverna dos Portadores. Reposicionando.");
        PlacePlayerAtCaveSpawn();
    }

    void HandlePowerChanged(BearerPowerService changedService)
    {
        if (changedService != powerService)
            return;

        if (!changedService.HasCompletedSelection)
        {
            if (!playerEntered)
                hasAdventureSpawn = false;

            transitionRunning = false;
            GameState.IsPowerSelectionOpen = playerEntered;
            RefreshPedestals();
            return;
        }

        resolvedPowerId = changedService.CurrentPowerId;
        selectionResolvedThisSession = BearerPowerCatalog.Find(resolvedPowerId) != null;

        if (!playerEntered || transitionRunning)
            return;

        StartCoroutine(LeaveCaveRoutine());
    }

    IEnumerator LeaveCaveRoutine()
    {
        if (transitionRunning)
            yield break;

        transitionRunning = true;
        BearerPowerDefinition definition = powerService != null ? powerService.CurrentDefinition : null;
        if (definition != null)
        {
            resolvedPowerId = definition.stableId;
            selectionResolvedThisSession = true;
            MessageSystem.Instance?.ShowMessage($"Voce agora e o {definition.bearerTitle}.");
        }

        yield return new WaitForSecondsRealtime(1.1f);

        if (introCanvas != null)
            introCanvas.enabled = false;

        // Remove the cave colliders before resolving the return position. Otherwise
        // the safe-spawn fallback can select the cave floor and leave the player in the sky.
        if (caveRoot != null)
            caveRoot.SetActive(false);

        Physics.SyncTransforms();

        bool returnedToAdventure = false;
        if (hasAdventureSpawn)
        {
            returnedToAdventure = player.WarpToSafePosition(adventureSpawn, adventureRotation);
            if (!returnedToAdventure)
            {
                player.TeleportExact(adventureSpawn, adventureRotation);
                returnedToAdventure = true;
            }
        }

        player.SetVerticalPositionLock(false);
        RestoreAdventureCamera();
        GameState.IsPowerSelectionOpen = false;

        playerEntered = false;
        transitionRunning = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (logCaveTransitions)
        {
            Debug.Log(
                $"Saida da Caverna dos Portadores. Retornou={returnedToAdventure}, " +
                $"Destino={adventureSpawn}, Player={player.transform.position}, Camera=" +
                $"{(RuntimeCameraCache.Main != null ? RuntimeCameraCache.Main.transform.position.ToString() : "ausente")}, " +
                $"Poder={resolvedPowerId}.");
        }
        MessageSystem.Instance?.ShowMessage("Sua aventura comeca agora.");
        SaveSelectionProgress();
    }

    void SaveSelectionProgress()
    {
        RestoreResolvedSelectionIfNeeded();

        SaveGameManager saveManager = SaveGameManager.Instance;
        if (saveManager == null)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.Mode == LanMultiplayerManager.SessionMode.Host)
            saveManager.SaveMultiplayerSession(false);
        else if (manager != null && manager.Mode == LanMultiplayerManager.SessionMode.Client)
            saveManager.SaveClientSession(false);
        else
            saveManager.SaveGame(false);
    }

    void RefreshPedestals()
    {
        for (int i = 0; i < pedestals.Count; i++)
            pedestals[i]?.RefreshAvailability();

        RefreshAvailabilityText();
    }

    void RefreshAvailabilityText()
    {
        if (availabilityText == null)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        int available = 0;
        IReadOnlyList<BearerPowerDefinition> definitions = BearerPowerCatalog.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            if (manager == null || !manager.IsBearerPowerClaimed(definitions[i].stableId))
                available++;
        }

        availabilityText.text = $"{available} de {definitions.Count} legados ainda respondem neste mundo";
    }

    GameObject CreatePart(string partName, PrimitiveType primitive, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        return part;
    }

    Material CreateMaterial(string materialName, Color color, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);

        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.2f);
        }

        return material;
    }

    TMP_Text CreateWorldLabel(Transform parent, string value, Vector3 localPosition, Color color, float fontSize)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        TMP_Text label = labelObject.AddComponent<TextMeshPro>();
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        label.rectTransform.sizeDelta = new Vector2(7f, 1.2f);
        labelObject.AddComponent<WorldFacingLabel>();
        return label;
    }

    TMP_Text CreateUiText(string objectName, Transform parent, string value, int fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    Image CreateUiImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }
}

public class PowerSelectionPedestal : MonoBehaviour, IPlayerInteractable
{
    string powerId;
    Transform crystal;
    TMP_Text label;
    Vector3 crystalStartPosition;
    Color availableColor;
    bool available = true;

    public void Configure(string configuredPowerId, Transform crystalTransform, TMP_Text worldLabel)
    {
        powerId = configuredPowerId;
        crystal = crystalTransform;
        label = worldLabel;
        crystalStartPosition = crystal != null ? crystal.localPosition : Vector3.zero;
        BearerPowerDefinition definition = BearerPowerCatalog.Find(powerId);
        availableColor = definition != null ? definition.secondaryColor : Color.white;
        RefreshAvailability();
    }

    void Update()
    {
        if (crystal == null)
            return;

        crystal.localPosition = crystalStartPosition + Vector3.up * (Mathf.Sin(Time.time * 1.8f + transform.position.x) * 0.16f);
        crystal.Rotate(0f, 34f * Time.deltaTime, 0f, Space.Self);
    }

    public bool Interact(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null)
            return false;

        BearerPowerDefinition definition = BearerPowerCatalog.Find(powerId);
        if (definition == null)
            return true;

        RefreshAvailability();
        if (!available)
        {
            MessageSystem.Instance?.ShowMessage($"{definition.displayName} ja escolheu outro portador.");
            return true;
        }

        PlayerMovement targetPlayer = playerInteraction.GetComponent<PlayerMovement>() ??
                                      playerInteraction.GetComponentInParent<PlayerMovement>();
        GameObject serviceOwner = targetPlayer != null ? targetPlayer.gameObject : playerInteraction.gameObject;
        BearerPowerService service = serviceOwner.GetComponent<BearerPowerService>() ??
                                     serviceOwner.AddComponent<BearerPowerService>();
        service.RequestPower(powerId);
        return true;
    }

    public void RefreshAvailability()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        available = manager == null || !manager.IsBearerPowerClaimed(powerId);

        BearerPowerDefinition definition = BearerPowerCatalog.Find(powerId);
        if (label != null && definition != null)
        {
            label.text = available
                ? definition.displayName.ToUpperInvariant()
                : $"{definition.displayName.ToUpperInvariant()}\n<color=#888888>REIVINDICADO</color>";
            label.color = available ? availableColor : new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        if (crystal != null)
        {
            Renderer renderer = crystal.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = available;
        }
    }
}

public class PowerSelectionNpc : MonoBehaviour, IPlayerInteractable
{
    static readonly string[] Dialogue =
    {
        "Nao sao presentes. Sao promessas antigas procurando um corpo.",
        "Cada mundo permite apenas um portador para cada legado.",
        "Quem absorve um legado pode negocia-lo... ou perde-lo pela violencia.",
        "Os Guardioes conhecem a origem desses poderes. E temem o que eles anunciam.",
        "Escolha. Depois que o legado responder, o sobrevivente que entrou aqui deixara de existir."
    };

    int dialogueIndex;

    public bool Interact(PlayerInteraction playerInteraction)
    {
        MessageSystem.Instance?.ShowMessage($"Vigia: {Dialogue[dialogueIndex]}");
        dialogueIndex = (dialogueIndex + 1) % Dialogue.Length;
        return true;
    }
}

public class WorldFacingLabel : MonoBehaviour
{
    void LateUpdate()
    {
        Camera camera = RuntimeCameraCache.Main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
    }
}
