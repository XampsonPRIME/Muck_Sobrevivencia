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

        Material rock = CreateMaterial("Sanctuary slate", new Color(0.22f, 0.29f, 0.3f, 1f));
        Material darkRock = CreateMaterial("Sanctuary shadow stone", new Color(0.055f, 0.085f, 0.1f, 1f));
        Material rune = CreateMaterial("Ancient gold", new Color(0.82f, 0.49f, 0.16f, 1f), true);

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

        caveRoot.AddComponent<SanctuaryArchitecture>().Build(rock, darkRock, rune);
        CreateLighting();
        CreateAtmosphere(rune);
        CreateNpc(rock, rune);
        CreatePedestals(rock);
        BuildIntroUi();
        SetLayerRecursively(caveRoot, ResolveCaveLayer());
        RefreshPedestals();
    }

    void CreateLighting()
    {
        CreatePointLight("CaveLightCenter", new Vector3(0f, 5.7f, -3f), new Color(0.45f, 0.72f, 0.8f), 7f, 24f, false);
        CreatePointLight("CaveLightLeft", new Vector3(-8f, 3.6f, 3f), new Color(1f, 0.68f, 0.32f), 6f, 14f, true);
        CreatePointLight("CaveLightRight", new Vector3(8f, 3.6f, 3f), new Color(1f, 0.68f, 0.32f), 6f, 14f, true);
    }

    void CreatePointLight(string lightName, Vector3 localPosition, Color color, float intensity, float range, bool animate)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(caveRoot.transform, false);
        lightObject.transform.localPosition = localPosition;
        Light point = lightObject.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = color;
        point.intensity = intensity;
        point.range = range;
        // These three animated fill lights otherwise allocate 18 cubemap faces.
        // Keep their colored illumination without consuming the shadow atlas.
        point.shadows = LightShadows.None;
        point.cullingMask = 1 << ResolveCaveLayer();
        if (animate)
        {
            CaveLightPulse pulse = lightObject.AddComponent<CaveLightPulse>();
            pulse.baseIntensity = intensity;
            pulse.frequency = lightName.Contains("Center") ? 0.7f : 1.1f;
        }
    }

    void CreateAtmosphere(Material rune)
    {
        GameObject atmosphere = new GameObject("CaveAtmosphere");
        atmosphere.transform.SetParent(caveRoot.transform, false);

        ParticleSystem particles = atmosphere.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = 90;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new Color(1f, 0.72f, 0.35f, 0.42f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission;
        emission.rateOverTime = 18f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 5f, 18f);
        shape.position = new Vector3(0f, 3.2f, 0f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        // All axes must use the same ParticleSystemCurveMode. Setting every
        // axis explicitly avoids Unity's "curves must all be in the same mode" error.
        ParticleSystem.MinMaxCurve driftX = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        ParticleSystem.MinMaxCurve driftY = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        ParticleSystem.MinMaxCurve driftZ = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.x = driftX;
        velocity.y = driftY;
        velocity.z = driftZ;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = rune;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

    }

    void CreateNpc(Material rock, Material rune)
    {
        GameObject npc = new GameObject("VigiaSemNome");
        npc.transform.SetParent(caveRoot.transform, false);
        npc.transform.localPosition = new Vector3(0f, 0.55f, 1.2f);
        npc.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        caveRoot.GetComponent<SanctuaryArchitecture>().DressKeeper(npc.transform);
        CreatePart("NpcEyeL", PrimitiveType.Sphere, npc.transform, new Vector3(-0.18f, 2.63f, 0.5f), new Vector3(0.09f, 0.07f, 0.06f), rune);
        CreatePart("NpcEyeR", PrimitiveType.Sphere, npc.transform, new Vector3(0.18f, 2.63f, 0.5f), new Vector3(0.09f, 0.07f, 0.06f), rune);

        CapsuleCollider interactionCollider = npc.AddComponent<CapsuleCollider>();
        interactionCollider.center = new Vector3(0f, 1.4f, 0f);
        interactionCollider.height = 3.4f;
        interactionCollider.radius = 0.8f;
        npc.AddComponent<PowerSelectionNpc>();
        CreateWorldLabel(npc.transform, "O VIGIA", new Vector3(0f, 3.45f, 0f), new Color(0.86f, 0.8f, 0.64f, 1f), 0.9f);
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
            caveRoot.GetComponent<SanctuaryArchitecture>().DecorateAltar(pedestalObject.transform, definition.secondaryColor);

            GameObject crystal = CreatePart(
                "PowerCrystal",
                PrimitiveType.Cube,
                pedestalObject.transform,
                new Vector3(0f, 2.25f, 0f),
                new Vector3(0.72f, 1.15f, 0.72f),
                CreateMaterial($"{definition.stableId}Crystal", definition.primaryColor, true));
            crystal.GetComponent<MeshFilter>().sharedMesh = caveRoot.GetComponent<SanctuaryArchitecture>().CrystalMesh;
            crystal.transform.localScale = new Vector3(0.55f, 0.8f, 0.55f);

            BoxCollider interactionCollider = pedestalObject.AddComponent<BoxCollider>();
            interactionCollider.center = new Vector3(0f, 1.45f, 0f);
            interactionCollider.size = new Vector3(2.4f, 3.2f, 2.4f);

            TMP_Text label = CreateWorldLabel(
                pedestalObject.transform,
                definition.displayName.ToUpperInvariant(),
                new Vector3(0f, 3.25f, 0f),
                definition.secondaryColor,
                1.1f);

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

        Image vignette = CreateUiImage("Vignette", canvasObject.transform, new Color(0.015f, 0.02f, 0.045f, 0.06f));
        vignette.rectTransform.anchorMin = Vector2.zero;
        vignette.rectTransform.anchorMax = Vector2.one;
        vignette.rectTransform.offsetMin = Vector2.zero;
        vignette.rectTransform.offsetMax = Vector2.zero;
        vignette.raycastTarget = false;

        TMP_Text title = CreateUiText("Title", canvasObject.transform, "S A N T U Á R I O   D O S   P O R T A D O R E S", 30, FontStyles.Normal);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -42f);
        title.rectTransform.sizeDelta = new Vector2(1400f, 64f);
        title.color = LobbyPresentation.Ivory;
        title.enableWordWrapping = false;

        TMP_Text subtitle = CreateUiText("Subtitle", canvasObject.transform, "Aproxime-se de um legado e pressione E para absorvê-lo", 22, FontStyles.Italic);
        subtitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -92f);
        subtitle.rectTransform.sizeDelta = new Vector2(1400f, 42f);
        subtitle.color = new Color(0.72f, 0.86f, 0.9f, 1f);
        subtitle.enableWordWrapping = false;

        availabilityText = CreateUiText("Availability", canvasObject.transform, string.Empty, 17, FontStyles.Normal);
        availabilityText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        availabilityText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        availabilityText.rectTransform.pivot = new Vector2(0.5f, 0f);
        availabilityText.rectTransform.anchoredPosition = new Vector2(0f, 64f);
        availabilityText.rectTransform.sizeDelta = new Vector2(1400f, 42f);
        availabilityText.color = new Color(0.92f, 0.82f, 0.5f, 1f);
        availabilityText.enableWordWrapping = false;
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
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);

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

