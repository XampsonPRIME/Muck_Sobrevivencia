using UnityEngine;

public class Hotbar : MonoBehaviour
{
    public HotbarSlot[] slots;

    public void AddItem(string itemName, Sprite icon, Item itemData)
    {
        // 🔥 NÃO DUPLICAR
        foreach (var slot in slots)
        {
            if (slot.itemData == itemData)
            {
                Debug.Log("Item já está na hotbar!");
                return;
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
}