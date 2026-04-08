using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

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
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrain = 20f;
    public float staminaRecovery = 15f;
    public float sprintResumeThresholdPercent = 0.5f;
    public float lowStaminaThresholdPercent = 0.25f;
    public float lowStaminaSpeedMultiplier = 0.45f;

    [Header("Vida")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Fome")]
    public float maxHunger = 100f;
    public float currentHunger;

    public float hungerDrainIdle = 0.04f;
    public float hungerDrainWalk = 0.12f;
    public float hungerDrainRun = 0.3f;
    public float hungerDamageRate = 1.5f;

    [Header("Sede")]
    public float maxThirst = 100f;
    public float currentThirst;
    public float thirstDrainIdle = 0.08f;
    public float thirstDrainWalk = 0.25f;
    public float thirstDrainRun = 0.6f;
    public float thirstDamageRate = 2f;

    [Header("Agua")]
    public float waterMovementMultiplier = 0.55f;
    public float waterSprintMultiplier = 0.7f;
    public float visualWaterSink = 0.85f;
    public float cameraWaterSink = 0.18f;
    public float waterDetectionOffset = 0.15f;

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
        inventory = GetComponent<Inventory>() ?? FindFirstObjectByType<Inventory>();
        hotbar = GetComponent<Hotbar>() ?? FindFirstObjectByType<Hotbar>();
        spawnPosition = transform.position;
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

        if (currentThirst <= 0)
            TakeDamage(thirstDamageRate * Time.deltaTime);
    }

    public void Heal(float amount)
    {
        if (GameState.IsPlayerDead)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("Curou: " + currentHealth);
    }

    public void TakeDamage(float amount)
    {
        if (GameState.IsPlayerDead)
            return;

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
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yVelocity = 0f;

        controller.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        controller.enabled = true;

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

        playerModel.SetActive(thirdPerson);
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
}
