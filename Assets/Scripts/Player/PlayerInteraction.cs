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

    public float interactDistance = 4f;
    public float interactRadius = 0.45f;
    public Transform cameraHolder;
    public bool invertHotbarScroll = false;

    public Inventory inventory;
    public Hotbar hotbar;

    public float hitRate = 0.5f;
    float nextHitTime = 0f;

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
    float preparedRockHitSoundOffset = -1f;
    [Header("Audio")]
    [Min(0f)] public float treeHitSoundStartOffset = DefaultTreeHitSoundStartOffset;
    [Min(0f)] public float rockHitSoundStartOffset = DefaultRockHitSoundStartOffset;

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
            new InputAction("Hotbar5", binding: "<Keyboard>/5")
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

        if (GameState.IsInLobby)
        {
            consumeTimer = 0f;
            return;
        }

        if (GameState.IsPaused)
        {
            consumeTimer = 0f;
            return;
        }

        if (GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen)
        {
            consumeTimer = 0f;
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
            return;
        }

        if (wasDeadLastFrame)
        {
            wasDeadLastFrame = false;
            ReequipSelectedSlot();
        }

        HandleHotbarSelection();

        if (GameState.IsInventoryOpen)
            return;

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

        if (cameraHolder == null && Camera.main != null)
            cameraHolder = Camera.main.transform;
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
            return;
        }

        if (!isHoldingAttack)
        {
            consumeTimer = 0f;
            return;
        }

        float consumeDuration = Mathf.Max(0.15f, selectedSlot.consumeHoldTime);
        consumeTimer += Time.deltaTime;

        if (consumeTimer < consumeDuration)
            return;

        consumeTimer = 0f;
        ConsumeSelectedItem();
    }

    void ConsumeSelectedItem()
    {
        if (selectedSlot == null || selectedSlot.IsEmpty() || !selectedSlot.isConsumable)
            return;

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

            return;
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

            return;
        }

        if (bottle != null)
        {
            selectedSlot.SetBottleState(false);
            inventory?.SetBottleState(selectedSlot.ItemName, true, false);

            if (MessageSystem.Instance != null)
                MessageSystem.Instance.ShowMessage($"Consumiu {consumedItemName}");

            return;
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
                PlayPickupSound();

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
            ReequipSelectedSlot();
            return;
        }

        Item item = hit.collider.GetComponent<Item>() ??
                    hit.collider.GetComponentInParent<Item>() ??
                    hit.collider.GetComponentInChildren<Item>();

        if (item != null)
            TryPickup(item);
    }

    void TryPickup(Item item)
    {
        PickupRespawner respawner = item.GetComponent<PickupRespawner>() ??
                                    item.GetComponentInParent<PickupRespawner>();

        if (respawner != null && !respawner.IsAvailable)
            return;

        string pickedItemName = item.itemName;

        Debug.Log("Pegou: " + pickedItemName + " icon: " + item.icon);

        inventory.AddItem(pickedItemName, 1, item);

        if (item.itemType == ItemType.Tool || item.itemType == ItemType.Consumable)
            hotbar.AddItem(pickedItemName, item.icon, item);

        if (MessageSystem.Instance != null)
            MessageSystem.Instance.ShowMessage($"+1 {pickedItemName}");

        PlayPickupSound();

        if (respawner != null)
            respawner.Collect();
        else
            Destroy(item.gameObject);
    }

    void Attack()
    {
        if (Time.time < nextHitTime || GameState.IsInventoryOpen || GameState.IsPaused || GameState.IsVendorOpen || GameState.IsCraftingOpen || GameState.IsDebugChatOpen)
            return;

        if (TryPlaceSelectedItem())
            return;

        if (!TryFindInteractionHit(out RaycastHit hit))
            return;

        RiverWaterSource riverWater = hit.collider.GetComponent<RiverWaterSource>() ??
                                      hit.collider.GetComponentInParent<RiverWaterSource>();

        if (riverWater != null)
        {
            TryFillSelectedBottle();
            nextHitTime = Time.time + hitRate;
            return;
        }

        nextHitTime = Time.time + hitRate;

        Animator anim = GetPlayerAnimator();
        if (anim != null)
        {
            anim.ResetTrigger("Chop");
            anim.SetTrigger("Chop");
        }

        MiniKrug miniKrug = hit.collider.GetComponent<MiniKrug>() ??
                            hit.collider.GetComponentInParent<MiniKrug>();

        if (miniKrug != null)
        {
            playerMovement?.RegisterBossOrMiniBossCombat();

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(miniKrug, playerMovement, currentTool, toolDamage))
                return;

            miniKrug.Hit(toolDamage, playerMovement);
            return;
        }

        BossEnemy bossEnemy = hit.collider.GetComponent<BossEnemy>() ??
                              hit.collider.GetComponentInParent<BossEnemy>();

        if (bossEnemy != null)
        {
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(bossEnemy, playerMovement, currentTool, toolDamage))
                return;

            if (!bossEnemy.CanBeChallengedBy(playerMovement))
            {
                MessageSystem.Instance?.ShowMessage(bossEnemy.BuildMinimumLevelMessage());
                return;
            }

            playerMovement?.RegisterBossOrMiniBossCombat();
            bossEnemy.Hit(toolDamage, playerMovement);
            return;
        }

        Cow cow = hit.collider.GetComponent<Cow>() ??
                  hit.collider.GetComponentInParent<Cow>();

        if (cow != null)
        {
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(cow, playerMovement, currentTool, toolDamage))
                return;

            cow.Hit(toolDamage);
            return;
        }

        ResourceNode resource = hit.collider.GetComponent<ResourceNode>() ??
                                hit.collider.GetComponentInParent<ResourceNode>();

        if (resource != null)
        {
            bool shouldPlayTreeHitSound = ShouldPlayTreeHitSound(resource);
            bool shouldPlayRockHitSound = ShouldPlayRockHitSound(resource);

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(resource, playerMovement, currentTool, toolDamage))
            {
                if (shouldPlayTreeHitSound)
                    PlayTreeImpactSound();
                if (shouldPlayRockHitSound)
                    PlayRockHitSound();

                return;
            }

            resource.Hit(inventory, hotbar, currentTool, toolDamage);

            if (shouldPlayTreeHitSound)
                PlayTreeImpactSound();
            if (shouldPlayRockHitSound)
                PlayRockHitSound();
        }
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
            if (Mathf.Abs(scrollValue.y) > 0.01f)
            {
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
