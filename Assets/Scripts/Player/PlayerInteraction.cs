using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 4f;
    public Transform cameraHolder;

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

    GameObject currentEquippedObject;
    HotbarSlot selectedSlot;
    float consumeTimer;

    [Header("Start Item")]
    public bool startWithAxe = true;
    public bool startWithPickaxe = true;

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
        playerMovement = GetComponent<PlayerMovement>();

        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();

        if (cameraHolder == null && playerMovement != null)
            cameraHolder = playerMovement.cameraHolder;

        if (cameraHolder == null && Camera.main != null)
            cameraHolder = Camera.main.transform;

        if (startWithAxe && axePrefab != null)
        {
            Item axe = axePrefab.GetComponent<Item>();
            inventory.AddItem(axe.itemName, 1, axe);
            hotbar.AddItem(axe.itemName, axe.icon, axe);
        }

        if (startWithPickaxe && pickaxePrefab != null)
        {
            Item pickaxe = pickaxePrefab.GetComponent<Item>();
            inventory.AddItem(pickaxe.itemName, 1, pickaxe);
            hotbar.AddItem(pickaxe.itemName, pickaxe.icon, pickaxe);
        }

        SelectSlot(0);
    }

    void Update()
    {
        HandleHotbarSelection();

        if (GameState.IsInventoryOpen)
            return;

        HandleConsumableUse();

        if (selectedSlot != null && selectedSlot.isConsumable)
            return;

        if (controls.Player.Attack.WasPressedThisFrame())
            Attack();

        if (controls.Player.Interact.WasPressedThisFrame())
            TryPickupFromRay();
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

        if (playerMovement != null)
        {
            playerMovement.Heal(selectedSlot.healthRestore);
            playerMovement.RestoreHunger(selectedSlot.hungerRestore);
        }

        inventory?.RemoveItem(selectedSlot.ItemName, 1);
        selectedSlot.RemoveOne();

        if (selectedSlot.IsEmpty())
            UnequipCurrentItem();

        if (MessageSystem.Instance != null)
            MessageSystem.Instance.ShowMessage($"Consumiu {consumedItemName}");
    }

    void TryPickupFromRay()
    {
        if (!TryGetInteractionRay(out Ray ray))
            return;

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            return;

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
        if (Time.time < nextHitTime || GameState.IsInventoryOpen)
            return;

        nextHitTime = Time.time + hitRate;

        if (!TryGetInteractionRay(out Ray ray))
            return;

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            return;

        Animator anim = GetPlayerAnimator();
        if (anim != null)
        {
            anim.ResetTrigger("Chop");
            anim.SetTrigger("Chop");
        }

        Cow cow = hit.collider.GetComponent<Cow>() ??
                  hit.collider.GetComponentInParent<Cow>();

        if (cow != null)
        {
            cow.Hit(toolDamage);
            return;
        }

        MiniKrug miniKrug = hit.collider.GetComponent<MiniKrug>() ??
                            hit.collider.GetComponentInParent<MiniKrug>();

        if (miniKrug != null)
        {
            miniKrug.Hit(toolDamage, playerMovement);
            return;
        }

        ResourceNode resource = hit.collider.GetComponent<ResourceNode>() ??
                                hit.collider.GetComponentInParent<ResourceNode>();

        if (resource != null)
            resource.Hit(inventory, hotbar, currentTool, toolDamage);
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

    void HandleHotbarSelection()
    {
        for (int i = 0; i < hotbarActions.Length; i++)
        {
            if (hotbarActions[i].WasPressedThisFrame())
            {
                SelectSlot(i);
                return;
            }
        }
    }

    void SelectSlot(int index)
    {
        if (hotbar == null || hotbar.slots.Length <= index)
            return;

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

        if (type == ToolType.Axe) prefab = axePrefab;
        if (type == ToolType.Pickaxe) prefab = pickaxePrefab;

        if (prefab == null)
        {
            UnequipCurrentItem();
            return;
        }

        EquipObjectInHand(prefab, Vector3.zero, Vector3.zero, Vector3.one, false);
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

        if (!stripGameplayComponents)
            return;

        foreach (Collider col in currentEquippedObject.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        foreach (Rigidbody rb in currentEquippedObject.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        foreach (Item item in currentEquippedObject.GetComponentsInChildren<Item>(true))
            Destroy(item);

        foreach (ResourceNode node in currentEquippedObject.GetComponentsInChildren<ResourceNode>(true))
            Destroy(node);

        foreach (ConsumableItem consumable in currentEquippedObject.GetComponentsInChildren<ConsumableItem>(true))
            Destroy(consumable);
    }

    Animator GetPlayerAnimator()
    {
        return GetComponentInChildren<Animator>(true);
    }
}
