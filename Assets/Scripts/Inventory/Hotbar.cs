using UnityEngine;

public class Hotbar : MonoBehaviour
{
    public HotbarSlot[] slots;
    int currentIndex = 0;

    public void AddItem(string itemName, Sprite icon, Item itemData)
    {


        // 🔥 NÃO DUPLICAR (CORRETO)
        foreach (var slot in slots)
        {
            Debug.Log($"Slot: {slot.name}, Item: {slot.itemData?.itemName}");
            if (slot.itemData?.itemName == itemName)
            {
                return; // 🔥 não adiciona duplicado
            }
        }

        // 🔥 encontra slot vazio
        foreach (var slot in slots)
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
}