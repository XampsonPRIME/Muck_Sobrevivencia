using System;

[Serializable]
public class InventoryItem
{
    public string itemName;
    public int quantity;

    public ItemType itemType;
    public ToolType toolType;

    public Item itemData; // 🔥 ESSENCIAL

    public InventoryItem(string name, int qty, Item data)
    {
        itemName = name;
        quantity = qty;
        itemData = data;

        if (data != null)
        {
            itemType = data.itemType;
            toolType = data.toolType;
        }
        else
        {
            itemType = ItemType.Resource;
            toolType = ToolType.None;
        }
    }
}