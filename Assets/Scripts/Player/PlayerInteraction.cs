using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 4f;

    public Inventory inventory;
    public Hotbar hotbar;

    float useTimer = 0f;
    public float useTime = 1.5f;

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

        // 🔥 itens iniciais
        if (startWithAxe && axePrefab != null)
        {
            Item axe = axePrefab.GetComponent<Item>();
            inventory.AddItem(axe.itemName);
            hotbar.AddItem(axe.itemName, axe.icon, axe);
        }

        if (startWithPickaxe && pickaxePrefab != null)
        {
            Item pickaxe = pickaxePrefab.GetComponent<Item>();
            inventory.AddItem(pickaxe.itemName);
            hotbar.AddItem(pickaxe.itemName, pickaxe.icon, pickaxe);
        }

        // 🔥 inicia selecionando slot 1
        SelectSlot(0);
    }

    void Update()
    {
        HandleHotbarSelection();
        HandleItemUse();

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, interactDistance))
            return;

        // 🔥 detectar item
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
            return;
        }

        // 🔥 bater (SÓ se não estiver usando item)
        if (attackHeld && Time.time >= nextHitTime && useTimer <= 0f)
        {
            TryHit(hit);
            nextHitTime = Time.time + hitRate;
        }
    }

    // =========================
    // 🍄 USAR ITEM
    // =========================
    void HandleItemUse()
    {
        if (!attackHeld)
        {
            useTimer = 0f;
            return;
        }

        HotbarSlot slot = GetSelectedSlot();

        if (slot == null || slot.IsEmpty())
            return;

        if (slot.itemType != ItemType.Resource)
            return;

        useTimer += Time.deltaTime;

        if (useTimer >= useTime)
        {
            UseItem(slot);
            useTimer = 0f;
        }
    }

    HotbarSlot GetSelectedSlot()
    {
        foreach (HotbarSlot slot in hotbar.slots)
        {
            if (slot.isSelected)
            {
                Debug.Log("Selecionado: " + slot.itemType + " - " + slot.toolType);
                return slot;
            }
        }

        return null;
    }

    void UseItem(HotbarSlot slot)
    {
        Item item = slot.itemData;

        if (item == null) return;

        if (item.itemName == "Cogumelo")
        {
            PlayerMovement player = GetComponent<PlayerMovement>();

            if (player != null && player.currentHealth < player.maxHealth)
            {
               // player.Heal(20f);
                slot.RemoveOne();

                Debug.Log("🍄 Comeu cogumelo!");
            }
        }
    }

    // =========================
    // 📦 COLETAR
    // =========================
    void TryPickup(Item item)
    {
        inventory.AddItem(item.itemName);
        hotbar.AddItem(item.itemName, item.icon, item);

        Destroy(item.gameObject);
    }

    // =========================
    // ⛏️ BATER
    // =========================
    void TryHit(RaycastHit hit)
    {
        ResourceNode resource = hit.collider.GetComponent<ResourceNode>() ??
                                hit.collider.GetComponentInParent<ResourceNode>();

        ToolSwing swing = currentToolObject?.GetComponent<ToolSwing>();
        swing?.Swing();

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

        // 🔥 limpa seleção anterior
        foreach (HotbarSlot s in hotbar.slots)
            s.isSelected = false;

        HotbarSlot slot = hotbar.slots[index];
        slot.isSelected = true;

        Debug.Log("Selecionou slot: " + index + " | Tipo: " + slot.itemType);

        if (slot.IsEmpty())
            return;

        if (slot.itemType == ItemType.Tool)
        {
            EquipTool(slot.toolType, slot.toolDamage);
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

        if (prefab != null && handPoint != null)
        {
            currentToolObject = Instantiate(prefab, handPoint);

            ToolView view = currentToolObject.GetComponent<ToolView>();

            if (view != null)
            {
                currentToolObject.transform.localPosition = view.position;
                currentToolObject.transform.localRotation = Quaternion.Euler(view.rotation);
            }
            else
            {
                currentToolObject.transform.localPosition = new Vector3(0.2f, -0.2f, 0.4f);
                currentToolObject.transform.localRotation = Quaternion.Euler(0, 90, 0);
            }
        }

        Debug.Log("Equipou: " + type);
    }
}