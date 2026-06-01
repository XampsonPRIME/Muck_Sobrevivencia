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
    public Sprite icon;
    public bool isConsumable;
    public float healthRestore;
    public float hungerRestore;
    public float thirstRestore;
    public float consumeHoldTime;
    public string prefabName;
    public Vector3 handLocalPosition;
    public Vector3 handLocalEulerAngles;
    public Vector3 handLocalScale;
    public bool isBottle;
    public bool bottleIsFilled;

    public InventoryItem(string name, int qty, Item data)
    {
        itemName = name;
        quantity = qty;
        itemData = data;
        icon = data != null ? data.icon : null;
        handLocalScale = Vector3.one;
        prefabName = data != null ? data.gameObject.name : "";

        if (data != null)
        {
            itemType = data.itemType;
            toolType = data.toolType;
            isBottle = data.GetComponent<BottleItem>() != null;
            bottleIsFilled = false;

            ConsumableItem consumable = data.GetComponent<ConsumableItem>();
            if (consumable != null)
            {
                isConsumable = true;
                healthRestore = consumable.healthRestore;
                hungerRestore = consumable.hungerRestore;
                thirstRestore = consumable.thirstRestore;
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

    public void SetBottleState(bool isFilled)
    {
        if (!isBottle || itemData == null)
            return;

        bottleIsFilled = isFilled;

        BottleItem bottle = itemData.GetComponent<BottleItem>();
        ConsumableItem consumable = itemData.GetComponent<ConsumableItem>();

        if (bottle == null)
            return;

        isConsumable = true;
        thirstRestore = isFilled ? bottle.filledThirstRestore : 0f;
        consumeHoldTime = isFilled ? bottle.filledConsumeHoldTime : (consumable != null ? consumable.consumeHoldTime : 0.6f);
    }

    public Sprite GetDisplayIcon()
    {
        ResolveMissingDisplayData();

        if (itemData == null)
            return icon;

        if (!isBottle)
            return itemData.icon != null ? itemData.icon : icon;

        BottleItem bottle = itemData.GetComponent<BottleItem>();
        Sprite bottleIcon = bottle != null ? bottle.GetIcon(bottleIsFilled) : itemData.icon;
        return bottleIcon != null ? bottleIcon : icon;
    }

    void ResolveMissingDisplayData()
    {
        if (icon != null)
            return;

        if (itemData != null && itemData.icon != null)
        {
            icon = itemData.icon;
            return;
        }

        Item[] candidates = Resources.FindObjectsOfTypeAll<Item>();
        Item fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            Item candidate = candidates[i];
            if (candidate == null || candidate.icon == null)
                continue;

            if (!string.IsNullOrWhiteSpace(prefabName) && candidate.gameObject.name == prefabName)
            {
                fallback = candidate;
                break;
            }

            if (fallback == null && candidate.itemName == itemName)
                fallback = candidate;
        }

        if (fallback == null)
            return;

        itemData = fallback;
        icon = fallback.icon;
        prefabName = fallback.gameObject.name;
        itemType = fallback.itemType;
        toolType = fallback.toolType;
    }

    public InventoryItem Clone()
    {
        InventoryItem clone = new InventoryItem(itemName, quantity, itemData)
        {
            itemType = itemType,
            toolType = toolType,
            icon = icon,
            isConsumable = isConsumable,
            healthRestore = healthRestore,
            hungerRestore = hungerRestore,
            thirstRestore = thirstRestore,
            consumeHoldTime = consumeHoldTime,
            prefabName = prefabName,
            handLocalPosition = handLocalPosition,
            handLocalEulerAngles = handLocalEulerAngles,
            handLocalScale = handLocalScale,
            isBottle = isBottle,
            bottleIsFilled = bottleIsFilled
        };

        return clone;
    }

    public bool CanStackWith(InventoryItem other)
    {
        if (other == null)
            return false;

        if (itemName != other.itemName)
            return false;

        if (isBottle || other.isBottle)
            return isBottle == other.isBottle && bottleIsFilled == other.bottleIsFilled;

        return true;
    }

    public int GetBuyPrice()
    {
        return itemData != null ? itemData.GetBuyPrice() : 0;
    }

    public int GetSellPrice()
    {
        return itemData != null ? itemData.GetSellPrice() : 0;
    }
}
