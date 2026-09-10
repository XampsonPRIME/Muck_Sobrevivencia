using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    const string CombatMusicResourcePath = "Audio/music_combat";
    const string AmbientMusicClipName = "Forest_Whispering";

    public static bool IsCombatMusicActive { get; private set; }
    public static Vector3 DefaultFreshStartPosition => DemoWorldProgression.ResolveFreshPlayerSpawn();
    public static Quaternion DefaultFreshStartRotation => DemoWorldProgression.ResolveFreshPlayerRotation();

    [Header("Movimento")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    [Range(0.2f, 1f)] public float rangedAimSpeedMultiplier = 0.65f;
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
    public float maxHealth = 150f;
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
    public float spawnAirDropHeight = 1f;
    public LayerMask spawnGroundMask = ~0;

    [Header("Debug")]
    public bool debugImmortal;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip attackSound;
    [Range(0f, 1f)] public float attackSoundVolume = 1f;
    public float attackSoundDuration = 3f;
    public AudioClip deathSound;
    [Range(0f, 1f)] public float deathSoundVolume = 1f;
    public AudioSource combatMusicSource;
    public AudioClip combatMusicClip;
    [Range(0f, 1f)] public float combatMusicVolume = 0.75f;
    public float combatMusicHoldDuration = 6f;
    public float combatMusicFadeSpeed = 2f;

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
    bool fallAnimationPlayed;
    bool verticalPositionLocked;
    float lockedVerticalPosition;
    Vector3 externalVelocity;
    float externalVelocityTimer;
    float stunEndTime;
    float xRotation;
    float yRotation;
    bool isRunning;
    bool sprintLocked;
    bool isInWater;
    bool lowHungerWarningShown;
    bool criticalHungerWarningShown;
    bool lowThirstWarningShown;
    bool criticalThirstWarningShown;
    bool rangedAimActive;
    float rangedAimMultiplier = 1f;
    float respawnInvulnerabilityEndTime;
    float lastDamageTime = float.NegativeInfinity;
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    GameObject deathMessageInstance;
    Coroutine stopSfxCoroutine;
    float combatMusicUntilTime;
    readonly List<AudioSource> pausedAmbientMusicSources = new List<AudioSource>();
    bool ambientMusicPausedForCombat;

    GameObject playerModel;
    Vector3 playerModelStartLocalPosition;

    public Animator VisualAnimator => anim;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();
        anim = GetComponentInChildren<Animator>();
        toggleCameraAction = new InputAction("ToggleCamera", binding: "<Keyboard>/c");
        if (Debug.isDebugBuild || Application.isEditor)
            damageTestAction = new InputAction("DamageTest", binding: "<Keyboard>/h");
        respawnAction = new InputAction("Respawn", binding: "<Keyboard>/r");

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        currentHunger = maxHunger;
        currentThirst = maxThirst;

        if (anim != null)
        {
            playerModel = anim.gameObject;
            playerModelStartLocalPosition = playerModel.transform.localPosition;
        }

        inventory = GetComponent<Inventory>();
        hotbar = GetComponent<Hotbar>() ?? SceneObjectCache.Find<Hotbar>(gameObject.scene, true);
        EnsureCameraHolder();
        spawnPosition = ResolveSafeSpawnPosition(transform.position);
        spawnRotation = transform.rotation;
        EnsureSfxSource();
        EnsureCombatMusicSource();
        LoadCombatMusicClip();
    }

    void OnEnable()
    {
        controls.Enable();
        toggleCameraAction.Enable();
        damageTestAction?.Enable();
        respawnAction.Enable();
    }

    void OnDisable()
    {
        respawnAction.Disable();
        damageTestAction?.Disable();
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
        HandleCombatMusic();

        if (GameState.IsInLobby || GameState.IsWorldLoading)
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

        if (GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen || GameState.IsDemoGuideOpen)
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

        if (IsStunned)
        {
            moveInput = Vector2.zero;
            isRunning = false;
        }

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

        if (damageTestAction != null && damageTestAction.WasPressedThisFrame())
        {
            TakeDamage(10f);
        }

        UpdateWaterState();
    }

    void LateUpdate()
    {
        ApplyCameraPose();
        EnforceGameplayCursorState();
    }

    void EnforceGameplayCursorState()
    {
        if (!Application.isFocused || !ShouldLockCursorForGameplay())
            return;

        bool needsLock = Cursor.lockState != CursorLockMode.Locked;
        bool needsHide = Cursor.visible;

        if (needsLock)
            Cursor.lockState = CursorLockMode.Locked;

        if (needsHide)
            Cursor.visible = false;

    }

    bool ShouldLockCursorForGameplay()
    {
        return !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPaused &&
               !GameState.IsPlayerDead &&
               !GameState.IsInventoryOpen &&
               !GameState.IsBestiaryOpen &&
               !GameState.IsQuestJournalOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen &&
               !GameState.IsDebugChatOpen &&
               !GameState.IsDemoGuideOpen &&
               !GameState.IsMapOpen &&
               !GameState.IsPowerSelectionOpen;
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

    public bool TryGetSafeSpawnPosition(Vector3 desiredPosition, out Vector3 safePosition)
    {
        Physics.SyncTransforms();

        if (TryGetGroundedSpawnPosition(desiredPosition, out safePosition))
            return true;

        Vector3 fallbackPosition = transform.position;
        if (TryGetGroundedSpawnPosition(fallbackPosition, out safePosition))
            return true;

        Vector3 defaultFreshStartPosition = DefaultFreshStartPosition;
        if (TryGetGroundedSpawnPosition(defaultFreshStartPosition, out safePosition))
            return true;

        Vector3 recordedSpawnPosition = spawnPosition;
        if (TryGetGroundedSpawnPosition(recordedSpawnPosition, out safePosition))
            return true;

        return TryGetGroundedSpawnPosition(Vector3.zero, out safePosition);
    }

    public bool WarpToSafePosition(Vector3 desiredPosition, Quaternion rotation)
    {
        return WarpToSafePosition(desiredPosition, rotation, false);
    }

    public bool WarpToSafePosition(Vector3 desiredPosition, Quaternion rotation, bool allowAirFallback)
    {
        if (!TryGetSafeSpawnPosition(desiredPosition, out Vector3 safePosition))
        {
            if (!allowAirFallback)
                return false;

            safePosition = ResolveAirFallbackSpawnPosition(desiredPosition);
        }

        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(safePosition, rotation);

        if (controller != null)
            controller.enabled = true;

        spawnPosition = safePosition;
        spawnRotation = rotation;
        yVelocity = 0f;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yRotation = transform.eulerAngles.y;
        xRotation = thirdPerson ? thirdPersonPitch : 0f;
        ApplyCameraPose();
        return true;
    }

    Vector3 ResolveAirFallbackSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 fallback = desiredPosition;

        if (!IsFiniteVector(fallback) || fallback.sqrMagnitude < 0.001f)
            fallback = DefaultFreshStartPosition;

        float emergencyLift = Mathf.Max(GetGroundedSpawnLift(), Mathf.Max(1f, spawnAirDropHeight));
        float minimumHeight = Mathf.Max(DemoWorldProgression.FreshSpawnHeight, emergencyLift + 8f);
        fallback.y = Mathf.Max(fallback.y, minimumHeight);
        return fallback;
    }

    bool IsFiniteVector(Vector3 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) &&
               !float.IsInfinity(value.y) &&
               !float.IsInfinity(value.z);
    }

    public void TeleportExact(Vector3 position, Quaternion rotation, bool updateRespawnPoint = true)
    {
        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (controller != null)
            controller.enabled = true;

        if (updateRespawnPoint)
        {
            spawnPosition = position;
            spawnRotation = rotation;
        }

        yVelocity = 0f;
        externalVelocity = Vector3.zero;
        externalVelocityTimer = 0f;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yRotation = transform.eulerAngles.y;
        xRotation = thirdPerson ? thirdPersonPitch : 0f;
        ApplyCameraPose();
    }

    public void SetVerticalPositionLock(bool locked, float worldY = 0f)
    {
        verticalPositionLocked = locked;
        if (locked)
            lockedVerticalPosition = worldY;

        yVelocity = 0f;
    }

    public void ResetToFreshStart()
    {
        ResetToFreshStart(DefaultFreshStartPosition, DefaultFreshStartRotation);
    }

    public void PrepareFreshStartForWorldGeneration()
    {
        ResetToFreshStart(DefaultFreshStartPosition, DefaultFreshStartRotation, false);
    }

    public void ResetToFreshStart(Vector3 desiredPosition, Quaternion rotation)
    {
        ResetToFreshStart(desiredPosition, rotation, true);
    }

    void ResetToFreshStart(Vector3 desiredPosition, Quaternion rotation, bool resolveGround)
    {
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
        GameState.IsVendorOpen = false;
        GameState.IsCraftingOpen = false;
        GameState.IsDebugChatOpen = false;
        GameState.IsBestiaryOpen = false;
        GameState.IsQuestJournalOpen = false;
        GameState.IsMapOpen = false;
        GameState.IsPowerSelectionOpen = false;

        AncestralPowerService powers = GetComponent<AncestralPowerService>();
        if (powers != null)
            powers.ClearPowers(false);

        verticalPositionLocked = false;

        if (resolveGround)
        {
            ApplySavedState(
                desiredPosition,
                rotation,
                false,
                maxHealth,
                maxStamina,
                maxHunger,
                maxThirst
            );
        }
        else
        {
            thirdPerson = false;

            if (controller != null)
                controller.enabled = false;

            transform.SetPositionAndRotation(desiredPosition, rotation);

            if (controller != null)
                controller.enabled = true;

            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentHunger = maxHunger;
            currentThirst = maxThirst;
            spawnPosition = desiredPosition;
            spawnRotation = rotation;
            yRotation = transform.eulerAngles.y;
            xRotation = 0f;
            ApplyCameraPose();
        }

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
        if (GameState.IsPlayerDead || GameState.IsPowerSelectionOpen)
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

    public void SetRangedAimMovement(bool active, float speedMultiplier)
    {
        rangedAimActive = active;
        rangedAimMultiplier = Mathf.Clamp(speedMultiplier, 0.2f, 1f);
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
        if (GameState.IsPlayerDead || GameState.IsPowerSelectionOpen)
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
        if (GameState.IsPlayerDead || GameState.IsPaused || GameState.IsInLobby || GameState.IsWorldLoading || GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen)
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
        if (GameState.IsPlayerDead || GameState.IsWorldLoading || GameState.IsPowerSelectionOpen)
            return;

        if (debugImmortal)
            return;

        if (Time.time < respawnInvulnerabilityEndTime)
            return;

        AncestralPowerService powers = GetComponent<AncestralPowerService>();
        if (powers != null && powers.TryIgnoreIncomingDamage())
        {
            MessageSystem.Instance?.ShowMessage("Guardiao Imortal bloqueou o dano.");
            return;
        }

        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        if (equipment != null)
            amount = Mathf.Max(1f, amount - equipment.GetTotalDefense() * 0.35f);

        if (powers != null && powers.BonusDefense > 0f)
            amount = Mathf.Max(1f, amount - powers.BonusDefense * 0.35f);

        BearerPowerController bearerController = GetComponent<BearerPowerController>();
        if (bearerController != null)
            amount = bearerController.ModifyIncomingDamage(amount);

        lastDamageTime = Time.time;
        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        externalVelocity = direction.normalized * Mathf.Max(0f, force);
        externalVelocityTimer = Mathf.Max(0.05f, duration);
    }

    public bool IsStunned => Time.time < stunEndTime;

    public void ApplyStun(float duration)
    {
        if (duration <= 0f || GameState.IsPlayerDead)
            return;

        stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);
        moveInput = Vector2.zero;
        isRunning = false;
    }

    public void MoveByAbility(Vector3 displacement)
    {
        if (controller == null || !controller.enabled || GameState.IsPlayerDead)
            return;

        controller.Move(displacement);
    }

    public void PlayAttackSound()
    {
        PlaySfx(attackSound, attackSoundVolume, attackSoundDuration);
    }

    public void PlayDeathSound()
    {
        PlaySfx(deathSound, deathSoundVolume);
    }

    public void PlayUiSound(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;

        EnsureSfxSource();

        if (sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void RegisterBossOrMiniBossCombat(float duration = -1f)
    {
        float holdDuration = duration > 0f ? duration : combatMusicHoldDuration;
        combatMusicUntilTime = Mathf.Max(combatMusicUntilTime, Time.unscaledTime + Mathf.Max(0.1f, holdDuration));
        LoadCombatMusicClip();
    }

    public void OverrideVisualAnimator(Animator replacementAnimator)
    {
        if (replacementAnimator == null)
            return;

        anim = replacementAnimator;
        playerModel = replacementAnimator.gameObject;
        playerModelStartLocalPosition = playerModel.transform.localPosition;
        HandleModelVisibility();
        ApplyWaterVisuals();
    }

    void Die()
    {
        PlayerAnimationBridge.Trigger(anim, PlayerAnimationBridge.DieTrigger);
        GameState.IsPlayerDead = true;
        GameState.IsInventoryOpen = false;
        GameState.IsVendorOpen = false;
        GameState.IsCraftingOpen = false;
        GameState.IsBestiaryOpen = false;
        GameState.IsQuestJournalOpen = false;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isRunning = false;
        yVelocity = 0f;

        AncestralPowerService powers = GetComponent<AncestralPowerService>();
        if (powers != null)
            powers.ClearPowers(true);

        List<InventoryItem> droppedItems = inventory != null ? inventory.CreateSnapshot() : null;
        if (droppedItems != null && droppedItems.Count > 0)
            DeathLoot.Spawn(transform.position, droppedItems);

        inventory?.ClearAll();
        hotbar?.ClearAll();

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        PlayDeathSound();
        ShowDeathMessage();

    }

    void EnsureSfxSource()
    {
        if (sfxSource != null)
            return;

        sfxSource = GetComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
    }

    void EnsureCombatMusicSource()
    {
        if (combatMusicSource != null)
            return;

        AudioSource[] audioSources = GetComponents<AudioSource>();
        AudioSource magicSource = GetComponent<PlayerMagic>()?.magicSfxSource;

        foreach (AudioSource audioSource in audioSources)
        {
            if (audioSource == null || audioSource == sfxSource || audioSource == magicSource)
                continue;

            combatMusicSource = audioSource;
            break;
        }

        if (combatMusicSource == null)
            combatMusicSource = gameObject.AddComponent<AudioSource>();

        combatMusicSource.playOnAwake = false;
        combatMusicSource.loop = true;
        combatMusicSource.spatialBlend = 0f;
        combatMusicSource.volume = 0f;
    }

    void LoadCombatMusicClip()
    {
        if (combatMusicClip != null)
            return;

        combatMusicClip = Resources.Load<AudioClip>(CombatMusicResourcePath);
    }

    void HandleCombatMusic()
    {
        EnsureCombatMusicSource();
        LoadCombatMusicClip();

        if (combatMusicSource == null || combatMusicClip == null)
            return;

        bool shouldPlayCombatMusic =
            !GameState.IsInLobby &&
            !GameState.IsWorldLoading &&
            !GameState.IsPaused &&
            !GameState.IsPlayerDead &&
            Time.unscaledTime < combatMusicUntilTime;

        IsCombatMusicActive = shouldPlayCombatMusic;

        if (combatMusicSource.clip != combatMusicClip)
            combatMusicSource.clip = combatMusicClip;

        float fadeStep = Mathf.Max(0.01f, combatMusicFadeSpeed) * Time.unscaledDeltaTime;
        float targetVolume = shouldPlayCombatMusic ? Mathf.Clamp01(combatMusicVolume) : 0f;

        if (shouldPlayCombatMusic && !combatMusicSource.isPlaying)
            combatMusicSource.Play();

        combatMusicSource.volume = Mathf.MoveTowards(combatMusicSource.volume, targetVolume, fadeStep);

        if (!shouldPlayCombatMusic && combatMusicSource.isPlaying && combatMusicSource.volume <= 0.001f)
            combatMusicSource.Stop();

        UpdateAmbientMusicPauseState(shouldPlayCombatMusic);
    }

    void UpdateAmbientMusicPauseState(bool shouldPauseAmbientMusic)
    {
        if (shouldPauseAmbientMusic)
        {
            if (!ambientMusicPausedForCombat)
                PauseAmbientMusicSources();

            return;
        }

        if (ambientMusicPausedForCombat)
            ResumeAmbientMusicSources();
    }

    void PauseAmbientMusicSources()
    {
        pausedAmbientMusicSources.Clear();

        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource audioSource in audioSources)
        {
            if (!IsSceneAmbientMusicSource(audioSource) || !audioSource.isPlaying)
                continue;

            audioSource.Pause();
            pausedAmbientMusicSources.Add(audioSource);
        }

        ambientMusicPausedForCombat = true;
    }

    void ResumeAmbientMusicSources()
    {
        for (int i = 0; i < pausedAmbientMusicSources.Count; i++)
        {
            AudioSource audioSource = pausedAmbientMusicSources[i];
            if (audioSource == null)
                continue;

            audioSource.UnPause();
        }

        pausedAmbientMusicSources.Clear();
        ambientMusicPausedForCombat = false;
    }

    bool IsSceneAmbientMusicSource(AudioSource audioSource)
    {
        if (audioSource == null || audioSource.clip == null)
            return false;

        if (audioSource == sfxSource || audioSource == combatMusicSource)
            return false;

        PlayerMagic playerMagic = GetComponent<PlayerMagic>();
        if (playerMagic != null && audioSource == playerMagic.magicSfxSource)
            return false;

        return string.Equals(audioSource.clip.name, AmbientMusicClipName, System.StringComparison.OrdinalIgnoreCase);
    }

    void PlaySfx(AudioClip clip, float volume, float maxDuration = 0f)
    {
        if (clip == null)
            return;

        EnsureSfxSource();

        if (sfxSource == null)
            return;

        if (stopSfxCoroutine != null)
        {
            StopCoroutine(stopSfxCoroutine);
            stopSfxCoroutine = null;
        }

        sfxSource.Stop();
        sfxSource.clip = clip;
        sfxSource.volume = Mathf.Clamp01(volume);
        sfxSource.loop = false;
        sfxSource.time = 0f;
        sfxSource.Play();

        float duration = maxDuration > 0f ? Mathf.Min(maxDuration, clip.length) : clip.length;
        stopSfxCoroutine = StartCoroutine(StopSfxAfterDelay(duration));
    }

    IEnumerator StopSfxAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (sfxSource != null && sfxSource.isPlaying)
            sfxSource.Stop();

        stopSfxCoroutine = null;
    }

    void Respawn()
    {
        GameState.IsPlayerDead = false;
        GameState.IsInventoryOpen = false;
        GameState.IsVendorOpen = false;
        GameState.IsCraftingOpen = false;
        GameState.IsBestiaryOpen = false;
        GameState.IsQuestJournalOpen = false;

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
        bool canRun = currentStamina > 0.01f && !sprintLocked && !IsRunningBlockedByInventoryWeight();

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
        if (verticalPositionLocked)
        {
            yVelocity = 0f;
        }
        else
        {
            bool grounded = controller.isGrounded;

            if (grounded && yVelocity < 0f)
                yVelocity = -2f;

            if (grounded)
                fallAnimationPlayed = false;

            if (grounded && !IsStunned && controls.Player.Jump.WasPressedThisFrame())
            {
                yVelocity = jumpForce;
                bool runningJump = isRunning &&
                                   moveInput.sqrMagnitude > 0.01f &&
                                   currentStamina > 0.01f &&
                                   !sprintLocked &&
                                   !IsRunningBlockedByInventoryWeight();
                PlayerAnimationBridge.Trigger(anim, runningJump ? PlayerAnimationBridge.RunJumpTrigger : PlayerAnimationBridge.JumpTrigger);
            }
            else if (!grounded && yVelocity < -3f && !fallAnimationPlayed)
            {
                fallAnimationPlayed = true;
                PlayerAnimationBridge.Trigger(anim, PlayerAnimationBridge.FallTrigger);
            }

            yVelocity += gravity * Time.deltaTime;
        }

        Quaternion yawRotationOnly = Quaternion.Euler(0f, yRotation, 0f);
        Vector3 forward = yawRotationOnly * Vector3.forward;
        Vector3 right = yawRotationOnly * Vector3.right;
        Vector3 move = (forward * moveInput.y) + (right * moveInput.x);

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool canRun = currentStamina > 0.01f && !sprintLocked && !IsRunningBlockedByInventoryWeight();
        float speed = (isRunning && isMoving && canRun) ? runSpeed : walkSpeed;

        if (isInWater)
            speed *= isRunning ? waterSprintMultiplier : waterMovementMultiplier;

        if (currentStamina <= maxStamina * lowStaminaThresholdPercent)
            speed *= lowStaminaSpeedMultiplier;

        if (rangedAimActive)
            speed *= rangedAimMultiplier;

        speed *= GetInventoryWeightSpeedMultiplier();
        speed *= 1f + GetEquipmentMoveSpeedBonus();
        BearerPowerController bearerController = GetComponent<BearerPowerController>();
        if (bearerController != null)
            speed *= bearerController.MovementSpeedMultiplier;

        Vector3 velocity = move.normalized * speed;
        velocity += externalVelocity;
        velocity.y = verticalPositionLocked ? 0f : yVelocity;

        controller.Move(velocity * Time.deltaTime);

        if (verticalPositionLocked && Mathf.Abs(transform.position.y - lockedVerticalPosition) > 0.001f)
        {
            controller.enabled = false;
            Vector3 lockedPosition = transform.position;
            lockedPosition.y = lockedVerticalPosition;
            transform.position = lockedPosition;
            controller.enabled = true;
        }

        DecayExternalVelocity();

        if (anim != null)
        {
            float animSpeed = (isRunning && isMoving) ? 1f : moveInput.magnitude * 0.5f;
            anim.SetFloat(PlayerAnimationBridge.SpeedParameter, animSpeed);
            PlayerAnimationBridge.SetFloatIfPresent(anim, PlayerAnimationBridge.DirectionXParameter, moveInput.x);
        }
    }

    void DecayExternalVelocity()
    {
        if (externalVelocity.sqrMagnitude <= 0.001f)
        {
            externalVelocity = Vector3.zero;
            externalVelocityTimer = 0f;
            return;
        }

        externalVelocityTimer -= Time.deltaTime;
        if (externalVelocityTimer <= 0f)
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 28f * Time.deltaTime);
    }

    bool IsRunningBlockedByInventoryWeight()
    {
        Inventory inventory = GetComponent<Inventory>();
        return inventory != null && inventory.IsRunBlockedByWeight();
    }

    float GetInventoryWeightSpeedMultiplier()
    {
        Inventory inventory = GetComponent<Inventory>();
        return inventory != null ? inventory.GetMovementSpeedMultiplier() : 1f;
    }

    float GetEquipmentMoveSpeedBonus()
    {
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        return equipment != null ? equipment.GetMoveSpeedBonus() : 0f;
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
        EnsureCameraHolder();

        if (cameraHolder == null)
            return;

        Vector3 poseOffset = thirdPerson ? thirdPersonOffset : firstPersonOffset;
        if (isInWater)
            poseOffset.y -= cameraWaterSink;

        cameraHolder.localPosition = poseOffset;
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void EnsureCameraHolder()
    {
        if (cameraHolder != null &&
            cameraHolder.IsChildOf(transform) &&
            cameraHolder.GetComponentInChildren<Camera>(true) != null)
        {
            return;
        }

        Camera ownedCamera = GetComponentInChildren<Camera>(true);
        if (ownedCamera != null)
        {
            Transform parent = ownedCamera.transform.parent;
            cameraHolder = parent != null && parent.IsChildOf(transform)
                ? parent
                : ownedCamera.transform;
            return;
        }

        if (cameraHolder != null && !cameraHolder.IsChildOf(transform))
            cameraHolder.SetParent(transform, false);
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

        if (TryGetGroundedSpawnPosition(Vector3.zero, out groundedPosition))
            return groundedPosition;

        float emergencyLift = Mathf.Max(GetGroundedSpawnLift(), Mathf.Max(0.5f, spawnAirDropHeight));
        float emergencyHeight = Mathf.Max(
            desiredPosition.y + emergencyLift,
            transform.position.y + emergencyLift,
            spawnPosition.y + emergencyLift
        );

        return new Vector3(desiredPosition.x, emergencyHeight, desiredPosition.z);
    }

    bool TryGetGroundedSpawnPosition(Vector3 desiredPosition, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * spawnRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            spawnRayDistance,
            spawnGroundMask,
            QueryTriggerInteraction.Ignore
        );

        float closestTerrainDistance = float.MaxValue;
        float closestFallbackDistance = float.MaxValue;
        Vector3 terrainPosition = desiredPosition;
        Vector3 fallbackPosition = desiredPosition;
        groundedPosition = desiredPosition;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidSpawnGroundHit(hit))
                continue;

            Vector3 candidatePosition = hit.point + Vector3.up * GetGroundedSpawnLift();
            bool isProceduralTerrain =
                hit.collider.GetComponentInParent<TerrainChunk>() != null ||
                hit.collider.GetComponentInParent<ProceduralTerrain>() != null;

            if (isProceduralTerrain && hit.distance < closestTerrainDistance)
            {
                closestTerrainDistance = hit.distance;
                terrainPosition = candidatePosition;
            }
            else if (!isProceduralTerrain && hit.distance < closestFallbackDistance)
            {
                closestFallbackDistance = hit.distance;
                fallbackPosition = candidatePosition;
            }
        }

        if (closestTerrainDistance < float.MaxValue)
        {
            groundedPosition = terrainPosition;
            return true;
        }

        if (closestFallbackDistance < float.MaxValue)
        {
            groundedPosition = fallbackPosition;
            return true;
        }

        return false;
    }

    float GetGroundedSpawnLift()
    {
        if (controller != null)
        {
            float bottomOffset = controller.center.y - controller.height * 0.5f;
            return Mathf.Max(spawnGroundPadding, -bottomOffset + spawnGroundPadding);
        }

        return 1f + spawnGroundPadding;
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

        if (hitCollider.GetComponentInParent<DistantMountains>() != null)
            return false;

        if (hitCollider.GetComponentInParent<Cow>() != null)
            return false;

        if (hitCollider.GetComponentInParent<WildChicken>() != null)
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
