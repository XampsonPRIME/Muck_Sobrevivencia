using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
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
            hotbar = FindFirstObjectByType<Hotbar>();

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
            deathLoot.Collect(inventory, hotbar);

            InventoryUI inventoryUi = FindFirstObjectByType<InventoryUI>();
            if (inventoryUi != null)
                inventoryUi.Refresh();

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

            InventoryUI inventoryUi = FindFirstObjectByType<InventoryUI>();
            if (inventoryUi != null)
                inventoryUi.Refresh();

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
        Debug.Log("Pegou: " + item.itemName + " icon: " + item.icon);

        inventory.AddItem(item.itemName, 1, item);

        if (item.itemType == ItemType.Tool || item.itemType == ItemType.Consumable)
            hotbar.AddItem(item.itemName, item.icon, item);

        Destroy(item.gameObject);
    }

    void Attack()
    {
        if (Time.time < nextHitTime || GameState.IsInventoryOpen || GameState.IsPaused)
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
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(resource, playerMovement, currentTool, toolDamage))
                return;

            resource.Hit(inventory, hotbar, currentTool, toolDamage);
        }
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

        if (prefab == null)
        {
            UnequipCurrentItem();
            return;
        }

        EquipObjectInHand(prefab, Vector3.zero, Vector3.zero, handScale, false);
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

        currentEquippedObject = Instantiate(prefab, hand);
        currentEquippedObject.transform.localPosition = localPosition;
        currentEquippedObject.transform.localRotation = Quaternion.Euler(localEulerAngles);
        currentEquippedObject.transform.localScale = localScale;

        // Equipped visuals should never block raycasts or behave like world pickups/resources.
        foreach (Collider col in currentEquippedObject.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        foreach (Rigidbody rb in currentEquippedObject.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        foreach (Item item in currentEquippedObject.GetComponentsInChildren<Item>(true))
            Destroy(item);

        foreach (ResourceNode node in currentEquippedObject.GetComponentsInChildren<ResourceNode>(true))
            Destroy(node);

        foreach (ConsumableItem consumable in currentEquippedObject.GetComponentsInChildren<ConsumableItem>(true))
        {
            if (stripGameplayComponents)
                Destroy(consumable);
        }

        foreach (BottleItem bottle in currentEquippedObject.GetComponentsInChildren<BottleItem>(true))
        {
            if (stripGameplayComponents)
                Destroy(bottle);
        }

        foreach (MagicSpellConsumable magic in currentEquippedObject.GetComponentsInChildren<MagicSpellConsumable>(true))
        {
            if (stripGameplayComponents)
                Destroy(magic);
        }
    }

    Animator GetPlayerAnimator()
    {
        return GetComponentInChildren<Animator>(true);
    }
}
