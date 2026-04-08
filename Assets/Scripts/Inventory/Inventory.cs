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

    public void ClearAll()
    {
        items.Clear();
    }
}
