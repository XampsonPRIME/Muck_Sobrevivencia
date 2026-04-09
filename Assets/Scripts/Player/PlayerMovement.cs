using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float jumpForce = 6f;
    public float gravity = -9.8f;

    [Header("Mouse")]
    public float mouseSensitivity = 2f;

    [Header("Camera")]
    public Transform cameraHolder;
    public bool thirdPerson = false;
    public float thirdPersonPitch = 15f;

    public Vector3 firstPersonOffset = new Vector3(0, 1.6f, 0);
    public Vector3 thirdPersonOffset = new Vector3(0, 2f, -3f);

    [Header("Stamina")]
    public float maxStamina = 500f;
    public float currentStamina;
    public float staminaDrain = 20f;
    public float staminaRecovery = 15f;
    public float sprintResumeThresholdPercent = 0.5f;
    public float lowStaminaThresholdPercent = 0.25f;
    public float lowStaminaSpeedMultiplier = 0.45f;

    [Header("Vida")]
    public float maxHealth = 500f;
    public float currentHealth;
    public float healthRegenPerSecond = 4f;
    public float healthRegenDelay = 6f;

    [Header("Fome")]
    public float maxHunger = 500f;
    public float currentHunger;

    public float hungerDrainIdle = 0.04f;
    public float hungerDrainWalk = 0.12f;
    public float hungerDrainRun = 0.3f;
    public float hungerDamageRate = 1.5f;
    [Range(0f, 1f)] public float lowHungerWarningThreshold = 0.5f;
    [Range(0f, 1f)] public float criticalHungerWarningThreshold = 0.05f;
    [Range(0f, 1f)] public float lowHungerWarningResetThreshold = 0.6f;

    [Header("Sede")]
    public float maxThirst = 500f;
    public float currentThirst;
    public float thirstDrainIdle = 0.08f;
    public float thirstDrainWalk = 0.25f;
    public float thirstDrainRun = 0.6f;
    public float thirstDamageRate = 2f;
    [Range(0f, 1f)] public float lowThirstWarningThreshold = 0.5f;
    [Range(0f, 1f)] public float criticalThirstWarningThreshold = 0.05f;
    [Range(0f, 1f)] public float lowThirstWarningResetThreshold = 0.6f;

    [Header("Agua")]
    public float waterMovementMultiplier = 0.55f;
    public float waterSprintMultiplier = 0.7f;
    public float visualWaterSink = 0.85f;
    public float cameraWaterSink = 0.18f;
    public float waterDetectionOffset = 0.15f;

    [Header("Respawn")]
    public float respawnInvulnerabilityDuration = 3f;
    public float spawnRayHeight = 40f;
    public float spawnRayDistance = 120f;
    public float spawnGroundPadding = 0.08f;
    public float spawnAirDropHeight = 10f;
    public LayerMask spawnGroundMask = ~0;

    CharacterController controller;
    PlayerControls controls;
    Animator anim;
    Inventory inventory;
    Hotbar hotbar;
    InputAction toggleCameraAction;
    InputAction damageTestAction;
    InputAction respawnAction;

    Vector2 moveInput;
    Vector2 lookInput;

    float yVelocity;
    float xRotation;
    float yRotation;
    bool isRunning;
    bool sprintLocked;
    bool isInWater;
    bool lowHungerWarningShown;
    bool criticalHungerWarningShown;
    bool lowThirstWarningShown;
    bool criticalThirstWarningShown;
    float respawnInvulnerabilityEndTime;
    float lastDamageTime = float.NegativeInfinity;
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    GameObject deathMessageInstance;

    GameObject playerModel;
    Vector3 playerModelStartLocalPosition;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();
        anim = GetComponentInChildren<Animator>();
        toggleCameraAction = new InputAction("ToggleCamera", binding: "<Keyboard>/c");
        damageTestAction = new InputAction("DamageTest", binding: "<Keyboard>/h");
        respawnAction = new InputAction("Respawn", binding: "<Keyboard>/r");

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        currentHunger = maxHunger;
        currentThirst = maxThirst;

        playerModel = anim.gameObject;
        playerModelStartLocalPosition = playerModel.transform.localPosition;
        inventory = GetComponent<Inventory>();
        hotbar = GetComponent<Hotbar>() ?? FindFirstObjectByType<Hotbar>();
        spawnPosition = ResolveSafeSpawnPosition(transform.position);
        spawnRotation = transform.rotation;
    }

    void OnEnable()
    {
        controls.Enable();
        toggleCameraAction.Enable();
        damageTestAction.Enable();
        respawnAction.Enable();
    }

    void OnDisable()
    {
        respawnAction.Disable();
        damageTestAction.Disable();
        toggleCameraAction.Disable();
        controls.Disable();
    }

    void Start()
    {
        yRotation = transform.eulerAngles.y;
        xRotation = thirdPerson ? thirdPersonPitch : 0f;
        ApplyCameraPose();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (GameState.IsInLobby)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            HandleModelVisibility();
            return;
        }

        if (GameState.IsPaused)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            HandleModelVisibility();
            return;
        }

        if (GameState.IsPlayerDead)
        {
            if (respawnAction.WasPressedThisFrame())
                Respawn();

            HandleModelVisibility();
            return;
        }

        moveInput = controls.Player.Move.ReadValue<Vector2>();
        lookInput = controls.Player.Look.ReadValue<Vector2>();
        isRunning = controls.Player.Run.IsPressed() && !sprintLocked;

        if (toggleCameraAction.WasPressedThisFrame())
        {
            thirdPerson = !thirdPerson;
            xRotation = thirdPerson ? thirdPersonPitch : Mathf.Clamp(xRotation, -80f, 80f);
        }

        HandleStamina();
        Look();
        Move();
        HandleModelVisibility();
        HandleHunger();
        HandleThirst();
        HandleHealthRegeneration();

        if (damageTestAction.WasPressedThisFrame())
        {
            TakeDamage(10f);
        }

        UpdateWaterState();
    }

    void LateUpdate()
    {
        ApplyCameraPose();
    }

    public void ApplySavedState(Vector3 position, Quaternion rotation, bool savedThirdPerson, float savedHealth, float savedStamina, float savedHunger, float savedThirst)
    {
        thirdPerson = savedThirdPerson;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yVelocity = 0f;

        if (controller != null)
            controller.enabled = false;

        Vector3 safePosition = ResolveSafeSpawnPosition(position);
        transform.SetPositionAndRotation(safePosition, rotation);

        if (controller != null)
            controller.enabled = true;

        currentHealth = Mathf.Clamp(savedHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(savedStamina, 0f, maxStamina);
        currentHunger = Mathf.Clamp(savedHunger, 0f, maxHunger);
        currentThirst = Mathf.Clamp(savedThirst, 0f, maxThirst);

        spawnPosition = safePosition;
        spawnRotation = rotation;
        yRotation = transform.eulerAngles.y;
        xRotation = thirdPerson ? thirdPersonPitch : 0f;
        ApplyCameraPose();
    }

    public void ResetToFreshStart()
    {
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;

        ApplySavedState(
            spawnPosition,
            spawnRotation,
            false,
            maxHealth,
            maxStamina,
            maxHunger,
            maxThirst
        );

        lowHungerWarningShown = false;
        criticalHungerWarningShown = false;
        lowThirstWarningShown = false;
        criticalThirstWarningShown = false;
        respawnInvulnerabilityEndTime = Time.time + respawnInvulnerabilityDuration;
        lastDamageTime = Time.time;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        sprintLocked = false;
        yVelocity = 0f;
        HideDeathMessage();
    }

    void HandleHunger()
    {
        if (GameState.IsPlayerDead)
            return;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        float hungerDrain = hungerDrainIdle;

        if (isMoving)
            hungerDrain = isRunning ? hungerDrainRun : hungerDrainWalk;

        currentHunger -= hungerDrain * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

        HandleLowHungerWarning();

        if (currentHunger <= 0)
        {
            TakeDamage(hungerDamageRate * Time.deltaTime);
        }
    }

    public void RestoreHunger(float amount)
    {
        currentHunger += amount;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);
    }

    void HandleLowHungerWarning()
    {
        if (maxHunger <= 0f)
            return;

        float hungerPercent = currentHunger / maxHunger;

        if (!lowHungerWarningShown && hungerPercent <= lowHungerWarningThreshold)
        {
            lowHungerWarningShown = true;
            MessageSystem.Instance?.ShowMessage("Estou com fome");
        }

        if (lowHungerWarningShown && hungerPercent >= lowHungerWarningResetThreshold)
            lowHungerWarningShown = false;

        if (!criticalHungerWarningShown && hungerPercent <= criticalHungerWarningThreshold)
            MessageSystem.Instance?.ShowMessage("Vou morrer de fome!");

        criticalHungerWarningShown = hungerPercent <= criticalHungerWarningThreshold;
    }

    public void RestoreThirst(float amount)
    {
        currentThirst += amount;
        currentThirst = Mathf.Clamp(currentThirst, 0, maxThirst);
    }

    void HandleThirst()
    {
        if (GameState.IsPlayerDead)
            return;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        float thirstDrain = thirstDrainIdle;

        if (isMoving)
            thirstDrain = isRunning ? thirstDrainRun : thirstDrainWalk;

        currentThirst -= thirstDrain * Time.deltaTime;
        currentThirst = Mathf.Clamp(currentThirst, 0, maxThirst);

        HandleLowThirstWarning();

        if (currentThirst <= 0)
            TakeDamage(thirstDamageRate * Time.deltaTime);
    }

    void HandleLowThirstWarning()
    {
        if (maxThirst <= 0f)
            return;

        float thirstPercent = currentThirst / maxThirst;

        if (!lowThirstWarningShown && thirstPercent <= lowThirstWarningThreshold)
        {
            lowThirstWarningShown = true;
            MessageSystem.Instance?.ShowMessage("Estou com cede");
        }

        if (lowThirstWarningShown && thirstPercent >= lowThirstWarningResetThreshold)
            lowThirstWarningShown = false;

        if (!criticalThirstWarningShown && thirstPercent <= criticalThirstWarningThreshold)
            MessageSystem.Instance?.ShowMessage("Vou morrer de cede!");

        criticalThirstWarningShown = thirstPercent <= criticalThirstWarningThreshold;
    }

    void HandleHealthRegeneration()
    {
        if (GameState.IsPlayerDead || GameState.IsPaused || GameState.IsInLobby)
            return;

        if (currentHealth >= maxHealth || healthRegenPerSecond <= 0f)
            return;

        if (currentHunger <= 0f || currentThirst <= 0f)
            return;

        if (Time.time < lastDamageTime + healthRegenDelay)
            return;

        Heal(healthRegenPerSecond * Time.deltaTime);
    }

    public void Heal(float amount)
    {
        if (GameState.IsPlayerDead)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (GameState.IsPlayerDead)
            return;

        if (Time.time < respawnInvulnerabilityEndTime)
            return;

        lastDamageTime = Time.time;
        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        GameState.IsPlayerDead = true;
        GameState.IsInventoryOpen = false;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yVelocity = 0f;

        List<InventoryItem> droppedItems = inventory != null ? inventory.CreateSnapshot() : null;
        if (droppedItems != null && droppedItems.Count > 0)
            DeathLoot.Spawn(transform.position, droppedItems);

        inventory?.ClearAll();
        hotbar?.ClearAll();

        InventoryUI inventoryUi = FindFirstObjectByType<InventoryUI>();
        if (inventoryUi != null)
            inventoryUi.Refresh();

        ShowDeathMessage();

        Debug.Log("Player morreu");
    }

    void Respawn()
    {
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;

        currentHealth = maxHealth;
        currentHunger = maxHunger;
        currentThirst = maxThirst;
        currentStamina = maxStamina;
        lowHungerWarningShown = false;
        criticalHungerWarningShown = false;
        lowThirstWarningShown = false;
        criticalThirstWarningShown = false;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yVelocity = 0f;
        lastDamageTime = Time.time;

        controller.enabled = false;
        Vector3 safeRespawnPosition = ResolveSafeSpawnPosition(spawnPosition);
        transform.SetPositionAndRotation(safeRespawnPosition, spawnRotation);
        spawnPosition = safeRespawnPosition;
        controller.enabled = true;

        respawnInvulnerabilityEndTime = Time.time + respawnInvulnerabilityDuration;
        yRotation = transform.eulerAngles.y;
        xRotation = thirdPerson ? thirdPersonPitch : 0f;
        ApplyCameraPose();
        HideDeathMessage();
    }

    void ShowDeathMessage()
    {
        if (deathMessageInstance != null)
            return;

        GameObject deathMessagePrefab = Resources.Load<GameObject>("DeathMessage");
        if (deathMessagePrefab != null)
        {
            deathMessageInstance = Instantiate(deathMessagePrefab);
            return;
        }

        deathMessageInstance = CreateDeathMessageFallback();
    }

    void HideDeathMessage()
    {
        if (deathMessageInstance == null)
            return;

        Destroy(deathMessageInstance);
        deathMessageInstance = null;
    }

    GameObject CreateDeathMessageFallback()
    {
        GameObject canvasObject = new GameObject("DeathMessageFallback");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObject = new GameObject("Overlay");
        overlayObject.transform.SetParent(canvasObject.transform, false);

        RectTransform overlayRect = overlayObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject textObject = new GameObject("DeathText");
        textObject.transform.SetParent(overlayObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(1100f, 180f);
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI deathText = textObject.AddComponent<TextMeshProUGUI>();
        deathText.text = "Você Morreu!\nAperte R para renascer";
        deathText.fontSize = 72f;
        deathText.enableAutoSizing = true;
        deathText.fontSizeMin = 30f;
        deathText.fontSizeMax = 72f;
        deathText.alignment = TextAlignmentOptions.Center;
        deathText.color = Color.white;

        return canvasObject;
    }

    void HandleModelVisibility()
    {
        if (playerModel == null)
            return;

        bool shouldShowModel = thirdPerson;
        if (playerModel.activeSelf != shouldShowModel)
            playerModel.SetActive(shouldShowModel);
    }

    void HandleStamina()
    {
        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool canRun = currentStamina > 0.01f && !sprintLocked;

        if (isRunning && isMoving && canRun)
        {
            currentStamina -= staminaDrain * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isRunning = false;
                sprintLocked = true;
            }
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRecovery * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        if (sprintLocked && currentStamina >= maxStamina * sprintResumeThresholdPercent)
            sprintLocked = false;
    }

    void Move()
    {
        bool grounded = controller.isGrounded;

        if (grounded && yVelocity < 0f)
            yVelocity = -2f;

        if (grounded && controls.Player.Jump.WasPressedThisFrame())
            yVelocity = jumpForce;

        yVelocity += gravity * Time.deltaTime;

        Quaternion yawRotationOnly = Quaternion.Euler(0f, yRotation, 0f);
        Vector3 forward = yawRotationOnly * Vector3.forward;
        Vector3 right = yawRotationOnly * Vector3.right;
        Vector3 move = (forward * moveInput.y) + (right * moveInput.x);

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool canRun = currentStamina > 0.01f && !sprintLocked;
        float speed = (isRunning && isMoving && canRun) ? runSpeed : walkSpeed;

        if (isInWater)
            speed *= isRunning ? waterSprintMultiplier : waterMovementMultiplier;

        if (currentStamina <= maxStamina * lowStaminaThresholdPercent)
            speed *= lowStaminaSpeedMultiplier;

        Vector3 velocity = move.normalized * speed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);

        if (anim != null)
        {
            float animSpeed = (isRunning && isMoving) ? 1f : moveInput.magnitude * 0.5f;
            anim.SetFloat("Speed", animSpeed);
        }
    }

    void Look()
    {
        Vector2 mouseDelta = lookInput * mouseSensitivity;

        yRotation += mouseDelta.x;

        if (!thirdPerson)
        {
            xRotation -= mouseDelta.y;
            xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        }
        else
        {
            xRotation = thirdPersonPitch;
        }
    }

    void ApplyCameraPose()
    {
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        if (cameraHolder == null)
            return;

        Vector3 poseOffset = thirdPerson ? thirdPersonOffset : firstPersonOffset;
        if (isInWater)
            poseOffset.y -= cameraWaterSink;

        cameraHolder.localPosition = poseOffset;
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void UpdateWaterState()
    {
        isInWater = false;

        if (RiverSystem.Instance == null)
        {
            ApplyWaterVisuals();
            return;
        }

        Vector3 feetPosition = transform.position + Vector3.up * waterDetectionOffset;
        if (RiverSystem.Instance.TryGetWaterSurfaceHeight(feetPosition, out float waterSurfaceHeight))
            isInWater = waterSurfaceHeight > transform.position.y + 0.05f;

        ApplyWaterVisuals();
    }

    void ApplyWaterVisuals()
    {
        if (playerModel == null)
            return;

        Vector3 targetLocalPosition = playerModelStartLocalPosition;
        if (isInWater)
            targetLocalPosition.y -= visualWaterSink;

        playerModel.transform.localPosition = Vector3.Lerp(
            playerModel.transform.localPosition,
            targetLocalPosition,
            Time.deltaTime * 10f
        );
    }

    Vector3 ResolveSafeSpawnPosition(Vector3 desiredPosition)
    {
        if (TryGetGroundedSpawnPosition(desiredPosition, out Vector3 groundedPosition))
            return groundedPosition;

        Vector3 fallbackPosition = transform.position;
        if (TryGetGroundedSpawnPosition(fallbackPosition, out groundedPosition))
            return groundedPosition;

        float emergencyHeight = Mathf.Max(
            desiredPosition.y + spawnAirDropHeight,
            transform.position.y + spawnAirDropHeight,
            spawnPosition.y + spawnAirDropHeight,
            spawnRayHeight + spawnAirDropHeight,
            60f
        );

        return new Vector3(desiredPosition.x, emergencyHeight, desiredPosition.z);
    }

    bool TryGetGroundedSpawnPosition(Vector3 desiredPosition, out Vector3 groundedPosition)
    {
        float controllerHeight = controller != null ? controller.height : 2f;
        Vector3 rayOrigin = desiredPosition + Vector3.up * spawnRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            spawnRayDistance,
            spawnGroundMask,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.MaxValue;
        groundedPosition = desiredPosition;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidSpawnGroundHit(hit))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundedPosition = hit.point + Vector3.up * (controllerHeight * 0.5f + spawnGroundPadding + spawnAirDropHeight);
            }
        }

        return closestDistance < float.MaxValue;
    }

    bool IsValidSpawnGroundHit(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null)
            return false;

        if (hit.normal.y < 0.35f)
            return false;

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return false;

        if (hitCollider.GetComponentInParent<Cow>() != null)
            return false;

        if (hitCollider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (hitCollider.GetComponentInParent<BossEnemy>() != null)
            return false;

        if (hitCollider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        return true;
    }
}
