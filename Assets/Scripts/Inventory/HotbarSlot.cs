using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarSlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;

    string itemName;
    int amount;

    // 🔥 DADOS DO ITEM
    public Item itemData;

    public ItemType itemType;
    public ToolType toolType;
    public int toolDamage;

    // ✅ SELEÇÃO (ESSENCIAL)
    public bool isSelected = false;

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

            this.itemData = itemData;

            if (itemData != null)
            {
                itemType = itemData.itemType;
                toolType = itemData.toolType;
                toolDamage = itemData.toolDamage;
            }
        }

        amount++;
        UpdateUI();
    }

    // 🔥 REMOVER ITEM (ex: comer cogumelo)
    public void RemoveOne()
    {
        amount--;

        if (amount <= 0)
        {
            ClearSlot();
        }
        else
        {
            UpdateUI();
        }
    }

    void ClearSlot()
    {
        itemName = "";
        amount = 0;

        icon.sprite = null;
        icon.enabled = false;

        amountText.text = "";

        itemData = null;
        itemType = ItemType.Resource;
        toolType = ToolType.None;
        toolDamage = 0;

        isSelected = false; // 👈 IMPORTANTE
    }

    void UpdateUI()
    {
        amountText.text = amount > 1 ? amount.ToString() : "";
    }
}