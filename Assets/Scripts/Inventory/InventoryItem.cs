using System;
using UnityEngine;

[Serializable]
public class InventoryItem
{
    public string itemName;
    public int quantity;

    public ItemType itemType;
    public ToolType toolType;

    public Item itemData;
    public bool isConsumable;
    public float healthRestore;
    public float hungerRestore;
    public float consumeHoldTime;
    public string prefabName;
    public Vector3 handLocalPosition;
    public Vector3 handLocalEulerAngles;
    public Vector3 handLocalScale;

    public InventoryItem(string name, int qty, Item data)
    {
        itemName = name;
        quantity = qty;
        itemData = data;
        handLocalScale = Vector3.one;
        prefabName = data != null ? data.gameObject.name : "";

        if (data != null)
        {
            itemType = data.itemType;
            toolType = data.toolType;

            ConsumableItem consumable = data.GetComponent<ConsumableItem>();
            if (consumable != null)
            {
                isConsumable = true;
                healthRestore = consumable.healthRestore;
                hungerRestore = consumable.hungerRestore;
                consumeHoldTime = consumable.consumeHoldTime;
                handLocalPosition = consumable.handLocalPosition;
                handLocalEulerAngles = consumable.handLocalEulerAngles;
                handLocalScale = consumable.handLocalScale;
            }
        }
        else
        {
            itemType = ItemType.Resource;
            toolType = ToolType.None;
        }
    }
}
