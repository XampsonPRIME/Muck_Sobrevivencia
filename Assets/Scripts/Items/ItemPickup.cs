using UnityEngine;
using UnityEngine.InputSystem;

public class ItemPickup : MonoBehaviour
{
    public string itemName;
    public Sprite icon;

    private bool playerNearby = false;

    void Update()
    {
        if (playerNearby && Keyboard.current.eKey.wasPressedThisFrame)
        {
            PickUp();
        }
    }

    void PickUp()
    {
        Inventory inv = FindFirstObjectByType<Inventory>();

        if (inv != null)
        {
            inv.AddItem(itemName);
        }

        HotbarSlot[] slots = FindObjectsByType<HotbarSlot>(FindObjectsSortMode.None);

        foreach (var slot in slots)
        {
            if (slot.CanStack(itemName))
            {
                slot.AddItem(itemName, icon);
                Destroy(gameObject);
                return;
            }
        }

        foreach (var slot in slots)
        {
            if (slot.IsEmpty())
            {
                slot.AddItem(itemName, icon);
                Destroy(gameObject);
                return;
            }
        }

        Debug.Log("Inventário cheio!");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Entrou trigger com: " + other.name);

        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            Debug.Log("PLAYER DETECTADO → pode pegar");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
        }
    }
}