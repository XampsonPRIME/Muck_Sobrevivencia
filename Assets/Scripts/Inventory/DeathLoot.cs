using System.Collections.Generic;
using UnityEngine;

public class DeathLoot : MonoBehaviour
{
    readonly List<InventoryItem> storedItems = new List<InventoryItem>();

    public static DeathLoot Spawn(Vector3 worldPosition, List<InventoryItem> items)
    {
        if (items == null || items.Count == 0)
            return null;

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = "DeathLoot";
        root.transform.position = ResolveSpawnPosition(worldPosition);
        root.transform.localScale = new Vector3(0.8f, 0.35f, 0.8f);

        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.48f, 0.26f, 0.12f, 1f);

        Collider collider = root.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = false;

        DeathLoot loot = root.AddComponent<DeathLoot>();
        loot.Store(items);
        return loot;
    }

    public bool HasItems => storedItems.Count > 0;

    public void Collect(Inventory inventory, Hotbar hotbar)
    {
        if (inventory == null || hotbar == null || storedItems.Count == 0)
            return;

        foreach (InventoryItem storedItem in storedItems)
        {
            if (storedItem == null || storedItem.itemData == null || storedItem.quantity <= 0)
                continue;

            inventory.AddInventoryItem(storedItem);

            if (storedItem.itemType == ItemType.Tool || storedItem.itemType == ItemType.Consumable)
                hotbar.AddInventoryItem(storedItem);
        }

        storedItems.Clear();
        Destroy(gameObject);
    }

    public int GetItemCount()
    {
        int total = 0;

        foreach (InventoryItem item in storedItems)
        {
            if (item != null)
                total += Mathf.Max(0, item.quantity);
        }

        return total;
    }

    void Store(List<InventoryItem> items)
    {
        storedItems.Clear();

        foreach (InventoryItem item in items)
        {
            if (item == null || item.itemData == null || item.quantity <= 0)
                continue;

            storedItems.Add(item.Clone());
        }
    }

    static Vector3 ResolveSpawnPosition(Vector3 desiredPosition)
    {
        if (Physics.Raycast(desiredPosition + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.35f;

        return desiredPosition + Vector3.up * 0.35f;
    }
}
