using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarSlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;

    string itemName;
    int amount;

    public bool IsEmpty()
    {
        return string.IsNullOrEmpty(itemName);
    }

    public bool CanStack(string name)
    {
        return itemName == name;
    }

    public void AddItem(string name, Sprite sprite)
    {
        if (IsEmpty())
        {
            itemName = name;
            icon.sprite = sprite;
            icon.enabled = true;
        }

        amount++;
        amountText.text = amount > 1 ? amount.ToString() : "";
    }
}