using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    public GameObject panel;
    public Transform content;
    public GameObject slotPrefab;

    PlayerInteraction playerInteraction;
    PlayerMovement playerMovement;
    Inventory inventory;
    InputAction toggleInventoryAction;

    void Awake()
    {
        toggleInventoryAction = new InputAction("ToggleInventory", binding: "<Keyboard>/i");
    }

    void OnEnable()
    {
        toggleInventoryAction.Enable();
    }

    void OnDisable()
    {
        toggleInventoryAction.Disable();
    }

    void Start()
    {
        inventory = FindFirstObjectByType<Inventory>();
        playerInteraction = FindFirstObjectByType<PlayerInteraction>();
        playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (panel != null)
            panel.SetActive(false);
    }

    void Update()
    {
        if (toggleInventoryAction.WasPressedThisFrame())
            Toggle();
    }

    void Toggle()
    {
        if (panel == null)
            return;

        bool isOpen = !panel.activeSelf;
        panel.SetActive(isOpen);

        GameState.IsInventoryOpen = isOpen;

        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerInteraction != null)
                playerInteraction.enabled = false;

            if (playerMovement != null)
                playerMovement.enabled = false;

            Refresh();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerInteraction != null)
                playerInteraction.enabled = true;

            if (playerMovement != null)
                playerMovement.enabled = true;
        }
    }

    public void Refresh()
    {
        if (inventory == null || content == null || slotPrefab == null)
        {
            Debug.LogError("InventoryUI nao configurado!");
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        foreach (var item in inventory.items)
        {
            GameObject slotGO = Instantiate(slotPrefab, content);
            InventorySlotUI slot = slotGO.GetComponent<InventorySlotUI>();

            if (slot != null)
                slot.Setup(item);
            else
                Debug.LogError("Slot sem InventorySlotUI!");
        }
    }
}
