using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 4f;

    public Inventory inventory;
    public Hotbar hotbar;

    public float hitRate = 0.5f;
    float nextHitTime = 0f;

    PlayerControls controls;

    bool interactPressed;

    public ToolType currentTool = ToolType.None;
    public int toolDamage = 1;

    [Header("Referências")]
    public Transform cameraHolder;

    [Header("Mão")]
    public GameObject axePrefab;
    public GameObject pickaxePrefab;

    GameObject currentToolObject;

    [Header("Start Item")]
    public bool startWithAxe = true;
    public bool startWithPickaxe = true;

    void Awake()
    {
        controls = new PlayerControls();

        controls.Player.Interact.performed += ctx => interactPressed = true;

        // 🔥 ATAQUE AGORA É EVENTO ÚNICO
        controls.Player.Attack.performed += ctx => Attack();
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Start()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();

        // itens iniciais
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
        if (GameState.IsInventoryOpen) return;

        // 🔥 RAYCAST PARA COLETA
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = cameraHolder.forward;
        origin += direction * 0.3f;

        Ray ray = new Ray(origin, direction);
        RaycastHit hit;

        Debug.DrawRay(origin, direction * interactDistance, Color.red);

        if (!Physics.Raycast(ray, out hit, interactDistance))
            return;

        Item item = hit.collider.GetComponent<Item>() ??
                    hit.collider.GetComponentInParent<Item>() ??
                    hit.collider.GetComponentInChildren<Item>();

        if (item != null)
        {
            if (interactPressed)
            {
                TryPickup(item);
                interactPressed = false;
            }
        }
    }

    void TryPickup(Item item)
{
    Debug.Log("Pegou: " + item.itemName + " icon: " + item.icon);

    // 🔥 vai pro inventário
    inventory.AddItem(item.itemName, 1, item);

    // 🔥 se for ferramenta → vai pra hotbar
    if (item.itemType == ItemType.Tool)
    {
        hotbar.AddItem(item.itemName, item.icon, item);
    }

    Destroy(item.gameObject);
}

    // =========================
    // 🪓 ATAQUE (CORRIGIDO)
    // =========================
    void Attack()
    {
        if (Time.time < nextHitTime) return;
        if (GameState.IsInventoryOpen) return; // 🔥 BLOQUEIO

        nextHitTime = Time.time + hitRate;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = cameraHolder.forward;
        origin += direction * 0.3f;

        Ray ray = new Ray(origin, direction);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, interactDistance))
            return;

        // 🔥 ANIMAÇÃO
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Chop");
            anim.SetTrigger("Chop");
        }

        // 🔥 RECURSO
        ResourceNode resource = hit.collider.GetComponent<ResourceNode>() ??
                                hit.collider.GetComponentInParent<ResourceNode>();

        if (resource != null)
        {
            resource.Hit(inventory, hotbar, currentTool, toolDamage);
        }
    }

    // =========================
    // 🔢 HOTBAR
    // =========================
    void HandleHotbarSelection()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectSlot(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectSlot(2);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectSlot(3);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) SelectSlot(4);
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
        {
            EquipTool(slot.toolType, slot.toolDamage);
        }
        else
        {
            UnequipTool();
        }
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

    // =========================
    // 🪓 EQUIPAR
    // =========================
    public void EquipTool(ToolType type, int damage)
    {
        currentTool = type;
        toolDamage = damage;

        if (currentToolObject != null)
            Destroy(currentToolObject);

        GameObject prefab = null;

        if (type == ToolType.Axe) prefab = axePrefab;
        if (type == ToolType.Pickaxe) prefab = pickaxePrefab;

        if (prefab == null) return;

        Animator anim = GetComponentInChildren<Animator>();
        Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);

        if (hand == null)
        {
            Debug.LogError("❌ Mão não encontrada!");
            return;
        }

        currentToolObject = Instantiate(prefab, hand);

        currentToolObject.transform.localPosition = new Vector3(-0.056f, 0.064f, 0.001f);
        currentToolObject.transform.localRotation = Quaternion.Euler(0.027f, -0.024f, 0.14f);
    }
}