/// <summary>Subtle organic movement keeps the cave alive without affecting gameplay.</summary>
public sealed class CaveLightPulse : MonoBehaviour
{
    public float baseIntensity = 2f;
    public float frequency = 1f;
    Light source;
    float phase;

    void Awake()
    {
        source = GetComponent<Light>();
        phase = transform.position.x * 0.17f + transform.position.z * 0.11f;
    }

    void Update()
    {
        if (source == null)
            return;

        float wave = Mathf.Sin(Time.time * frequency + phase) * 0.08f +
                     Mathf.Sin(Time.time * frequency * 2.37f + phase * 1.7f) * 0.035f;
        source.intensity = baseIntensity * (1f + wave);
    }
}

public sealed class CaveFloatingShard : MonoBehaviour
{
    Vector3 start;
    float phase;

    void Awake()
    {
        start = transform.localPosition;
        phase = start.x * 0.23f + start.z * 0.17f;
    }

    void Update()
    {
        float t = Time.time * 0.65f + phase;
        transform.localPosition = start + new Vector3(Mathf.Sin(t) * 0.08f, Mathf.Sin(t * 1.37f) * 0.12f, Mathf.Cos(t) * 0.08f);
        transform.Rotate(0f, 18f * Time.deltaTime, 9f * Time.deltaTime, Space.Self);
    }
}

