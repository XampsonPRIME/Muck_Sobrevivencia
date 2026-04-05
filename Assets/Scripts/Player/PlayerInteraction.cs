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

    public ToolType currentTool = ToolType.None;
    public int toolDamage = 1;

    [Header("Mao")]
    public GameObject axePrefab;
    public GameObject pickaxePrefab;

    GameObject currentToolObject;

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
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();

        if (cameraHolder == null)
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null)
                cameraHolder = movement.cameraHolder;
        }

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

        if (controls.Player.Attack.WasPressedThisFrame())
            Attack();

        bool interactPressed = controls.Player.Interact.WasPressedThisFrame();
        if (!interactPressed)
            return;

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

        if (item.itemType == ItemType.Tool)
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

        foreach (HotbarSlot s in hotbar.slots)
            s.isSelected = false;

        HotbarSlot slot = hotbar.slots[index];
        slot.isSelected = true;

        if (slot.IsEmpty())
        {
            UnequipTool();
            return;
        }

        if (slot.itemType == ItemType.Tool)
            EquipTool(slot.toolType, slot.toolDamage);
        else
            UnequipTool();
    }

    void UnequipTool()
    {
        currentTool = ToolType.None;

        if (currentToolObject != null)
        {
            Destroy(currentToolObject);
            currentToolObject = null;
        }
    }

    public void EquipTool(ToolType type, int damage)
    {
        currentTool = type;
        toolDamage = damage;

        if (currentToolObject != null)
            Destroy(currentToolObject);

        GameObject prefab = null;

        if (type == ToolType.Axe) prefab = axePrefab;
        if (type == ToolType.Pickaxe) prefab = pickaxePrefab;

        if (prefab == null)
            return;

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

        currentToolObject = Instantiate(prefab, hand);
        currentToolObject.transform.localPosition = new Vector3(-0.056f, 0.064f, 0.001f);
        currentToolObject.transform.localRotation = Quaternion.Euler(0.027f, -0.024f, 0.14f);
    }

    Animator GetPlayerAnimator()
    {
        return GetComponentInChildren<Animator>(true);
    }
}
