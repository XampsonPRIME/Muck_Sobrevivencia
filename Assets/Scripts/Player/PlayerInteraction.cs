using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    const string PickupSoundClipPath = "Audio/SFX/Coletando_algo_no_game";
    const float PickupSoundVolume = 0.12f;
    const string TreeHitSoundClipPath = "Audio/SFX/Batendo_na_arvore";
    const float TreeHitSoundVolume = 0.12f;
    const float DefaultTreeHitSoundStartOffset = 0.95f;
    const string RockHitSoundClipPath = "Audio/SFX/Batendo_em_pedra";
    const float RockHitSoundVolume = 0.20f;
    const float DefaultRockHitSoundStartOffset = 0.95f;
    const float HotbarScrollThreshold = 0.5f;
    static Material meleeImpactMaterial;
    static Material meleeCriticalImpactMaterial;

    public float interactDistance = 4f;
    public float interactRadius = 0.45f;
    public Transform cameraHolder;
    public bool invertHotbarScroll = false;

    public Inventory inventory;
    public Hotbar hotbar;

    public float hitRate = 0.5f;
    float nextHitTime = 0f;

    [Header("Combate Corpo a Corpo")]
    [Min(1)] public int unarmedDamage = 5;
    [Min(0f)] public float unarmedStaminaCost = 8f;
    [Min(0f)] public float lightWeaponStaminaCost = 10f;
    [Min(0f)] public float heavyWeaponStaminaCost = 15f;
    [Min(0.05f)] public float unarmedAttackCooldown = 0.6f;
    [Min(0.05f)] public float lightWeaponAttackCooldown = 0.8f;
    [Min(0.05f)] public float heavyWeaponAttackCooldown = 1.2f;
    [Min(0.2f)] public float meleeAttackRange = 2.2f;
    [Min(0.5f)] public float meleeAimAssistRange = 3.6f;
    [Min(0.1f)] public float meleeAttackRadius = 0.95f;
    [Min(0f)] public float meleeAttackHeight = 1.1f;
    [Range(-1f, 1f)] public float meleeForwardDotThreshold = -0.15f;
    public LayerMask meleeHitMask = ~0;
    [Range(0f, 1f)] public float criticalChance = 0.1f;
    [Min(1f)] public float criticalMultiplier = 2f;

    PlayerControls controls;
    InputAction[] hotbarActions;
    PlayerMovement playerMovement;

    public ToolType currentTool = ToolType.None;
    public int toolDamage = 1;

    [Header("Mao")]
    public GameObject axePrefab;
    public GameObject pickaxePrefab;
    public GameObject bottlePrefab;
    public GameObject silverAxePrefab;
    public GameObject silverPickaxePrefab;

    public Vector3 axeHandScale = new(0.39f, 0.39f, 0.39f);
    public Vector3 pickaxeHandScale = new(0.39f, 0.39f, 0.39f);

    GameObject currentEquippedObject;
    HotbarSlot selectedSlot;
    float consumeTimer;
    bool consumeAwaitingRelease;
    bool hotbarScrollReady = true;
    bool wasDeadLastFrame;
    bool starterItemsInitialized;
    AudioClip pickupSound;
    AudioSource pickupAudioSource;
    AudioClip treeHitSound;
    AudioSource treeHitAudioSource;
    AudioClip preparedTreeHitSound;
    float preparedTreeHitSoundOffset = -1f;
    AudioClip rockHitSound;
    AudioSource rockHitAudioSource;
    AudioClip preparedRockHitSound;
    BowCombatHUD bowHud;
    Camera bowCamera;
    Coroutine bowReleaseRoutine;
    float defaultBowCameraFov = 60f;
    float nextBowShotTime;
    bool bowAiming;
    bool hasDefaultBowCameraFov;
    [Header("Audio")]
    [Min(0f)] public float treeHitSoundStartOffset = DefaultTreeHitSoundStartOffset;
    [Min(0f)] public float rockHitSoundStartOffset = DefaultRockHitSoundStartOffset;

    [Header("Arco")]
    public int bowDamage = 15;
    public float bowShotCooldown = 0.48f;
    public float bowArrowSpeed = 32f;
    public float bowArrowRange = 40f;
    [Range(0.5f, 1f)] public float bowAimFovMultiplier = 0.82f;
    [Range(0.2f, 1f)] public float bowAimMovementMultiplier = 0.65f;

    [Header("Start Item")]
    public bool startWithAxe = true;
    public bool startWithPickaxe = true;
    public bool startWithBottle = true;

    void Awake()
    {
        controls = new PlayerControls();
        hotbarActions = new[]
        {
            new InputAction("Hotbar1", binding: "<Keyboard>/1"),
            new InputAction("Hotbar2", binding: "<Keyboard>/2"),
            new InputAction("Hotbar3", binding: "<Keyboard>/3"),
            new InputAction("Hotbar4", binding: "<Keyboard>/4"),
            new InputAction("Hotbar5", binding: "<Keyboard>/5"),
            new InputAction("Hotbar6", binding: "<Keyboard>/6"),
            new InputAction("Hotbar7", binding: "<Keyboard>/7"),
            new InputAction("Hotbar8", binding: "<Keyboard>/8")
        };

        EnsurePickupAudioSource();
        LoadPickupSoundIfNeeded();
        PreloadPickupSound();
        EnsureTreeHitAudioSource();
        LoadTreeHitSoundIfNeeded();
        PreloadTreeHitSound();
        EnsureRockHitAudioSource();
        LoadRockHitSoundIfNeeded();
        PreloadRockHitSound();
    }

    void OnEnable()
    {
        controls.Enable();

        foreach (InputAction action in hotbarActions)
            action.Enable();
    }

    void OnDisable()
    {
        ResetBowAimState(true);

        foreach (InputAction action in hotbarActions)
            action.Disable();

        controls.Disable();
    }

    void Start()
    {
        ResolveReferences();
        EnsureStarterItems();
    }

    void Update()
    {
        ResolveReferences();
        EnsureStarterItems();

        if (GameState.IsInLobby || GameState.IsWorldLoading)
        {
            consumeTimer = 0f;
            ResetBowAimState(false);
            return;
        }

        if (GameState.IsPaused)
        {
            consumeTimer = 0f;
            ResetBowAimState(false);
            return;
        }

        if (GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen || GameState.IsDemoGuideOpen)
        {
            consumeTimer = 0f;
            ResetBowAimState(false);
            return;
        }

        if (GameState.IsPlayerDead)
        {
            if (!wasDeadLastFrame)
            {
                wasDeadLastFrame = true;
                UnequipCurrentItem();
            }

            consumeTimer = 0f;
            ResetBowAimState(false);
            return;
        }

        if (GameState.IsPowerSelectionOpen)
        {
            consumeTimer = 0f;
            ResetBowAimState(false);

            if (controls.Player.Interact.WasPressedThisFrame())
                TryPickupFromRay();

            return;
        }

        if (wasDeadLastFrame)
        {
            wasDeadLastFrame = false;
            ReequipSelectedSlot();
        }

        if (GameState.IsInventoryOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen)
        {
            consumeAwaitingRelease = false;
            ResetBowAimState(false);
            return;
        }

        HandleHotbarSelection();

        if (IsBowEquipped())
        {
            HandleBowCombat();

            if (controls.Player.Interact.WasPressedThisFrame())
                TryPickupFromRay();

            return;
        }

        ResetBowAimState(false);
        HandleConsumableUse();

        bool bottleSelected = selectedSlot != null && selectedSlot.isBottle;
        if (selectedSlot != null && selectedSlot.isConsumable && !bottleSelected)
            return;

        if (controls.Player.Attack.WasPressedThisFrame())
            Attack();

        if (controls.Player.Interact.WasPressedThisFrame())
            TryPickupFromRay();
    }

    void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (hotbar == null)
            hotbar = SceneObjectCache.Find<Hotbar>(gameObject.scene, true);

        if (cameraHolder == null && playerMovement != null)
            cameraHolder = playerMovement.cameraHolder;

        if (cameraHolder == null && RuntimeCameraCache.Main != null)
            cameraHolder = RuntimeCameraCache.Main.transform;
    }

    void EnsureStarterItems()
    {
        if (starterItemsInitialized || inventory == null || hotbar == null)
            return;

        bool starterItemsAdded = false;
        if (inventory.items.Count == 0 && !HasAnyHotbarItems())
        {
            starterItemsAdded |= TryAddStarterItem(startWithAxe, axePrefab);
            starterItemsAdded |= TryAddStarterItem(startWithPickaxe, pickaxePrefab);
            starterItemsAdded |= TryAddStarterItem(startWithBottle, bottlePrefab);
        }

        starterItemsInitialized = true;
        SelectSlot(0);

        if (!starterItemsAdded || hotbar.slots == null)
            return;

        foreach (HotbarSlot slot in hotbar.slots)
        {
            if (slot != null && slot.isBottle)
                slot.SetBottleState(false);
        }
    }

    public void ResetStarterLoadout()
    {
        starterItemsInitialized = false;
        ResolveReferences();
        EnsureStarterItems();
    }

    bool TryAddStarterItem(bool shouldAdd, GameObject prefab)
    {
        if (!shouldAdd || prefab == null || inventory == null || hotbar == null)
            return false;

        Item item = prefab.GetComponent<Item>();
        if (item == null)
            return false;

        inventory.AddItem(item.itemName, 1, item);
        hotbar.AddItem(item.itemName, item.icon, item);
        return true;
    }

    bool HasAnyHotbarItems()
    {
        if (hotbar == null || hotbar.slots == null)
            return false;

        foreach (HotbarSlot slot in hotbar.slots)
        {
            if (slot != null && !slot.IsEmpty())
                return true;
        }

        return false;
    }

    void HandleConsumableUse()
    {
        bool isHoldingAttack = controls.Player.Attack.IsPressed();

        if (selectedSlot == null || selectedSlot.IsEmpty() || !selectedSlot.isConsumable)
        {
            consumeTimer = 0f;
            consumeAwaitingRelease = false;
            return;
        }

        if (!isHoldingAttack)
        {
            consumeTimer = 0f;
            consumeAwaitingRelease = false;
            return;
        }

        if (consumeAwaitingRelease)
            return;

        float consumeDuration = Mathf.Max(0.15f, selectedSlot.consumeHoldTime);
        consumeTimer += Time.deltaTime;

        if (consumeTimer < consumeDuration)
            return;

        consumeTimer = 0f;
        if (ConsumeSelectedItem())
            consumeAwaitingRelease = true;
    }

    bool ConsumeSelectedItem()
    {
        if (selectedSlot == null || selectedSlot.IsEmpty() || !selectedSlot.isConsumable)
            return false;

        string consumedItemName = selectedSlot.ItemName;
        int selectedAmount = selectedSlot.GetAmount();
        Item consumedItemData = selectedSlot.GetItemData();
        ConsumableItem consumable = consumedItemData != null ? consumedItemData.GetComponent<ConsumableItem>() : null;
        BottleItem bottle = consumedItemData != null ? consumedItemData.GetComponent<BottleItem>() : null;
        MagicSpellConsumable magicConsumable = consumedItemData != null ? consumedItemData.GetComponent<MagicSpellConsumable>() : null;
        bool isFilledBottle = selectedSlot.isBottle && selectedSlot.bottleIsFilled;
        Item replacementItem = bottle == null && consumable != null ? consumable.itemAfterConsume : null;

        if (bottle != null && !bottle.CanDrink(isFilledBottle))
        {
            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage("A garrafa esta vazia. Encha antes de beber.");

            return true;
        }

        if (playerMovement != null)
        {
            playerMovement.Heal(selectedSlot.healthRestore);
            playerMovement.RestoreHunger(selectedSlot.hungerRestore);
            playerMovement.RestoreThirst(selectedSlot.thirstRestore);
        }

        if (magicConsumable != null)
        {
            PlayerMagic playerMagic;

            if (playerMovement != null)
                playerMagic = playerMovement.GetComponent<PlayerMagic>() ?? playerMovement.gameObject.AddComponent<PlayerMagic>();
            else
                playerMagic = GetComponent<PlayerMagic>() ?? gameObject.AddComponent<PlayerMagic>();

            playerMagic.UnlockAreaMagic(magicConsumable.magicName);

            inventory?.RemoveItem(selectedSlot.ItemName, 1);
            selectedSlot.RemoveOne();

            if (selectedSlot.IsEmpty())
                UnequipCurrentItem();

            return true;
        }

        if (bottle != null)
        {
            selectedSlot.SetBottleState(false);
            inventory?.SetBottleState(selectedSlot.ItemName, true, false);

            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage($"Consumiu {consumedItemName}");

            return true;
        }

        inventory?.RemoveItem(selectedSlot.ItemName, 1);

        if (replacementItem != null && selectedAmount == 1)
        {
            inventory?.AddItem(replacementItem.itemName, 1, replacementItem);
            selectedSlot.SetItem(replacementItem.itemName, replacementItem.icon, replacementItem, 1);
            EquipConsumable(selectedSlot);
        }
        else
        {
            selectedSlot.RemoveOne();

            if (replacementItem != null)
            {
                inventory?.AddItem(replacementItem.itemName, 1, replacementItem);
                hotbar?.AddItem(replacementItem.itemName, replacementItem.icon, replacementItem);
            }
        }

        if (selectedSlot.IsEmpty())
            UnequipCurrentItem();

        if (MessageSystem.Instance != null)
            MessageSystem.Instance.ShowMessage($"Consumiu {consumedItemName}");

        return true;
    }

    bool IsBowEquipped()
    {
        return selectedSlot != null &&
               !selectedSlot.IsEmpty() &&
               selectedSlot.itemType == ItemType.Tool &&
               selectedSlot.toolType == ToolType.Bow;
    }

    void HandleBowCombat()
    {
        consumeTimer = 0f;
        bool shouldAim = Mouse.current != null && Mouse.current.rightButton.isPressed;
        SetBowAimState(shouldAim);
        UpdateBowHud();

        if (controls.Player.Attack.WasPressedThisFrame())
            TryShootBow();
    }

    void SetBowAimState(bool active)
    {
        bool canAim = active && IsBowEquipped() && !GameState.IsWorldLoading && !GameState.IsPaused && !GameState.IsPlayerDead && !GameState.IsInventoryOpen && !GameState.IsBestiaryOpen && !GameState.IsQuestJournalOpen;
        if (canAim && !bowAiming)
            BowCombatAudio.PlayDraw(transform.position);

        bowAiming = canAim;

        if (playerMovement != null)
            playerMovement.SetRangedAimMovement(bowAiming, bowAimMovementMultiplier);

        UpdateBowCameraFov();
        UpdateBowHud();
    }

    void ResetBowAimState(bool restoreFov)
    {
        bowAiming = false;

        if (playerMovement != null)
            playerMovement.SetRangedAimMovement(false, 1f);

        if (restoreFov && bowCamera != null && hasDefaultBowCameraFov)
            bowCamera.fieldOfView = defaultBowCameraFov;
        else
            UpdateBowCameraFov();

        UpdateBowHud();
    }

    void UpdateBowCameraFov()
    {
        Camera camera = ResolveBowCamera();
        if (camera == null || !hasDefaultBowCameraFov)
            return;

        float targetFov = bowAiming ? defaultBowCameraFov * bowAimFovMultiplier : defaultBowCameraFov;
        camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, targetFov, Time.deltaTime * 12f);
    }

    Camera ResolveBowCamera()
    {
        Camera candidate = null;

        if (cameraHolder != null)
            candidate = cameraHolder.GetComponentInChildren<Camera>(true);

        if (candidate == null)
            candidate = RuntimeCameraCache.Main;

        if (candidate == null)
            return null;

        if (bowCamera != candidate)
        {
            bowCamera = candidate;
            defaultBowCameraFov = bowCamera.fieldOfView;
            hasDefaultBowCameraFov = true;
        }

        return bowCamera;
    }

    bool TryShootBow()
    {
        if (Time.time < nextBowShotTime)
            return false;

        if (CountArrows() <= 0)
        {
            MessageSystem.Instance?.ShowMessage("Sem flechas.");
            nextBowShotTime = Time.time + 0.18f;
            return false;
        }

        if (!TryResolveBowShot(out Vector3 fireOrigin, out Vector3 shotDirection))
        {
            MessageSystem.Instance?.ShowMessage("Nao foi possivel mirar.");
            return false;
        }

        if (!ConsumeArrow())
            return false;

        nextBowShotTime = Time.time + Mathf.Max(0.08f, ApplyAncestralAttackCooldownBonus(bowShotCooldown));

        GameObject arrowObject = new GameObject("Flecha Projetil");
        arrowObject.transform.SetPositionAndRotation(fireOrigin, Quaternion.LookRotation(shotDirection, Vector3.up));
        BowProjectile projectile = arrowObject.AddComponent<BowProjectile>();
        projectile.Launch(playerMovement, shotDirection, CalculateAncestralProjectileDamage(bowDamage), bowArrowSpeed, bowArrowRange, ArrowEffectType.Basic);

        PlayBowReleaseFeedback();
        UpdateBowHud();
        return true;
    }

    bool ConsumeArrow()
    {
        if (inventory == null)
            return false;

        InventoryItem arrowStack = inventory.GetItem(ArrowItemRegistry.ItemName);
        if (arrowStack == null || arrowStack.quantity <= 0)
            return false;

        hotbar?.RemoveInventoryItem(arrowStack, 1);
        inventory.RemoveItem(arrowStack.itemName, 1);

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        return true;
    }

    int CountArrows()
    {
        if (inventory == null)
            return 0;

        InventoryItem arrows = inventory.GetItem(ArrowItemRegistry.ItemName);
        return arrows != null ? Mathf.Max(0, arrows.quantity) : 0;
    }

    bool TryResolveBowShot(out Vector3 fireOrigin, out Vector3 shotDirection)
    {
        Transform firePoint = currentEquippedObject != null ? currentEquippedObject.transform.Find("FirePoint") : null;
        Transform aimTransform = cameraHolder != null ? cameraHolder : transform;
        Camera camera = ResolveBowCamera();

        Vector3 aimOrigin = camera != null ? camera.transform.position : aimTransform.position;
        Vector3 aimForward = camera != null ? camera.transform.forward : aimTransform.forward;
        fireOrigin = firePoint != null
            ? firePoint.position
            : aimOrigin + aimForward * 0.55f + aimTransform.right * 0.16f - aimTransform.up * 0.08f;

        Vector3 aimTarget = aimOrigin + aimForward * bowArrowRange;
        RaycastHit[] hits = Physics.RaycastAll(aimOrigin, aimForward, bowArrowRange, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform))
                    continue;

                aimTarget = hits[i].point;
                break;
            }
        }

        shotDirection = aimTarget - fireOrigin;
        if (shotDirection.sqrMagnitude < 0.001f)
            shotDirection = aimForward;

        shotDirection.Normalize();
        return shotDirection.sqrMagnitude > 0.001f;
    }

    void PlayBowReleaseFeedback()
    {
        PlayerAnimationBridge.Trigger(GetPlayerAnimator(), PlayerAnimationBridge.AttackTrigger);

        BowCombatAudio.PlayRelease(transform.position);

        if (bowReleaseRoutine != null)
            StopCoroutine(bowReleaseRoutine);

        if (currentEquippedObject != null)
            bowReleaseRoutine = StartCoroutine(AnimateBowRelease(currentEquippedObject.transform));
    }

    IEnumerator AnimateBowRelease(Transform bowTransform)
    {
        if (bowTransform == null)
            yield break;

        Quaternion startRotation = bowTransform.localRotation;
        Quaternion recoilRotation = startRotation * Quaternion.Euler(-8f, 0f, 5f);
        float elapsed = 0f;
        const float duration = 0.16f;

        while (elapsed < duration && bowTransform != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curve = Mathf.Sin(t * Mathf.PI);
            bowTransform.localRotation = Quaternion.Slerp(startRotation, recoilRotation, curve);
            yield return null;
        }

        if (bowTransform != null)
            bowTransform.localRotation = startRotation;
    }

    void UpdateBowHud()
    {
        bool equipped = IsBowEquipped() && !GameState.IsWorldLoading && !GameState.IsInventoryOpen && !GameState.IsBestiaryOpen && !GameState.IsQuestJournalOpen && !GameState.IsCraftingOpen && !GameState.IsVendorOpen && !GameState.IsPaused && !GameState.IsPlayerDead;
        if (bowHud == null && !equipped)
            return;

        EnsureBowHud();

        if (bowHud == null)
            return;

        bowHud.SetState(equipped, bowAiming, CountArrows(), ArrowItemRegistry.GetSprite());
    }

    void EnsureBowHud()
    {
        if (bowHud != null || LanMultiplayerManager.IsDedicatedRuntime || LanMultiplayerManager.IsDedicatedProcessRequested)
            return;

        GameObject hudObject = new GameObject("BowCombatHUD");
        hudObject.transform.SetParent(transform, false);
        bowHud = hudObject.AddComponent<BowCombatHUD>();
    }

    void TryPickupFromRay()
    {
        if (!TryFindInteractionHit(out RaycastHit hit))
            return;

        IPlayerInteractable interactable = hit.collider.GetComponent<IPlayerInteractable>() ??
                                           hit.collider.GetComponentInParent<IPlayerInteractable>() ??
                                           hit.collider.GetComponentInChildren<IPlayerInteractable>();

        if (interactable != null && interactable.Interact(this))
            return;

        Door door = hit.collider.GetComponent<Door>() ??
                    hit.collider.GetComponentInParent<Door>();

        if (door != null)
        {
            door.ToggleDoor();
            return;
        }


        VendorShop vendorShop = hit.collider.GetComponent<VendorShop>() ??
                                hit.collider.GetComponentInParent<VendorShop>();

        if (vendorShop != null)
        {
            VendorShopUI.Instance?.Open(vendorShop, inventory, hotbar, playerMovement, this);
            return;
        }

        RiverWaterSource riverWater = hit.collider.GetComponent<RiverWaterSource>() ??
                                      hit.collider.GetComponentInParent<RiverWaterSource>();

        if (riverWater != null)
        {
            TryFillSelectedBottle();
            return;
        }

        DeathLoot deathLoot = hit.collider.GetComponent<DeathLoot>() ??
                              hit.collider.GetComponentInParent<DeathLoot>();

        if (deathLoot != null)
        {
            bool hadItems = deathLoot.HasItems;
            deathLoot.Collect(inventory, hotbar);

            InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
            if (inventoryUi != null)
                inventoryUi.Refresh();

            if (hadItems)
            {
                PlayPickupSound();
                PlayerAnimationBridge.Trigger(this, PlayerAnimationBridge.PickupTrigger);
            }

            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage("Recuperou seu loot");

            ReequipSelectedSlot();
            return;
        }

        MagicSpellPickup magicPickup = hit.collider.GetComponent<MagicSpellPickup>() ??
                                       hit.collider.GetComponentInParent<MagicSpellPickup>();

        if (magicPickup != null)
        {
            magicPickup.Collect(inventory, hotbar);

            InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
            if (inventoryUi != null)
                inventoryUi.Refresh();

            PlayPickupSound();
            PlayerAnimationBridge.Trigger(this, PlayerAnimationBridge.PickupTrigger);
            ReequipSelectedSlot();
            return;
        }

        Item item = hit.collider.GetComponent<Item>() ??
                    hit.collider.GetComponentInParent<Item>() ??
                    hit.collider.GetComponentInChildren<Item>();

        if (item != null)
            TryPickup(item);
    }

    public bool TryPickup(Item item)
    {
        PickupRespawner respawner = item.GetComponent<PickupRespawner>() ??
                                    item.GetComponentInParent<PickupRespawner>();

        if (respawner != null && !respawner.IsAvailable)
            return false;

        item.ApplyDefinition();
        string pickedItemName = item.itemName;
        Vector3 pickupMessagePosition = item.transform.position;
        Sprite pickupIcon = item.icon;
        if (inventory == null || string.IsNullOrWhiteSpace(pickedItemName))
            return false;

        if (!inventory.AddItem(pickedItemName, 1, item))
        {
            MessageSystem.Instance?.ShowMessage("Inventario cheio");
            return false;
        }

        if (item.itemType == ItemType.Tool || item.itemType == ItemType.Consumable)
            hotbar.AddItem(pickedItemName, item.icon, item);

        PickupMessageSystem.Show(pickedItemName, 1, pickupMessagePosition + Vector3.up * 0.55f, pickupIcon);

        PlayPickupSound();
        PlayerAnimationBridge.Trigger(this, PlayerAnimationBridge.PickupTrigger);

        if (respawner != null)
            respawner.Collect();
        else
            Destroy(item.gameObject);

        return true;
    }

    void Attack()
    {
        if (Time.time < nextHitTime || GameState.IsWorldLoading || GameState.IsInventoryOpen || GameState.IsBestiaryOpen || GameState.IsQuestJournalOpen || GameState.IsPaused || GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen)
            return;

        if (TryPlaceSelectedItem())
            return;

        bool hasInteractionHit = TryFindInteractionHit(out RaycastHit hit);

        if (hasInteractionHit && TryHandleRiverAttackInteraction(hit))
            return;

        BuildMeleeAttackData(out int baseDamage, out float staminaCost, out float cooldown, out ToolType attackTool);
        if (!TryConsumeAttackStamina(staminaCost))
        {
            nextHitTime = Time.time + 0.18f;
            return;
        }

        nextHitTime = Time.time + Mathf.Max(0.05f, cooldown);

        if (TryFindMeleeEnemyRayHit(out RaycastHit enemyRayHit) &&
            TryApplyMeleeEnemyHitFromRay(enemyRayHit, baseDamage, attackTool))
        {
            PlayMeleeAttackAnimation();
            return;
        }

        if (TryApplyMeleeEnemyHit(baseDamage, attackTool))
        {
            PlayMeleeAttackAnimation();
            return;
        }

        if (hasInteractionHit && TryHitResourceFromRay(hit))
            return;

        PlayMeleeAttackAnimation();
    }

    bool TryHandleRiverAttackInteraction(RaycastHit hit)
    {
        if (hit.collider == null)
            return false;

        RiverWaterSource riverWater = hit.collider.GetComponent<RiverWaterSource>() ??
                                      hit.collider.GetComponentInParent<RiverWaterSource>();

        if (riverWater == null)
            return false;

        TryFillSelectedBottle();
        nextHitTime = Time.time + hitRate;
        return true;
    }

    void BuildMeleeAttackData(out int baseDamage, out float staminaCost, out float cooldown, out ToolType attackTool)
    {
        attackTool = currentTool;
        bool hasMeleeWeapon = selectedSlot != null &&
                              !selectedSlot.IsEmpty() &&
                              selectedSlot.itemType == ItemType.Tool &&
                              selectedSlot.toolType != ToolType.None &&
                              selectedSlot.toolType != ToolType.Bow;

        if (!hasMeleeWeapon)
        {
            attackTool = ToolType.None;
            baseDamage = Mathf.Max(1, unarmedDamage);
            staminaCost = unarmedStaminaCost;
            cooldown = ApplyAncestralAttackCooldownBonus(unarmedAttackCooldown);
            return;
        }

        baseDamage = Mathf.Max(1, toolDamage);

        if (IsHeavyMeleeTool(attackTool))
        {
            staminaCost = heavyWeaponStaminaCost;
            cooldown = ApplyAncestralAttackCooldownBonus(heavyWeaponAttackCooldown);
            return;
        }

        staminaCost = lightWeaponStaminaCost;
        cooldown = ApplyAncestralAttackCooldownBonus(lightWeaponAttackCooldown);
    }

    float ApplyAncestralAttackCooldownBonus(float cooldown)
    {
        AncestralPowerService powers = playerMovement != null
            ? playerMovement.GetComponent<AncestralPowerService>()
            : GetComponent<AncestralPowerService>();

        return powers != null
            ? Mathf.Max(0.05f, cooldown * powers.AttackCooldownMultiplier)
            : cooldown;
    }

    int CalculateAncestralProjectileDamage(int baseDamage)
    {
        int finalDamage = Mathf.Max(1, baseDamage);
        AncestralPowerService powers = playerMovement != null
            ? playerMovement.GetComponent<AncestralPowerService>()
            : GetComponent<AncestralPowerService>();

        if (powers != null)
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * powers.GetOutgoingDamageMultiplier(false)));

        return finalDamage;
    }

    bool IsHeavyMeleeTool(ToolType toolType)
    {
        return toolType == ToolType.Axe || toolType == ToolType.Pickaxe;
    }

    bool TryConsumeAttackStamina(float staminaCost)
    {
        if (playerMovement == null || staminaCost <= 0f)
            return true;

        if (playerMovement.currentStamina + 0.01f < staminaCost)
        {
            MessageSystem.Instance?.ShowMessage("Stamina insuficiente para atacar.");
            return false;
        }

        playerMovement.currentStamina = Mathf.Max(0f, playerMovement.currentStamina - staminaCost);
        return true;
    }

    void PlayMeleeAttackAnimation()
    {
        PlayerAnimationBridge.Trigger(GetPlayerAnimator(), PlayerAnimationBridge.AttackTrigger);
    }

    bool TryApplyMeleeEnemyHit(int baseDamage, ToolType attackTool)
    {
        Vector3 origin = transform.position + Vector3.up * meleeAttackHeight;
        Vector3 forward = GetMeleeForward();
        Vector3 start = origin + forward * 0.15f;
        Vector3 end = origin + forward * meleeAttackRange;
        int hitMask = meleeHitMask.value == 0 ? ~0 : meleeHitMask.value;
        Collider[] hits = Physics.OverlapCapsule(start, end, meleeAttackRadius, hitMask, QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return false;

        List<Collider> orderedHits = new List<Collider>(hits);
        orderedHits.Sort((a, b) =>
        {
            float aDistance = GetColliderDistanceSquared(a, origin);
            float bDistance = GetColliderDistanceSquared(b, origin);
            return aDistance.CompareTo(bDistance);
        });

        for (int i = 0; i < orderedHits.Count; i++)
        {
            Collider candidate = orderedHits[i];
            if (candidate == null || candidate.transform.IsChildOf(transform))
                continue;

            Vector3 impactPoint = GetSafeClosestPoint(candidate, origin);
            if (!IsColliderInsideMeleeArc(impactPoint, origin, forward))
                continue;

            if (TryApplyMeleeDamageToCollider(candidate, baseDamage, attackTool, impactPoint))
                return true;
        }

        return false;
    }

    bool TryFindMeleeEnemyRayHit(out RaycastHit enemyHit)
    {
        enemyHit = default;

        if (!TryGetInteractionRay(out Ray ray))
            return false;

        float searchRadius = Mathf.Max(0.35f, interactRadius);
        float searchDistance = Mathf.Max(meleeAttackRange, meleeAimAssistRange) + meleeAttackRadius;
        int hitMask = meleeHitMask.value == 0 ? ~0 : meleeHitMask.value;
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            searchRadius,
            searchDistance,
            hitMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform))
                continue;

            if (!IsDamageableEnemyCollider(candidate))
                continue;

            enemyHit = hits[i];
            return true;
        }

        return false;
    }

    bool TryApplyMeleeEnemyHitFromRay(RaycastHit hit, int baseDamage, ToolType attackTool)
    {
        if (hit.collider == null || !IsDamageableEnemyCollider(hit.collider))
            return false;

        Vector3 origin = transform.position + Vector3.up * meleeAttackHeight;
        Vector3 forward = GetMeleeForward();
        Vector3 impactPoint = hit.point != Vector3.zero ? hit.point : GetSafeClosestPoint(hit.collider, origin);
        Vector3 closestPoint = GetSafeClosestPoint(hit.collider, origin);

        if (!IsColliderInsideMeleeArc(closestPoint, origin, forward, meleeAimAssistRange))
            return false;

        return TryApplyMeleeDamageToCollider(hit.collider, baseDamage, attackTool, impactPoint);
    }

    bool IsDamageableEnemyCollider(Collider candidate)
    {
        if (candidate == null)
            return false;

        return candidate.GetComponentInParent<MiniKrug>() != null ||
               candidate.GetComponentInParent<BossEnemy>() != null ||
               candidate.GetComponentInParent<EarthGolem>() != null ||
               candidate.GetComponentInParent<WildBoar>() != null ||
               candidate.GetComponentInParent<Cow>() != null ||
               candidate.GetComponentInParent<WildChicken>() != null;
    }

    float GetColliderDistanceSquared(Collider candidate, Vector3 origin)
    {
        if (candidate == null)
            return float.MaxValue;

        Vector3 closestPoint = GetSafeClosestPoint(candidate, origin);
        return (closestPoint - origin).sqrMagnitude;
    }

    Vector3 GetSafeClosestPoint(Collider candidate, Vector3 origin)
    {
        if (candidate == null)
            return origin;

        MeshCollider meshCollider = candidate as MeshCollider;
        if (meshCollider != null && !meshCollider.convex)
            return candidate.bounds.ClosestPoint(origin);

        return candidate.ClosestPoint(origin);
    }

    Vector3 GetMeleeForward()
    {
        Transform source = cameraHolder != null ? cameraHolder : transform;
        Vector3 forward = source.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
            return Vector3.forward;

        return forward.normalized;
    }

    bool IsColliderInsideMeleeArc(Vector3 impactPoint, Vector3 origin, Vector3 forward)
    {
        return IsColliderInsideMeleeArc(impactPoint, origin, forward, meleeAttackRange);
    }

    bool IsColliderInsideMeleeArc(Vector3 impactPoint, Vector3 origin, Vector3 forward, float range)
    {
        Vector3 toTarget = impactPoint - origin;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.01f)
            return true;

        float maxRange = Mathf.Max(0.2f, range) + 0.2f;
        if (toTarget.sqrMagnitude > maxRange * maxRange)
            return false;

        return Vector3.Dot(forward, toTarget.normalized) >= meleeForwardDotThreshold;
    }

    bool TryApplyMeleeDamageToCollider(Collider hitCollider, int baseDamage, ToolType attackTool, Vector3 impactPoint)
    {
        MiniKrug miniKrug = hitCollider.GetComponent<MiniKrug>() ??
                            hitCollider.GetComponentInParent<MiniKrug>();

        if (miniKrug != null)
        {
            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical);
            playerMovement?.RegisterBossOrMiniBossCombat();
            PlayMeleeHitFeedback(impactPoint, critical);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(miniKrug, playerMovement, attackTool, finalDamage))
                return true;

            miniKrug.Hit(finalDamage, playerMovement);
            return true;
        }

        BossEnemy bossEnemy = hitCollider.GetComponent<BossEnemy>() ??
                              hitCollider.GetComponentInParent<BossEnemy>();

        if (bossEnemy != null)
        {
            if (!bossEnemy.CanBeChallengedBy(playerMovement))
            {
                MessageSystem.Instance?.ShowMessage(bossEnemy.BuildMinimumLevelMessage());
                return true;
            }

            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical);
            playerMovement?.RegisterBossOrMiniBossCombat();
            PlayMeleeHitFeedback(impactPoint, critical);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(bossEnemy, playerMovement, attackTool, finalDamage))
                return true;

            bossEnemy.Hit(finalDamage, playerMovement);
            return true;
        }

        EarthGolem earthGolem = hitCollider.GetComponent<EarthGolem>() ??
                                hitCollider.GetComponentInParent<EarthGolem>();

        if (earthGolem != null)
        {
            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical);
            playerMovement?.RegisterBossOrMiniBossCombat();
            PlayMeleeHitFeedback(impactPoint, critical);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(earthGolem, playerMovement, attackTool, finalDamage))
                return true;

            earthGolem.Hit(finalDamage, playerMovement);
            return true;
        }

        KaelTorGuardian kaelTor = hitCollider.GetComponent<KaelTorGuardian>() ??
                                  hitCollider.GetComponentInParent<KaelTorGuardian>();

        if (kaelTor != null)
        {
            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical);
            playerMovement?.RegisterBossOrMiniBossCombat();
            PlayMeleeHitFeedback(impactPoint, critical);
            kaelTor.Hit(finalDamage, playerMovement);
            return true;
        }

        WildBoar boar = hitCollider.GetComponent<WildBoar>() ??
                        hitCollider.GetComponentInParent<WildBoar>();

        if (boar != null)
        {
            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical, true);
            PlayMeleeHitFeedback(impactPoint, critical);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(boar, playerMovement, attackTool, finalDamage))
                return true;

            boar.Hit(finalDamage, playerMovement);
            return true;
        }

        Cow cow = hitCollider.GetComponent<Cow>() ??
                  hitCollider.GetComponentInParent<Cow>();

        if (cow != null)
        {
            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical, true);
            PlayMeleeHitFeedback(impactPoint, critical);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(cow, playerMovement, attackTool, finalDamage))
                return true;

            cow.Hit(finalDamage);
            return true;
        }

        WildChicken chicken = hitCollider.GetComponent<WildChicken>() ??
                              hitCollider.GetComponentInParent<WildChicken>();

        if (chicken != null)
        {
            int finalDamage = CalculateMeleeDamage(baseDamage, out bool critical, true);
            PlayMeleeHitFeedback(impactPoint, critical);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(chicken, playerMovement, attackTool, finalDamage))
                return true;

            Vector3 threatPosition = playerMovement != null ? playerMovement.transform.position : transform.position;
            chicken.Hit(finalDamage, threatPosition);
            return true;
        }

        return false;
    }

    int CalculateMeleeDamage(int baseDamage, out bool critical, bool animalTarget = false)
    {
        int finalDamage = Mathf.Max(1, baseDamage);
        critical = Random.value < criticalChance;

        if (critical)
            finalDamage = Mathf.Max(finalDamage + 1, Mathf.RoundToInt(finalDamage * criticalMultiplier));

        AncestralPowerService powers = playerMovement != null
            ? playerMovement.GetComponent<AncestralPowerService>()
            : GetComponent<AncestralPowerService>();

        if (powers != null)
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * powers.GetOutgoingDamageMultiplier(animalTarget)));

        return finalDamage;
    }

    void PlayMeleeHitFeedback(Vector3 impactPoint, bool critical)
    {
        playerMovement?.PlayAttackSound();
        BowCombatAudio.PlayImpact(impactPoint, true);
        CreateMeleeImpactVisual(impactPoint, critical);

        if (critical)
            ShowFloatingCombatText(impactPoint + Vector3.up * 0.55f, "CRITICO", new Color(1f, 0.84f, 0.12f, 1f));
    }

    void CreateMeleeImpactVisual(Vector3 impactPoint, bool critical)
    {
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = critical ? "MeleeCriticalImpact" : "MeleeImpact";
        effect.transform.position = impactPoint;
        effect.transform.localScale = Vector3.one * (critical ? 0.28f : 0.18f);

        Collider collider = effect.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = effect.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = critical
                ? GetMeleeCriticalImpactMaterial()
                : GetMeleeImpactMaterial();
        }

        Destroy(effect, critical ? 0.34f : 0.24f);
    }

    static Material GetMeleeImpactMaterial()
    {
        return meleeImpactMaterial ??= RuntimeMaterialUtility.Create("MeleeImpact", new Color(1f, 0.28f, 0.16f, 0.85f));
    }

    static Material GetMeleeCriticalImpactMaterial()
    {
        return meleeCriticalImpactMaterial ??= RuntimeMaterialUtility.Create("MeleeCriticalImpact", new Color(1f, 0.84f, 0.12f, 0.95f));
    }

    void ShowFloatingCombatText(Vector3 position, string message, Color color)
    {
        if (LanMultiplayerManager.IsDedicatedRuntime || LanMultiplayerManager.IsDedicatedProcessRequested)
            return;

        GameObject textObject = new GameObject($"CombatText_{message}");
        textObject.transform.position = position;

        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = 0.55f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0.08f, 0.02f, 0f, 1f);

        CombatFloatingText floatingText = textObject.AddComponent<CombatFloatingText>();
        floatingText.Initialize(0.85f, 1.15f);
    }

    bool TryHitResourceFromRay(RaycastHit hit)
    {
        if (hit.collider == null)
            return false;

        ResourceNode resource = hit.collider.GetComponent<ResourceNode>() ??
                                hit.collider.GetComponentInParent<ResourceNode>();

        if (resource != null)
        {
            bool shouldPlayTreeHitSound = ShouldPlayTreeHitSound(resource);
            bool shouldPlayRockHitSound = ShouldPlayRockHitSound(resource);
            PlayResourceHitAnimation(shouldPlayTreeHitSound, shouldPlayRockHitSound);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(resource, playerMovement, currentTool, toolDamage))
            {
                if (shouldPlayTreeHitSound)
                    PlayTreeImpactSound();
                if (shouldPlayRockHitSound)
                    PlayRockHitSound();

                return true;
            }

            resource.Hit(inventory, hotbar, currentTool, toolDamage);

            if (shouldPlayTreeHitSound)
                PlayTreeImpactSound();
            if (shouldPlayRockHitSound)
                PlayRockHitSound();

            return true;
        }

        return false;
    }

    void PlayResourceHitAnimation(bool tree, bool rock)
    {
        if (tree)
        {
            PlayerAnimationBridge.Trigger(this, PlayerAnimationBridge.CutWoodTrigger);
            return;
        }

        if (rock)
            PlayerAnimationBridge.Trigger(this, PlayerAnimationBridge.MineTrigger);
    }

    bool TryPlaceSelectedItem()
    {
        if (selectedSlot == null || selectedSlot.IsEmpty())
            return false;

        Item selectedItem = selectedSlot.GetItemData();
        PlaceableItem placeable = selectedItem != null ? selectedItem.GetComponent<PlaceableItem>() : null;
        if (placeable == null)
            return false;

        if (!TryFindPlacementPoint(out Vector3 placePoint, out Vector3 placeNormal))
        {
            MessageSystem.Instance?.ShowMessage("Aponte para o chao para colocar.");
            return true;
        }

        if (Vector3.Angle(placeNormal, Vector3.up) > 38f)
        {
            MessageSystem.Instance?.ShowMessage("Lugar muito inclinado.");
            return true;
        }

        CreatePlacedFurnace(placePoint, placeable);

        inventory?.RemoveItem(selectedSlot.ItemName, 1);
        selectedSlot.RemoveOne();

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        if (selectedSlot.IsEmpty())
            UnequipCurrentItem();
        else
            ReequipSelectedSlot();

        MessageSystem.Instance?.ShowMessage("Fornalha colocada.");
        nextHitTime = Time.time + hitRate;
        return true;
    }

    bool TryFindPlacementPoint(out Vector3 placePoint, out Vector3 placeNormal)
    {
        placePoint = Vector3.zero;
        placeNormal = Vector3.up;

        if (!TryGetInteractionRay(out Ray ray))
            return false;

        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance + 2f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.collider.GetComponentInParent<ResourceNode>() != null ||
                hit.collider.GetComponentInParent<Cow>() != null ||
                hit.collider.GetComponentInParent<WildChicken>() != null ||
                hit.collider.GetComponentInParent<WildBoar>() != null ||
                hit.collider.GetComponentInParent<EarthGolem>() != null ||
                hit.collider.GetComponentInParent<MiniKrug>() != null ||
                hit.collider.GetComponentInParent<BossEnemy>() != null)
                continue;

            placePoint = hit.point;
            placeNormal = hit.normal;
            return true;
        }

        return false;
    }

    void CreatePlacedFurnace(Vector3 placePoint, PlaceableItem placeable)
    {
        GameObject furnace = new GameObject(string.IsNullOrWhiteSpace(placeable.placedObjectName) ? "Fornalha" : placeable.placedObjectName);
        furnace.transform.position = placePoint;

        Vector3 forward = cameraHolder != null ? cameraHolder.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        furnace.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        furnace.transform.localScale = placeable.placedScale;

        Material stoneMaterial = CreateRuntimeMaterial(new Color(0.28f, 0.26f, 0.22f, 1f));
        Material darkStoneMaterial = CreateRuntimeMaterial(new Color(0.12f, 0.11f, 0.1f, 1f));
        Material metalMaterial = CreateRuntimeMaterial(new Color(0.08f, 0.07f, 0.06f, 1f));
        Material fireMaterial = CreateRuntimeMaterial(new Color(1f, 0.42f, 0.03f, 1f));
        Material emberMaterial = CreateRuntimeMaterial(new Color(1f, 0.86f, 0.12f, 1f));

        CreateFurnaceBlock(furnace.transform, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(1.7f, 0.35f, 1.35f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "BackWall", new Vector3(0f, 0.82f, 0.52f), new Vector3(1.55f, 1.1f, 0.22f), darkStoneMaterial);
        CreateFurnaceBlock(furnace.transform, "LeftWall", new Vector3(-0.72f, 0.82f, 0f), new Vector3(0.22f, 1.1f, 1.15f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "RightWall", new Vector3(0.72f, 0.82f, 0f), new Vector3(0.22f, 1.1f, 1.15f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "TopStone", new Vector3(0f, 1.4f, 0f), new Vector3(1.55f, 0.28f, 1.2f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "FrontTopArch", new Vector3(0f, 1.12f, -0.58f), new Vector3(1.45f, 0.28f, 0.2f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "FrontLeftArch", new Vector3(-0.5f, 0.65f, -0.58f), new Vector3(0.26f, 0.72f, 0.2f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "FrontRightArch", new Vector3(0.5f, 0.65f, -0.58f), new Vector3(0.26f, 0.72f, 0.2f), stoneMaterial);
        CreateFurnaceBlock(furnace.transform, "FireboxBack", new Vector3(0f, 0.62f, -0.46f), new Vector3(0.8f, 0.65f, 0.08f), metalMaterial);
        CreateFurnaceBlock(furnace.transform, "AshDrawer", new Vector3(0f, 0.26f, -0.7f), new Vector3(0.65f, 0.22f, 0.12f), metalMaterial);

        CreateFurnaceCylinder(furnace.transform, "Chimney", new Vector3(0f, 1.78f, 0.05f), new Vector3(0.82f, 0.42f, 0.82f), stoneMaterial);
        CreateFurnaceCylinder(furnace.transform, "ChimneyMouth", new Vector3(0f, 2.04f, 0.05f), new Vector3(0.98f, 0.18f, 0.98f), darkStoneMaterial);
        CreateFurnaceBlock(furnace.transform, "ToolRail", new Vector3(0.88f, 0.98f, -0.05f), new Vector3(0.08f, 0.08f, 0.82f), metalMaterial);
        CreateFurnaceBlock(furnace.transform, "HangingToolA", new Vector3(0.94f, 0.58f, -0.22f), new Vector3(0.05f, 0.72f, 0.05f), metalMaterial);
        CreateFurnaceBlock(furnace.transform, "HangingToolB", new Vector3(0.94f, 0.55f, 0.2f), new Vector3(0.05f, 0.62f, 0.05f), metalMaterial);

        CreateFurnaceBlock(furnace.transform, "FireGlow", new Vector3(0f, 0.55f, -0.66f), new Vector3(0.44f, 0.34f, 0.08f), fireMaterial);
        CreateFurnaceBlock(furnace.transform, "FireCore", new Vector3(0.04f, 0.58f, -0.72f), new Vector3(0.18f, 0.42f, 0.06f), emberMaterial);
        CreateFurnaceBlock(furnace.transform, "Coal", new Vector3(-0.16f, 0.39f, -0.72f), new Vector3(0.18f, 0.08f, 0.08f), darkStoneMaterial);
        CreateFurnaceBlock(furnace.transform, "CoalSmall", new Vector3(0.18f, 0.38f, -0.72f), new Vector3(0.14f, 0.08f, 0.08f), darkStoneMaterial);

        Light fireLight = furnace.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.45f, 0.12f, 1f);
        fireLight.range = 3f;
        fireLight.intensity = 1.25f;

        BoxCollider collider = furnace.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.8f, 2.25f, 1.45f);
        collider.center = new Vector3(0f, 1.1f, 0f);

        furnace.AddComponent<PlacedFurnace>();
    }

    Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        return material;
    }

    GameObject CreateFurnaceBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;
        block.transform.localRotation = Quaternion.Euler(0f, Random.Range(-4f, 4f), Random.Range(-2f, 2f));

        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = material;

        Collider collider = block.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return block;
    }

    GameObject CreateFurnaceCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localScale = localScale;

        Renderer renderer = cylinder.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = material;

        Collider collider = cylinder.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return cylinder;
    }

    bool TryGetInteractionRay(out Ray ray)
    {
        if (cameraHolder == null)
        {
            ray = default;
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = cameraHolder.forward;
        origin += direction * 0.3f;

        ray = new Ray(origin, direction);
        return true;
    }

    bool TryFindInteractionHit(out RaycastHit bestHit)
    {
        bestHit = default;

        if (!TryGetInteractionRay(out Ray ray))
            return false;

        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            interactRadius,
            interactDistance,
            ~0,
            QueryTriggerInteraction.Collide
        );

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
                continue;

            RiverWaterSource riverWater = hit.collider.GetComponent<RiverWaterSource>() ??
                                          hit.collider.GetComponentInParent<RiverWaterSource>();
            if (riverWater != null)
            {
                bestHit = hit;
                return true;
            }
        }

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
                continue;

            bestHit = hit;
            return true;
        }

        return false;
    }

    void HandleHotbarSelection()
    {
        if (hotbar != null && hotbar.slots != null && hotbar.slots.Length > 0)
        {
            Vector2 scrollValue = Mouse.current != null ? Mouse.current.scroll.ReadValue() : Vector2.zero;
            if (Mathf.Abs(scrollValue.y) <= HotbarScrollThreshold)
                hotbarScrollReady = true;
            else if (hotbarScrollReady)
            {
                hotbarScrollReady = false;
                int direction = scrollValue.y > 0f ? -1 : 1;
                if (invertHotbarScroll)
                    direction *= -1;

                int currentIndex = 0;
                for (int i = 0; i < hotbar.slots.Length; i++)
                {
                    if (hotbar.slots[i] == selectedSlot)
                    {
                        currentIndex = i;
                        break;
                    }
                }

                int nextIndex = (currentIndex + direction + hotbar.slots.Length) % hotbar.slots.Length;
                SelectSlot(nextIndex);
                return;
            }
        }

        for (int i = 0; i < hotbarActions.Length; i++)
        {
            if (hotbarActions[i].WasPressedThisFrame())
            {
                SelectSlot(i);
                return;
            }
        }
    }

    public void RefreshEquippedVisuals()
    {
        ReequipSelectedSlot();
    }

    void ReequipSelectedSlot()
    {
        if (hotbar == null || hotbar.slots == null || hotbar.slots.Length == 0)
            return;

        if (selectedSlot != null)
        {
            for (int i = 0; i < hotbar.slots.Length; i++)
            {
                if (hotbar.slots[i] == selectedSlot)
                {
                    SelectSlot(i);
                    return;
                }
            }
        }

        SelectSlot(0);
    }

    void SelectSlot(int index)
    {
        if (hotbar == null || hotbar.slots.Length <= index)
            return;

        hotbar.SetSelectedIndex(index);

        foreach (HotbarSlot slot in hotbar.slots)
            slot.isSelected = false;

        selectedSlot = hotbar.slots[index];
        selectedSlot.isSelected = true;
        consumeTimer = 0f;
        consumeAwaitingRelease = false;

        if (selectedSlot.IsEmpty())
        {
            UnequipCurrentItem();
            return;
        }

        if (selectedSlot.itemType == ItemType.Tool)
        {
            EquipTool(selectedSlot.toolType, selectedSlot.toolDamage);
            return;
        }

        if (selectedSlot.itemType == ItemType.Consumable)
        {
            EquipConsumable(selectedSlot);
            return;
        }

        UnequipCurrentItem();
    }

    public void SelectSlotIndex(int index)
    {
        SelectSlot(index);
    }

    public void RefreshEquippedSelection()
    {
        ReequipSelectedSlot();
    }

    void UnequipCurrentItem()
    {
        ResetBowAimState(true);
        currentTool = ToolType.None;
        consumeTimer = 0f;

        if (currentEquippedObject != null)
        {
            Destroy(currentEquippedObject);
            currentEquippedObject = null;
        }
    }

    public void EquipTool(ToolType type, int damage)
    {
        currentTool = type;
        toolDamage = damage;
        consumeTimer = 0f;

        GameObject prefab = null;
        Vector3 handScale = Vector3.one;

        if (type == ToolType.Axe)
        {
            prefab = axePrefab;
            handScale = axeHandScale;
        }

        if (type == ToolType.Pickaxe)
        {
            prefab = pickaxePrefab;
            handScale = pickaxeHandScale;
        }

        if (type == ToolType.Sword)
        {
            EquipRuntimeSwordVisual();
            return;
        }

        if (type == ToolType.Bow)
        {
            EquipRuntimeBowVisual();
            UpdateBowHud();
            return;
        }

        if (prefab == null)
        {
            UnequipCurrentItem();
            return;
        }

        EquipObjectInHand(prefab, Vector3.zero, Vector3.zero, handScale, false);
    }

    void EquipRuntimeSwordVisual()
    {
        if (currentEquippedObject != null)
            Destroy(currentEquippedObject);

        Animator anim = GetPlayerAnimator();
        if (anim == null)
            return;

        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            return;

        GameObject sword = new GameObject("EspadaEnferrujada_EquippedVisual");
        SpriteRenderer renderer = sword.AddComponent<SpriteRenderer>();
        renderer.sprite = RustySwordItemRegistry.GetSprite();
        renderer.sortingOrder = 4;

        sword.transform.SetParent(hand, false);
        sword.transform.localPosition = new Vector3(0.03f, 0.04f, 0.02f);
        sword.transform.localRotation = Quaternion.Euler(15f, 0f, -45f);
        sword.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);

        currentEquippedObject = sword;
    }

    void EquipRuntimeBowVisual()
    {
        ResetBowAimState(true);

        if (currentEquippedObject != null)
            Destroy(currentEquippedObject);

        Animator anim = GetPlayerAnimator();
        if (anim == null)
            return;

        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            return;

        Material wood = RuntimeMaterialUtility.Create("EquippedBowWoodRuntime", new Color(0.46f, 0.25f, 0.09f, 1f));
        Material cord = RuntimeMaterialUtility.Create("EquippedBowStringRuntime", new Color(0.86f, 0.78f, 0.58f, 1f));
        Material grip = RuntimeMaterialUtility.Create("EquippedBowGripRuntime", new Color(0.22f, 0.13f, 0.07f, 1f));

        GameObject bow = new GameObject("ArcoSimples_EquippedVisual");
        bow.transform.SetParent(hand, false);
        bow.transform.localPosition = new Vector3(0.08f, 0.02f, 0.08f);
        bow.transform.localRotation = Quaternion.Euler(12f, -18f, -72f);
        bow.transform.localScale = new Vector3(0.58f, 0.58f, 0.58f);

        CreateBowPart("UpperLimb", bow.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.12f, 0.58f, 0.08f), Quaternion.Euler(0f, 0f, -18f), wood);
        CreateBowPart("LowerLimb", bow.transform, new Vector3(0f, -0.42f, 0f), new Vector3(0.12f, 0.58f, 0.08f), Quaternion.Euler(0f, 0f, 18f), wood);
        CreateBowPart("UpperTip", bow.transform, new Vector3(0.14f, 0.82f, 0f), new Vector3(0.1f, 0.24f, 0.08f), Quaternion.Euler(0f, 0f, -34f), wood);
        CreateBowPart("LowerTip", bow.transform, new Vector3(0.14f, -0.82f, 0f), new Vector3(0.1f, 0.24f, 0.08f), Quaternion.Euler(0f, 0f, 34f), wood);
        CreateBowPart("Grip", bow.transform, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.28f, 0.1f), Quaternion.identity, grip);
        CreateBowPart("StringUpper", bow.transform, new Vector3(0.28f, 0.42f, 0f), new Vector3(0.035f, 0.74f, 0.035f), Quaternion.Euler(0f, 0f, 8f), cord);
        CreateBowPart("StringLower", bow.transform, new Vector3(0.28f, -0.42f, 0f), new Vector3(0.035f, 0.74f, 0.035f), Quaternion.Euler(0f, 0f, -8f), cord);

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(bow.transform, false);
        firePoint.transform.localPosition = new Vector3(0.18f, 0f, 0.62f);
        firePoint.transform.localRotation = Quaternion.identity;

        currentEquippedObject = bow;
    }

    void CreateBowPart(string partName, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    void EquipConsumable(HotbarSlot slot)
    {
        currentTool = ToolType.None;
        toolDamage = 0;

        GameObject sourcePrefab = FindConsumablePrefab(slot);
        if (sourcePrefab == null)
        {
            UnequipCurrentItem();
            return;
        }

        EquipObjectInHand(
            sourcePrefab,
            slot.handLocalPosition,
            slot.handLocalEulerAngles,
            slot.handLocalScale,
            true
        );
    }

    public bool TryFillSelectedBottle()
    {
        if (selectedSlot == null || selectedSlot.IsEmpty())
        {
            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage("Selecione uma garrafa vazia.");

            return false;
        }

        Item selectedItem = selectedSlot.GetItemData();
        BottleItem bottle = selectedItem != null ? selectedItem.GetComponent<BottleItem>() : null;
        if (bottle == null)
        {
            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage("Selecione uma garrafa vazia.");

            return false;
        }

        if (selectedSlot.bottleIsFilled)
        {
            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage("A garrafa ja esta cheia.");

            return false;
        }

        selectedSlot.SetBottleState(true);
        inventory?.SetBottleState(selectedSlot.ItemName, false, true);
        EquipConsumable(selectedSlot);

        if (MessageSystem.Instance != null)
            MessageSystem.Instance.ShowMessage("Garrafa enchida");

        return true;
    }

    GameObject FindConsumablePrefab(HotbarSlot slot)
    {
        if (slot.itemData != null)
            return slot.itemData.gameObject;

        GameObject[] prefabs = Resources.FindObjectsOfTypeAll<GameObject>();

        if (!string.IsNullOrWhiteSpace(slot.prefabName))
        {
            foreach (GameObject prefab in prefabs)
            {
                if (prefab.name == slot.prefabName && prefab.GetComponent<Item>() != null)
                    return prefab;
            }
        }

        foreach (GameObject prefab in prefabs)
        {
            Item item = prefab.GetComponent<Item>();
            if (item != null && item.itemName == slot.ItemName)
                return prefab;
        }

        return null;
    }

    void EquipObjectInHand(
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        bool stripGameplayComponents)
    {
        if (currentEquippedObject != null)
            Destroy(currentEquippedObject);

        Animator anim = GetPlayerAnimator();
        if (anim == null)
        {
            Debug.LogError("Animator do jogador nao encontrado.");
            return;
        }

        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
        {
            Debug.LogError("Mao nao encontrada!");
            return;
        }

        currentEquippedObject = CreateEquippedVisual(prefab, stripGameplayComponents);
        if (currentEquippedObject == null)
        {
            Debug.LogError($"Nao foi possivel criar visual equipado para {prefab.name}.");
            return;
        }

        currentEquippedObject.transform.SetParent(hand, false);
        currentEquippedObject.transform.localPosition = localPosition;
        currentEquippedObject.transform.localRotation = Quaternion.Euler(localEulerAngles);
        currentEquippedObject.transform.localScale = localScale;
    }

    GameObject CreateEquippedVisual(GameObject source, bool stripGameplayComponents)
    {
        if (source == null)
            return null;

        GameObject visualRoot = new GameObject($"{source.name}_EquippedVisual");
        CopyVisualHierarchy(source.transform, visualRoot.transform, stripGameplayComponents);
        return visualRoot;
    }

    void CopyVisualHierarchy(Transform source, Transform destinationParent, bool stripGameplayComponents)
    {
        GameObject destination = new GameObject(source.gameObject.name);
        destination.transform.SetParent(destinationParent, false);
        destination.transform.localPosition = source.localPosition;
        destination.transform.localRotation = source.localRotation;
        destination.transform.localScale = source.localScale;
        destination.layer = source.gameObject.layer;
        destination.tag = source.gameObject.tag;

        MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
        if (sourceMeshFilter != null)
        {
            MeshFilter meshFilter = destination.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
        }

        MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
        if (sourceMeshRenderer != null)
        {
            MeshRenderer meshRenderer = destination.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;
            meshRenderer.shadowCastingMode = sourceMeshRenderer.shadowCastingMode;
            meshRenderer.receiveShadows = sourceMeshRenderer.receiveShadows;
            meshRenderer.lightProbeUsage = sourceMeshRenderer.lightProbeUsage;
            meshRenderer.reflectionProbeUsage = sourceMeshRenderer.reflectionProbeUsage;
        }

        SkinnedMeshRenderer sourceSkinnedMeshRenderer = source.GetComponent<SkinnedMeshRenderer>();
        if (sourceSkinnedMeshRenderer != null)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = destination.AddComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.sharedMesh = sourceSkinnedMeshRenderer.sharedMesh;
            skinnedMeshRenderer.sharedMaterials = sourceSkinnedMeshRenderer.sharedMaterials;
            skinnedMeshRenderer.shadowCastingMode = sourceSkinnedMeshRenderer.shadowCastingMode;
            skinnedMeshRenderer.receiveShadows = sourceSkinnedMeshRenderer.receiveShadows;
            skinnedMeshRenderer.lightProbeUsage = sourceSkinnedMeshRenderer.lightProbeUsage;
            skinnedMeshRenderer.reflectionProbeUsage = sourceSkinnedMeshRenderer.reflectionProbeUsage;
            skinnedMeshRenderer.updateWhenOffscreen = sourceSkinnedMeshRenderer.updateWhenOffscreen;
        }

        SpriteRenderer sourceSpriteRenderer = source.GetComponent<SpriteRenderer>();
        if (sourceSpriteRenderer != null)
        {
            SpriteRenderer spriteRenderer = destination.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sourceSpriteRenderer.sprite;
            spriteRenderer.color = sourceSpriteRenderer.color;
            spriteRenderer.flipX = sourceSpriteRenderer.flipX;
            spriteRenderer.flipY = sourceSpriteRenderer.flipY;
            spriteRenderer.sortingLayerID = sourceSpriteRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = sourceSpriteRenderer.sortingOrder;
            spriteRenderer.sharedMaterial = sourceSpriteRenderer.sharedMaterial;
        }

        if (!stripGameplayComponents)
        {
            TrailRenderer sourceTrailRenderer = source.GetComponent<TrailRenderer>();
            if (sourceTrailRenderer != null)
            {
                TrailRenderer trailRenderer = destination.AddComponent<TrailRenderer>();
                trailRenderer.sharedMaterial = sourceTrailRenderer.sharedMaterial;
                trailRenderer.time = sourceTrailRenderer.time;
                trailRenderer.startWidth = sourceTrailRenderer.startWidth;
                trailRenderer.endWidth = sourceTrailRenderer.endWidth;
                trailRenderer.widthMultiplier = sourceTrailRenderer.widthMultiplier;
                trailRenderer.colorGradient = sourceTrailRenderer.colorGradient;
            }
        }

        for (int i = 0; i < source.childCount; i++)
            CopyVisualHierarchy(source.GetChild(i), destination.transform, stripGameplayComponents);
    }

    Animator GetPlayerAnimator()
    {
        MeshyHeroRuntimeVisual runtimeVisual = GetComponentInChildren<MeshyHeroRuntimeVisual>(true);
        if (runtimeVisual != null && runtimeVisual.ActiveAnimator != null)
            return runtimeVisual.ActiveAnimator;

        return GetComponentInChildren<Animator>(true);
    }

    void EnsurePickupAudioSource()
    {
        if (pickupAudioSource != null)
            return;

        pickupAudioSource = gameObject.AddComponent<AudioSource>();
        pickupAudioSource.playOnAwake = false;
        pickupAudioSource.loop = false;
        pickupAudioSource.spatialBlend = 0f;
    }

    void LoadPickupSoundIfNeeded()
    {
        if (pickupSound == null)
            pickupSound = Resources.Load<AudioClip>(PickupSoundClipPath);
    }

    void PreloadPickupSound()
    {
        if (pickupSound != null)
            pickupSound.LoadAudioData();
    }

    void PlayPickupSound()
    {
        LoadPickupSoundIfNeeded();
        EnsurePickupAudioSource();

        if (pickupSound == null || pickupAudioSource == null)
            return;

        pickupAudioSource.PlayOneShot(pickupSound, PickupSoundVolume);
    }

    bool ShouldPlayTreeHitSound(ResourceNode resource)
    {
        if (resource == null)
            return false;

        if (!resource.CanBeHitBy(currentTool))
            return false;

        if (resource.requiredTool == ToolType.Axe)
            return true;

        return MatchesResourceKeyword(resource, "madeira", "arvore", "tree");
    }

    bool ShouldPlayRockHitSound(ResourceNode resource)
    {
        if (resource == null)
            return false;

        if (!resource.CanBeHitBy(currentTool))
            return false;

        if (resource.requiredTool == ToolType.Pickaxe)
            return true;

        return MatchesResourceKeyword(resource, "pedra", "rock", "stone");
    }

    bool MatchesResourceKeyword(ResourceNode resource, params string[] keywords)
    {
        if (resource == null || keywords == null || keywords.Length == 0)
            return false;

        string itemName = resource.itemName ?? string.Empty;
        string objectName = resource.gameObject.name ?? string.Empty;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (itemName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (objectName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    void EnsureTreeHitAudioSource()
    {
        if (treeHitAudioSource != null)
            return;

        treeHitAudioSource = gameObject.AddComponent<AudioSource>();
        treeHitAudioSource.playOnAwake = false;
        treeHitAudioSource.loop = false;
        treeHitAudioSource.spatialBlend = 0f;
    }

    void LoadTreeHitSoundIfNeeded()
    {
        if (treeHitSound == null)
            treeHitSound = Resources.Load<AudioClip>(TreeHitSoundClipPath);
    }

    void PreloadTreeHitSound()
    {
        if (treeHitSound != null)
            treeHitSound.LoadAudioData();
    }

    void PlayTreeHitSound()
    {
        LoadTreeHitSoundIfNeeded();
        EnsureTreeHitAudioSource();

        if (treeHitSound == null || treeHitAudioSource == null)
            return;

        AudioClip clipToPlay = GetPreparedClip(
            treeHitSound,
            treeHitSoundStartOffset,
            ref preparedTreeHitSound,
            ref preparedTreeHitSoundOffset,
            "TreeHit");

        treeHitAudioSource.Stop();
        treeHitAudioSource.clip = clipToPlay;
        treeHitAudioSource.volume = TreeHitSoundVolume;
        treeHitAudioSource.time = 0f;
        treeHitAudioSource.Play();
    }

    void PlayTreeImpactSound()
    {
        if (playerMovement != null && playerMovement.attackSound != null)
        {
            playerMovement.PlayAttackSound();
            return;
        }

        PlayTreeHitSound();
    }

    void EnsureRockHitAudioSource()
    {
        if (rockHitAudioSource != null)
            return;

        rockHitAudioSource = gameObject.AddComponent<AudioSource>();
        rockHitAudioSource.playOnAwake = false;
        rockHitAudioSource.loop = false;
        rockHitAudioSource.spatialBlend = 0f;
    }

    void LoadRockHitSoundIfNeeded()
    {
        if (rockHitSound == null)
            rockHitSound = Resources.Load<AudioClip>(RockHitSoundClipPath);
    }

    void PreloadRockHitSound()
    {
        if (rockHitSound != null)
            rockHitSound.LoadAudioData();
    }

    void PlayRockHitSound()
    {
        LoadRockHitSoundIfNeeded();
        if (rockHitSound == null)
            return;

        if (playerMovement != null)
        {
            playerMovement.PlayUiSound(rockHitSound, RockHitSoundVolume);
            return;
        }

        EnsureRockHitAudioSource();
        if (rockHitAudioSource == null)
            return;

        rockHitAudioSource.Stop();
        rockHitAudioSource.clip = rockHitSound;
        rockHitAudioSource.volume = RockHitSoundVolume;
        rockHitAudioSource.time = 0f;
        rockHitAudioSource.Play();
    }

    AudioClip GetPreparedClip(
        AudioClip sourceClip,
        float startOffsetSeconds,
        ref AudioClip cachedClip,
        ref float cachedOffsetSeconds,
        string cacheLabel)
    {
        if (sourceClip == null)
            return null;

        float safeOffsetSeconds = Mathf.Max(0f, startOffsetSeconds);
        if (safeOffsetSeconds <= 0.001f)
            return sourceClip;

        if (cachedClip != null && Mathf.Approximately(cachedOffsetSeconds, safeOffsetSeconds))
            return cachedClip;

        int startSample = Mathf.Clamp(
            Mathf.RoundToInt(safeOffsetSeconds * sourceClip.frequency),
            0,
            Mathf.Max(0, sourceClip.samples - 1));

        int trimmedSamples = sourceClip.samples - startSample;
        if (trimmedSamples <= 0)
            return sourceClip;

        float[] sampleData = new float[trimmedSamples * sourceClip.channels];
        if (!sourceClip.GetData(sampleData, startSample))
            return sourceClip;

        cachedClip = AudioClip.Create(
            $"{sourceClip.name}_{cacheLabel}_Trimmed",
            trimmedSamples,
            sourceClip.channels,
            sourceClip.frequency,
            false);
        cachedClip.SetData(sampleData, 0);
        cachedOffsetSeconds = safeOffsetSeconds;
        return cachedClip;
    }
}

public class CombatFloatingText : MonoBehaviour
{
    float lifetime = 0.85f;
    float riseSpeed = 1.15f;
    float timer;
    TextMeshPro text;
    Color startColor = Color.white;

    public void Initialize(float textLifetime, float textRiseSpeed)
    {
        lifetime = Mathf.Max(0.1f, textLifetime);
        riseSpeed = Mathf.Max(0f, textRiseSpeed);
        text = GetComponent<TextMeshPro>();

        if (text != null)
            startColor = text.color;
    }

    void Awake()
    {
        if (text == null)
        {
            text = GetComponent<TextMeshPro>();
            if (text != null)
                startColor = text.color;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        Camera camera = RuntimeCameraCache.Main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);

        if (text != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, Mathf.Clamp01(timer / lifetime));
            text.color = color;
        }

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}