/// <summary>Runtime sanctuary art. Decorative geometry has no gameplay colliders.</summary>
public sealed class SanctuaryArchitecture : MonoBehaviour
{
    Material stone, dark, gold, bronze, cloth;
    readonly List<Object> owned = new List<Object>();
    readonly Dictionary<CanvasGroup, float> hiddenHud = new Dictionary<CanvasGroup, float>();
    float nextHudScan;
    public Mesh CrystalMesh { get; private set; }

    public void Build(Material slate, Material shadow, Material glow)
    {
        stone = slate; dark = shadow; gold = glow;
        var grain = new Texture2D(128, 128, TextureFormat.RGB24, false) { name = "Slate mineral grain", wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float n = 0.55f + Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.35f + Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.1f;
                grain.SetPixel(x, y, new Color(n, n, n));
            }
        grain.Apply(); owned.Add(grain);
        stone.SetTexture("_BaseMap", grain);
        bronze = new Material(slate) { name = "Aged sanctuary bronze" };
        bronze.color = new Color(0.48f, 0.32f, 0.13f);
        bronze.SetFloat("_Metallic", 0.55f);
        owned.Add(bronze);
        cloth = new Material(shadow) { name = "Keeper midnight cloth" };
        cloth.color = new Color(0.055f, 0.12f, 0.14f);
        owned.Add(cloth);
        CrystalMesh = Crystal();

        // Slate paving with narrow seams, all below the locked walking height.
        for (int x = -6; x <= 6; x++)
            for (int z = -4; z <= 4; z++)
                Part("Cut slate paving", PrimitiveType.Cube, transform,
                    new Vector3(x * 2f, 0.505f, z * 2.2f), new Vector3(1.95f, 0.015f, 2.15f),
                    (x + z) % 4 == 0 ? dark : stone);
        Ring(transform, new Vector3(0, 0.525f, 0), 4.1f, 0.045f, bronze, true);
        Ring(transform, new Vector3(0, 0.528f, 0), 3.75f, 0.018f, gold, true);
        Ring(transform, new Vector3(0, 0.53f, 0), 1.4f, 0.035f, bronze, true);
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI / 12f;
            var mark = Part("Engraved radial seal", PrimitiveType.Cube, transform,
                new Vector3(Mathf.Sin(a) * 3.93f, 0.535f, Mathf.Cos(a) * 3.93f), new Vector3(0.045f, 0.012f, 0.17f), gold);
            mark.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Part("Processional inlay", PrimitiveType.Cube, transform, new Vector3(side * 1.7f, 0.53f, -5.8f), new Vector3(0.035f, 0.02f, 4.8f), bronze);
            for (int z = -8; z <= 8; z += 4)
            {
                Pillar(new Vector3(side * 12.4f, 0.5f, z), 6.2f);
                for (int row = 0; row < 4; row++)
                    Part("Recessed wall masonry", PrimitiveType.Cube, transform, new Vector3(side * 13f, 1.4f + row * 1.7f, z + 1.8f), new Vector3(0.45f, 1.63f, 3.9f), row % 2 == 0 ? stone : dark);
            }
        }
        foreach (float x in new[] { -9f, -3f, 3f, 9f })
        {
            Pillar(new Vector3(x - 2.1f, 0.5f, 7f), 4f);
            Pillar(new Vector3(x + 2.1f, 0.5f, 7f), 4f);
            Arch(new Vector3(x, 4.5f, 7f), 2.1f);
            Part("Altar niche backing", PrimitiveType.Cube, transform, new Vector3(x, 3.4f, 9.5f), new Vector3(4.5f, 5.7f, 0.5f), dark);
            Ring(transform, new Vector3(x, 3.55f, 8.95f), 1.3f, 0.055f, bronze, false);
            for (int c = 0; c < 5; c++)
            {
                float px = x - 1.55f + c * 0.78f;
                float h = 0.22f + (c % 3) * 0.1f;
                Part("Votive candle", PrimitiveType.Cylinder, transform, new Vector3(px, 0.55f + h, 6.9f), new Vector3(0.11f, h, 0.11f), bronze);
                MeshPart("Candle flame", CrystalMesh, transform, new Vector3(px, 0.6f + h * 2, 6.9f), new Vector3(0.035f, 0.095f, 0.035f), gold);
                // Small emissive flames use no extra lights or shadow maps.
            }
        }
        // Tall ribs turn the flat room shell into a vaulted interior.
        for (int z = -7; z <= 7; z += 7)
            for (int i = 0; i < 25; i++)
            {
                float a = i * Mathf.PI / 24;
                var rib = Part("Vault rib", PrimitiveType.Cube, transform,
                    new Vector3(Mathf.Cos(a) * 12, 5.9f + Mathf.Sin(a) * 1.45f, z), new Vector3(1.57f, 0.22f, 0.4f), bronze);
                rib.localRotation = Quaternion.Euler(0, 0, -Mathf.Cos(a) * 7);
            }
        Ring(transform, new Vector3(0, 4.7f, 9.85f), 2.2f, 0.09f, bronze, false);
        Ring(transform, new Vector3(0, 4.7f, 9.8f), 1.95f, 0.025f, gold, false);
        var relic = MeshPart("Sanctuary heart", CrystalMesh, transform, new Vector3(0, 4.7f, 9.3f), new Vector3(0.55f, 1.15f, 0.55f), gold);
        relic.gameObject.AddComponent<CaveFloatingShard>();
    }

    void Pillar(Vector3 p, float height)
    {
        Part("Carved plinth", PrimitiveType.Cube, transform, p + Vector3.up * 0.16f, new Vector3(1.15f, 0.32f, 1.15f), dark);
        for (int i = 0; i < 6; i++)
            Part("Pillar ashlar", PrimitiveType.Cube, transform, p + Vector3.up * (0.36f + (i + 0.5f) * height / 6), new Vector3(0.76f, height / 6 - 0.04f, 0.82f), stone);
        Part("Pillar capital", PrimitiveType.Cube, transform, p + Vector3.up * (height + 0.36f), new Vector3(1.12f, 0.22f, 1.05f), bronze);
        Part("Pillar gilded flute", PrimitiveType.Cube, transform, p + new Vector3(0, height * 0.5f, -0.43f), new Vector3(0.06f, height * 0.75f, 0.035f), bronze);
    }

    void Arch(Vector3 center, float radius)
    {
        for (int i = 0; i < 17; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI / 17;
            var block = Part("Arch voussoir", PrimitiveType.Cube, transform,
                center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0),
                new Vector3(radius * Mathf.PI / 17 - 0.015f, 0.47f, 0.9f), i == 8 ? bronze : stone);
            block.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90);
        }
    }

    public void DecorateAltar(Transform parent, Color accent)
    {
        Part("Altar stepped foot", PrimitiveType.Cube, parent, new Vector3(0, 0.12f, 0), new Vector3(1.9f, 0.24f, 1.65f), dark);
        Part("Altar carved chest", PrimitiveType.Cube, parent, new Vector3(0, 0.95f, 0), new Vector3(1.3f, 1.35f, 1.1f), stone);
        Part("Altar cornice", PrimitiveType.Cube, parent, new Vector3(0, 1.65f, 0), new Vector3(1.6f, 0.18f, 1.4f), bronze);
        Ring(parent, new Vector3(0, 0.95f, 0.565f), 0.42f, 0.03f, bronze, false);
        Ring(parent, new Vector3(0, 2.45f, 0), 0.85f, 0.035f, bronze, false);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            MeshPart("Altar crown", CrystalMesh, parent, new Vector3(Mathf.Cos(a) * 0.65f, 1.9f, Mathf.Sin(a) * 0.65f), new Vector3(0.07f, 0.28f, 0.07f), bronze);
        }
    }

    public void DressKeeper(Transform parent)
    {
        // A fluted, tapered mantle with a hood opening and clasp, not a capsule.
        MeshPart("Pleated mantle", Mantle(), parent, Vector3.zero, Vector3.one, cloth);
        Part("Keeper hood", PrimitiveType.Sphere, parent, new Vector3(0, 2.57f, 0.06f), new Vector3(0.82f, 0.95f, 0.68f), cloth);
        Part("Recessed face", PrimitiveType.Sphere, parent, new Vector3(0, 2.57f, 0.32f), new Vector3(0.52f, 0.62f, 0.17f), dark);
        for (int side = -1; side <= 1; side += 2)
        {
            var trim = Part("Mantle gold trim", PrimitiveType.Cube, parent, new Vector3(side * 0.22f, 1.32f, 0.41f), new Vector3(0.035f, 1.95f, 0.025f), bronze);
            trim.localRotation = Quaternion.Euler(0, 0, side * 7);
            var sleeve = Part("Folded sleeve", PrimitiveType.Capsule, parent, new Vector3(side * 0.43f, 1.67f, 0.18f), new Vector3(0.35f, 0.57f, 0.38f), cloth);
            sleeve.localRotation = Quaternion.Euler(20, 0, side * 27);
        }
        MeshPart("Keeper clasp", CrystalMesh, parent, new Vector3(0, 2.05f, 0.48f), new Vector3(0.1f, 0.14f, 0.06f), gold);
        Part("Keeper staff", PrimitiveType.Cylinder, parent, new Vector3(-0.85f, 1.42f, 0.12f), new Vector3(0.065f, 1.4f, 0.065f), bronze);
        Ring(parent, new Vector3(-0.85f, 2.92f, 0.12f), 0.22f, 0.035f, bronze, false);
        MeshPart("Staff ember", CrystalMesh, parent, new Vector3(-0.85f, 2.92f, 0.12f), new Vector3(0.08f, 0.14f, 0.08f), gold);
    }

    Transform Part(string name, PrimitiveType type, Transform parent, Vector3 p, Vector3 s, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = s;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    Transform MeshPart(string name, Mesh mesh, Transform parent, Vector3 p, Vector3 s, Material m)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = s;
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    void Ring(Transform parent, Vector3 p, float radius, float thickness, Material material, bool horizontal)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= 96; i++)
        {
            float a = i * Mathf.PI * 2 / 96;
            for (int edge = 0; edge < 2; edge++)
            {
                float r = radius + (edge == 0 ? -thickness : thickness);
                vertices.Add(horizontal ? new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r) : new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0));
            }
            if (i == 96) continue;
            int n = i * 2; triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 });
        }
        MeshPart("Runic metal ring", Mesh(vertices, triangles), parent, p, Vector3.one, material);
    }

    Mesh Crystal()
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3, b = (i + 1) * Mathf.PI / 3;
            Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
            int n = v.Count; v.AddRange(new[] { Vector3.up, q, p, Vector3.down, p, q });
            t.AddRange(new[] { n, n + 1, n + 2, n + 3, n + 4, n + 5 });
        }
        return Mesh(v, t);
    }

    Mesh Mantle()
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int row = 0; row < 4; row++)
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                float r = new[] { 0.68f, 0.48f, 0.54f, 0.23f }[row] + Mathf.Cos(a * 12) * 0.04f;
                v.Add(new Vector3(Mathf.Cos(a) * r, new[] { 0.05f, 1.1f, 2.05f, 2.35f }[row], Mathf.Sin(a) * r));
                if (row == 3 || i == 48) continue;
                int n = row * 49 + i; t.AddRange(new[] { n, n + 49, n + 1, n + 1, n + 49, n + 50 });
            }
        return Mesh(v, t);
    }

    Mesh Mesh(List<Vector3> vertices, List<int> indices)
    {
        var mesh = new Mesh { name = "Sanctuary crafted geometry" };
        mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh); return mesh;
    }

    void LateUpdate()
    {
        if (!GameState.IsPowerSelectionOpen) { RestoreHud(); return; }
        if (Time.unscaledTime >= nextHudScan)
        {
            nextHudScan = Time.unscaledTime + 0.5f;
            foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rect == null) continue;
                string n = rect.name.Replace("(Clone)", "");
                if (n != "StatsHud" && n != "Hotbar" && n != "AncestralPowerPanel") continue;
                // Unity's missing/destroyed component wrappers require its overloaded
                // null comparison; C# ?? can retain an invalid native component.
                var group = rect.GetComponent<CanvasGroup>();
                if (group == null)
                    group = rect.gameObject.AddComponent<CanvasGroup>();
                if (group == null) continue;
                if (!hiddenHud.ContainsKey(group)) hiddenHud.Add(group, group.alpha);
            }
        }
        foreach (var pair in hiddenHud) if (pair.Key != null) pair.Key.alpha = 0;
    }

    void RestoreHud()
    {
        foreach (var pair in hiddenHud) if (pair.Key != null) pair.Key.alpha = pair.Value;
        hiddenHud.Clear();
    }
    void OnDisable() { RestoreHud(); }
    void OnDestroy() { RestoreHud(); foreach (var asset in owned) if (asset != null) Destroy(asset); }
}
