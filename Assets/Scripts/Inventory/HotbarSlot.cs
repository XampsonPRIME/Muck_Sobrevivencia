using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarSlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;

    string itemName;
    int amount;

    // 🔥 NOVO
    public ItemType itemType;
    public ToolType toolType;
    public int toolDamage;

    public bool IsEmpty()
    {
        return string.IsNullOrEmpty(itemName);
    }

    public bool CanStack(string name)
    {
        return itemName == name;
    }

    public void AddItem(string name, Sprite sprite, Item itemData = null)
    {
        if (IsEmpty())
        {
            itemName = name;
            icon.sprite = sprite;
            icon.enabled = true;

            // 🔥 salva dados da ferramenta
            if (itemData != null)
            {
                itemType = itemData.itemType;
                toolType = itemData.toolType;
                toolDamage = itemData.toolDamage;
            }
        }

        amount++;
        amountText.text = amount > 1 ? amount.ToString() : "";
    }
}