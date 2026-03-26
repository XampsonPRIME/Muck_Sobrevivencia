using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 4f;

    Inventory inventory;
    Hotbar hotbar;

    void Start()
    {
        inventory = GetComponent<Inventory>();

        // 🔥 NOVO MÉTODO (substitui o obsoleto)
        hotbar = FindFirstObjectByType<Hotbar>();

        if (hotbar == null)
        {
            Debug.LogError("Hotbar não encontrada na cena!");
        }
    }

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            Item item = hit.collider.GetComponent<Item>();

            if (item != null)
            {
                if (inventory != null)
                    inventory.AddItem(item.itemName);

                if (hotbar != null)
                    hotbar.AddItem(item.itemName, item.icon);

                Destroy(hit.collider.gameObject);
            }
        }
    }
}