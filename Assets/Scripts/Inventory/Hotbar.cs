using UnityEngine;

public class Hotbar : MonoBehaviour
{
    public HotbarSlot[] slots;
    int currentIndex = 0;
    public int SelectedIndex => currentIndex;

    public void AddItem(string itemName, Sprite icon, Item itemData)
    {


        // 🔥 NÃO DUPLICAR (CORRETO)
        foreach (var slot in slots)
        {
            if (slot.itemData?.itemName == itemName)
            {
                return; // 🔥 não adiciona duplicado
            }
        }

        foreach (HotbarSlot slot in slots)
        {
            if (slot.IsEmpty())
            {
                slot.AddItem(itemName, icon, itemData);
                return;
            }
        }

        Debug.Log("Hotbar cheia!");
    }

    public HotbarSlot GetSelectedSlot()
    {
        if (slots == null || slots.Length == 0) return null;

        return slots[currentIndex];
    }

    public void SetSelectedIndex(int index)
    {
        if (index < 0 || index >= slots.Length) return;

        currentIndex = index;
    }

    public void ClearAll()
    {
        if (slots == null)
            return;

        foreach (HotbarSlot slot in slots)
        {
            if (slot != null)
                slot.ClearSlot();
        }

        currentIndex = 0;
    }

    public void AddInventoryItem(InventoryItem inventoryItem)
    {
        TryAddInventoryItem(inventoryItem);
    }

    public bool TryAddInventoryItem(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || inventoryItem.itemData == null || slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (slot == null || slot.IsEmpty())
                continue;

            bool sameBottleState = !inventoryItem.isBottle || slot.bottleIsFilled == inventoryItem.bottleIsFilled;
            if (slot.ItemName != inventoryItem.itemName || !sameBottleState)
                continue;

            slot.SetItem(
                inventoryItem.itemName,
                inventoryItem.GetDisplayIcon(),
                inventoryItem.itemData,
                slot.GetAmount() + inventoryItem.quantity
            );

            if (inventoryItem.isBottle)
                slot.SetBottleState(inventoryItem.bottleIsFilled);

            return true;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (slot == null || !slot.IsEmpty())
                continue;

            slot.SetItem(
                inventoryItem.itemName,
                inventoryItem.GetDisplayIcon(),
                inventoryItem.itemData,
                inventoryItem.quantity
            );

            if (inventoryItem.isBottle)
                slot.SetBottleState(inventoryItem.bottleIsFilled);

            return true;
        }

        return false;
    }
}
