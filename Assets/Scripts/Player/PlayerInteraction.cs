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
    bool attackHeld;

    // 🔥 ferramenta atual
    public ToolType currentTool = ToolType.None;
    public int toolDamage = 1;

    [Header("Mão")]
    public Transform handPoint;
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

        controls.Player.Attack.performed += ctx => attackHeld = true;
        controls.Player.Attack.canceled += ctx => attackHeld = false;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Start()
    {
    
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();

        // 🔥 start com machado
        if (startWithAxe && axePrefab != null)
        {
            Item axe = axePrefab.GetComponent<Item>();

            inventory.AddItem(axe.itemName);
            hotbar.AddItem(axe.itemName, axe.icon, axe);
        }

        // 🔥 start com picareta
        if (startWithPickaxe && pickaxePrefab != null)
        {
            Item pickaxe = pickaxePrefab.GetComponent<Item>();

            inventory.AddItem(pickaxe.itemName);
            hotbar.AddItem(pickaxe.itemName, pickaxe.icon, pickaxe);
        }
    }

    void Update()
    {
        HandleHotbarSelection();

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, interactDistance))
            return;

        // 🔥 detectar item
        Item item = hit.collider.GetComponent<Item>();

        if (item == null)
            item = hit.collider.GetComponentInParent<Item>();

        if (item == null)
            item = hit.collider.GetComponentInChildren<Item>();

        // prioridade item
        if (item != null)
        {
            if (interactPressed)
            {
                TryPickup(item);
                interactPressed = false;
            }

            return;
        }

        // bater
        if (attackHeld && Time.time >= nextHitTime)
        {
            TryHit(hit);
            nextHitTime = Time.time + hitRate;
        }
    }

    void TryPickup(Item item)
    {
        if (item == null) return;

        inventory.AddItem(item.itemName);
        hotbar.AddItem(item.itemName, item.icon, item);

        if (item.itemType == ItemType.Tool)
        {
            EquipTool(item.toolType, item.toolDamage);
        }

        Destroy(item.gameObject);
    }

    void TryHit(RaycastHit hit)
    {
        ResourceNode resource = hit.collider.GetComponent<ResourceNode>();

        ToolSwing swing = currentToolObject?.GetComponent<ToolSwing>();

        if (swing != null)
        {
            swing.Swing();
        }

        if (resource == null)
            resource = hit.collider.GetComponentInParent<ResourceNode>();

        if (resource != null)
        {
            resource.Hit(inventory, hotbar, currentTool, toolDamage);
        }
    }

    // 🔥 TROCA COM TECLAS
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

        HotbarSlot slot = hotbar.slots[index];

        if (slot.IsEmpty())
        {
            Debug.Log("Slot vazio");
            return;
        }

        Debug.Log("Selecionou slot: " + index);

        if (slot.itemType == ItemType.Tool)
        {
            EquipTool(slot.toolType, slot.toolDamage);
        }
    }

    // 🔥 EQUIPAR
    public void EquipTool(ToolType type, int damage)
    {
        currentTool = type;
        toolDamage = damage;

        if (currentToolObject != null)
            Destroy(currentToolObject);

        GameObject prefab = null;

        if (type == ToolType.Axe) prefab = axePrefab;
        if (type == ToolType.Pickaxe) prefab = pickaxePrefab;

        if (prefab != null && handPoint != null)
        {
            currentToolObject = Instantiate(prefab, handPoint);

            // 🔥 pega configuração do prefab
            ToolView view = currentToolObject.GetComponent<ToolView>();

            if (view != null)
            {
                currentToolObject.transform.localPosition = view.position;
                currentToolObject.transform.localRotation = Quaternion.Euler(view.rotation);
            }
            else
            {
                // fallback padrão
                currentToolObject.transform.localPosition = new Vector3(0.2f, -0.2f, 0.4f);
                currentToolObject.transform.localRotation = Quaternion.Euler(0, 90, 0);
            }
        }

        Debug.Log("Equipou: " + type);
    }
}