using UnityEngine;

public class Hotbar : MonoBehaviour
{
    public HotbarSlot[] slots;

    public void AddItem(string itemName, Sprite icon)
    {
        // Tenta empilhar
        foreach (HotbarSlot slot in slots)
        {
            if (!slot.IsEmpty() && slot.CanStack(itemName))
            {
                slot.AddItem(itemName, icon);
                return;
            }
        }

        // Procura slot vazio
        foreach (HotbarSlot slot in slots)
        {
            if (slot.IsEmpty())
            {
                slot.AddItem(itemName, icon);
                return;
            }
        }

        Debug.Log("Hotbar cheia!");
    }
}