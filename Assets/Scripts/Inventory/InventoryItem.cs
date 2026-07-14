using System;
using UnityEngine;

[Serializable]
public class InventoryItem
{
    public string itemName;
    public int quantity;

    public ItemType itemType;
    public ToolType toolType;
    public InventoryCategory category;
    public ItemRarity rarity;
    public EquipmentSlotType equipmentSlot;

    public Item itemData;
    public Sprite icon;
    public string description;
    public float weight;
    public int maxStack;
    public int defense;
    public int durability;
    public float moveSpeedBonus;
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
        handLocalScale = Vector3.one;
        prefabName = data != null ? data.gameObject.name : "";

        if (data != null)
        {
            data.ApplyDefinition();
            icon = data.GetDisplayIcon();
            itemType = data.itemType;
            toolType = data.toolType;
            category = data.GetCategory();
            rarity = data.GetRarity();
            description = data.GetDescription();
            weight = data.GetWeight();
            maxStack = data.GetMaxStack();
            equipmentSlot = data.GetEquipmentSlot();
            defense = data.GetDefense();
            durability = data.GetDurability();
            moveSpeedBonus = data.GetMoveSpeedBonus();
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
            category = InventoryCategory.Resources;
            rarity = ItemRarity.Common;
            description = "Item coletado durante a exploracao.";
            weight = 0.1f;
            maxStack = Item.ResourceStackLimit;
            equipmentSlot = EquipmentSlotType.None;
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
        {
            Sprite displayIcon = itemData.GetDisplayIcon();
            return displayIcon != null ? displayIcon : icon;
        }

        BottleItem bottle = itemData.GetComponent<BottleItem>();
        Sprite bottleIcon = bottle != null ? bottle.GetIcon(bottleIsFilled) : itemData.GetDisplayIcon();
        return bottleIcon != null ? bottleIcon : icon;
    }

    void ResolveMissingDisplayData()
    {
        if (icon != null)
            return;

        if (itemData != null && itemData.GetDisplayIcon() != null)
        {
            icon = itemData.GetDisplayIcon();
            return;
        }

        Item[] candidates = Resources.FindObjectsOfTypeAll<Item>();
        Item fallback = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            Item candidate = candidates[i];
            if (candidate == null || candidate.GetDisplayIcon() == null)
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
        icon = fallback.GetDisplayIcon();
        prefabName = fallback.gameObject.name;
        itemType = fallback.itemType;
        toolType = fallback.toolType;
        category = fallback.GetCategory();
        rarity = fallback.GetRarity();
        description = fallback.GetDescription();
        weight = fallback.GetWeight();
        maxStack = fallback.GetMaxStack();
        equipmentSlot = fallback.GetEquipmentSlot();
        defense = fallback.GetDefense();
        durability = fallback.GetDurability();
        moveSpeedBonus = fallback.GetMoveSpeedBonus();
    }

    public InventoryItem Clone()
    {
        InventoryItem clone = new InventoryItem(itemName, quantity, itemData)
        {
            itemType = itemType,
            toolType = toolType,
            category = category,
            rarity = rarity,
            equipmentSlot = equipmentSlot,
            icon = icon,
            description = description,
            weight = weight,
            maxStack = maxStack,
            defense = defense,
            durability = durability,
            moveSpeedBonus = moveSpeedBonus,
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

    public void CopyFrom(InventoryItem other)
    {
        if (other == null)
            return;

        itemName = other.itemName;
        quantity = other.quantity;
        itemType = other.itemType;
        toolType = other.toolType;
        category = other.category;
        rarity = other.rarity;
        equipmentSlot = other.equipmentSlot;
        itemData = other.itemData;
        icon = other.icon;
        description = other.description;
        weight = other.weight;
        maxStack = other.maxStack;
        defense = other.defense;
        durability = other.durability;
        moveSpeedBonus = other.moveSpeedBonus;
        isConsumable = other.isConsumable;
        healthRestore = other.healthRestore;
        hungerRestore = other.hungerRestore;
        thirstRestore = other.thirstRestore;
        consumeHoldTime = other.consumeHoldTime;
        prefabName = other.prefabName;
        handLocalPosition = other.handLocalPosition;
        handLocalEulerAngles = other.handLocalEulerAngles;
        handLocalScale = other.handLocalScale;
        isBottle = other.isBottle;
        bottleIsFilled = other.bottleIsFilled;
    }

    public bool CanStackWith(InventoryItem other)
    {
        if (other == null)
            return false;

        if (itemName != other.itemName)
            return false;

        if (GetMaxStack() <= 1 || other.GetMaxStack() <= 1)
            return false;

        if (isBottle || other.isBottle)
            return isBottle == other.isBottle && bottleIsFilled == other.bottleIsFilled;

        return true;
    }

    public int GetMaxStack()
    {
        ResolveMissingDisplayData();

        if (itemData != null)
            return itemData.GetMaxStack();

        return Mathf.Max(1, maxStack <= 0 ? Item.ResourceStackLimit : maxStack);
    }

    public float GetTotalWeight()
    {
        ResolveMissingDisplayData();
        return Mathf.Max(0f, weight) * Mathf.Max(0, quantity);
    }

    public bool IsEquipment()
    {
        ResolveMissingDisplayData();
        return equipmentSlot != EquipmentSlotType.None || itemType == ItemType.Equipment || itemType == ItemType.Tool;
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
