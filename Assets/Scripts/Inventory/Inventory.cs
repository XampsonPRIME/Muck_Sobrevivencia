using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public List<InventoryItem> items = new List<InventoryItem>();

    public void AddItem(string itemName, int amount = 1, Item itemData = null)
    {
        InventoryItem existing = items.Find(i => i.itemName == itemName);

        if (existing != null)
        {
            existing.quantity += amount;

            if (itemData != null)
                existing.itemData = itemData;
        }
        else
        {
            items.Add(new InventoryItem(itemName, amount, itemData));
        }
    }

    // 🔥 NOVO
    public bool RemoveItem(string itemName, int amount = 1)
    {
        InventoryItem existing = items.Find(i => i.itemName == itemName);

        if (existing == null)
            return false;

        existing.quantity -= amount;

        if (existing.quantity <= 0)
        {
            items.Remove(existing);
        }

        return true;
    }

    public InventoryItem GetItem(string itemName)
    {
        return items.Find(i => i.itemName == itemName);
    }

    public bool SetBottleState(string itemName, bool oldState, bool newState)
    {
        InventoryItem item = items.Find(i => i.itemName == itemName && i.isBottle && i.bottleIsFilled == oldState);
        if (item == null)
            return false;

        item.SetBottleState(newState);
        return true;
    }

    public void AddInventoryItem(InventoryItem item)
    {
        if (item == null || item.itemData == null || item.quantity <= 0)
            return;

        InventoryItem existing = items.Find(i => i.CanStackWith(item));

        if (existing != null)
        {
            existing.quantity += item.quantity;
            existing.itemData = item.itemData;
        }
        else
        {
            items.Add(item.Clone());
        }
    }

    public List<InventoryItem> CreateSnapshot()
    {
        List<InventoryItem> snapshot = new List<InventoryItem>(items.Count);

        foreach (InventoryItem item in items)
        {
            if (item == null)
                continue;

            snapshot.Add(item.Clone());
        }

        return snapshot;
    }

    public void ClearAll()
    {
        items.Clear();
    }
}
