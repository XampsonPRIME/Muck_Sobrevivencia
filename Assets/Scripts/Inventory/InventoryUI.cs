using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public GameObject panel;
    public Transform content;
    public GameObject slotPrefab;
    PlayerInteraction playerInteraction;
    Inventory inventory;

    void Start()
    {
        inventory = FindFirstObjectByType<Inventory>();
        playerInteraction = FindFirstObjectByType<PlayerInteraction>();
        panel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            Toggle();
        }
    }

    void Toggle()
    {
        bool isOpen = !panel.activeSelf;
        panel.SetActive(isOpen);

        GameState.IsInventoryOpen = isOpen;

        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 🔥 BLOQUEIA PLAYER
            if (playerInteraction != null)
                playerInteraction.enabled = false;

            // 🔥 BLOQUEIA MOVIMENTO / CÂMERA
            var movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null)
                movement.enabled = false;

            Refresh();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 🔥 REATIVA PLAYER
            if (playerInteraction != null)
                playerInteraction.enabled = true;

            // 🔥 REATIVA MOVIMENTO
            var movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null)
                movement.enabled = true;
        }
    }

    void Refresh()
    {
        if (inventory == null || content == null || slotPrefab == null)
        {
            Debug.LogError("❌ InventoryUI não configurado!");
            return;
        }

        // limpa slots
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        // cria slots
        foreach (var item in inventory.items)
        {
            GameObject slotGO = Instantiate(slotPrefab, content);

            InventorySlotUI slot = slotGO.GetComponent<InventorySlotUI>();

            if (slot != null)
            {
                slot.Setup(item);
            }
            else
            {
                Debug.LogError("❌ Slot sem InventorySlotUI!");
            }
        }
    }
}