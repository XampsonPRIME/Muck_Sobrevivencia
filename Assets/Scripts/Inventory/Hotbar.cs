using UnityEngine;

public class Hotbar : MonoBehaviour
{
    public HotbarSlot[] slots;

    public void AddItem(string itemName, Sprite icon, Item itemData = null)
    {
        // empilhar
        foreach (HotbarSlot slot in slots)
        {
            if (!slot.IsEmpty() && slot.CanStack(itemName))
            {
                slot.AddItem(itemName, icon, itemData);
                return;
            }
        }

        // slot vazio
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
}