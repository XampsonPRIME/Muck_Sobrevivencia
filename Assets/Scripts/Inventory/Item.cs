using UnityEngine;

public enum ItemType
{
    Resource,
    Tool,
    Consumable,
    Equipment,
    Quest
}

public enum ToolType
{
    None,
    Axe,
    Pickaxe,
    Sword,
    Bow,
    
}

public enum InventoryCategory
{
    All,
    Resources,
    Food,
    Tools,
    Weapons,
    Armor,
    Consumables,
    Special,
    Other
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum EquipmentSlotType
{
    None,
    Head,
    Chest,
    Legs,
    Boots,
    MainHand,
    Shield,
    Accessory
}

public class Item : MonoBehaviour
{
    public const int ResourceStackLimit = 999;
    public const int ConsumableStackLimit = 50;
    public const int EquipmentStackLimit = 1;

    [Header("Definicao")]
    public InventoryItemDefinition definition;

    [Header("Base")]
    public string itemName;
    public Sprite icon;

    public ItemType itemType = ItemType.Resource;
    public InventoryCategory category = InventoryCategory.Other;
    public ItemRarity rarity = ItemRarity.Common;
    [TextArea(2, 5)] public string description;
    [Min(0f)] public float weight = 0.1f;
    [Min(1)] public int maxStack = 0;

    [Header("Ferramenta")]
    public ToolType toolType = ToolType.None;
    public int toolDamage = 1;

    [Header("Equipamento")]
    public EquipmentSlotType equipmentSlot = EquipmentSlotType.None;
    [Min(0)] public int defense = 0;
    [Min(0)] public int durability = 0;
    public float moveSpeedBonus = 0f;

    [Header("Comercio")]
    [Min(0)] public int buyPrice = 0;
    [Min(0)] public int sellPrice = 0;

    public int GetBuyPrice()
    {
        return Mathf.Max(0, buyPrice);
    }

    public int GetSellPrice()
    {
        return Mathf.Max(0, sellPrice);
    }

    public string GetDisplayName()
    {
        return definition != null && !string.IsNullOrWhiteSpace(definition.itemName)
            ? definition.itemName
            : itemName;
    }

    public Sprite GetDisplayIcon()
    {
        return definition != null && definition.icon != null ? definition.icon : icon;
    }

    public InventoryCategory GetCategory()
    {
        if (definition != null)
            return definition.category;

        if (category != InventoryCategory.Other && category != InventoryCategory.All)
            return category;

        if (itemType == ItemType.Consumable)
            return IsFoodName(itemName) ? InventoryCategory.Food : InventoryCategory.Consumables;

        if (itemType == ItemType.Tool)
        {
            if (toolType == ToolType.Sword || toolType == ToolType.Bow)
                return InventoryCategory.Weapons;

            return InventoryCategory.Tools;
        }

        if (itemType == ItemType.Equipment)
            return equipmentSlot == EquipmentSlotType.Head ||
                   equipmentSlot == EquipmentSlotType.Chest ||
                   equipmentSlot == EquipmentSlotType.Legs ||
                   equipmentSlot == EquipmentSlotType.Boots
                ? InventoryCategory.Armor
                : InventoryCategory.Special;

        if (itemType == ItemType.Quest)
            return InventoryCategory.Special;

        if (IsFoodName(itemName))
            return InventoryCategory.Food;

        return InventoryCategory.Resources;
    }

    public ItemRarity GetRarity()
    {
        return definition != null ? definition.rarity : rarity;
    }

    public string GetDescription()
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.description))
            return definition.description;

        if (!string.IsNullOrWhiteSpace(description))
            return description;

        return BuildFallbackDescription();
    }

    public float GetWeight()
    {
        if (definition != null)
            return Mathf.Max(0f, definition.weight);

        if (weight > 0f)
            return weight;

        return GetDefaultWeight();
    }

    public int GetMaxStack()
    {
        if (definition != null)
            return Mathf.Max(1, definition.maxStack);

        if (maxStack > 0)
            return Mathf.Max(1, maxStack);

        if (itemType == ItemType.Tool || itemType == ItemType.Equipment)
            return EquipmentStackLimit;

        if (itemType == ItemType.Consumable)
            return ConsumableStackLimit;

        return ResourceStackLimit;
    }

    public EquipmentSlotType GetEquipmentSlot()
    {
        if (definition != null)
            return definition.equipmentSlot;

        if (equipmentSlot != EquipmentSlotType.None)
            return equipmentSlot;

        if (itemType == ItemType.Tool)
        {
            if (toolType == ToolType.None && itemName != null && itemName.ToLowerInvariant().Contains("escudo"))
                return EquipmentSlotType.Shield;

            return EquipmentSlotType.MainHand;
        }

        return EquipmentSlotType.None;
    }

    public int GetDefense()
    {
        return definition != null ? Mathf.Max(0, definition.defense) : Mathf.Max(0, defense);
    }

    public int GetDurability()
    {
        return definition != null ? Mathf.Max(0, definition.durability) : Mathf.Max(0, durability);
    }

    public float GetMoveSpeedBonus()
    {
        return definition != null ? definition.moveSpeedBonus : moveSpeedBonus;
    }

    public void ApplyDefinition()
    {
        if (definition == null)
            return;

        itemName = definition.itemName;
        icon = definition.icon;
        itemType = definition.itemType;
        category = definition.category;
        rarity = definition.rarity;
        description = definition.description;
        weight = definition.weight;
        maxStack = definition.maxStack;
        toolType = definition.toolType;
        toolDamage = definition.toolDamage;
        equipmentSlot = definition.equipmentSlot;
        defense = definition.defense;
        durability = definition.durability;
        moveSpeedBonus = definition.moveSpeedBonus;
        buyPrice = definition.buyPrice;
        sellPrice = definition.sellPrice;
    }

    string BuildFallbackDescription()
    {
        if (itemType == ItemType.Tool)
            return toolType == ToolType.Bow ? "Arma de combate a distancia." : "Ferramenta util para exploracao e combate.";

        if (itemType == ItemType.Consumable)
            return "Item consumivel utilizado para recuperar recursos do personagem.";

        if (itemType == ItemType.Equipment)
            return "Equipamento que pode melhorar os atributos do personagem.";

        return "Recurso coletado durante a exploracao.";
    }

    float GetDefaultWeight()
    {
        string name = itemName != null ? itemName.ToLowerInvariant() : string.Empty;

        if (name.Contains("madeira"))
            return 0.5f;

        if (name.Contains("pedra") || name.Contains("ferro") || name.Contains("metal"))
            return 1f;

        if (name.Contains("pena"))
            return 0.02f;

        if (name.Contains("corda") || name.Contains("graveto"))
            return 0.1f;

        if (itemType == ItemType.Tool || itemType == ItemType.Equipment)
            return 3f;

        if (itemType == ItemType.Consumable)
            return 0.3f;

        return 0.2f;
    }

    static bool IsFoodName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string lower = value.ToLowerInvariant();
        return lower.Contains("carne") ||
               lower.Contains("comida") ||
               lower.Contains("cogumelo") ||
               lower.Contains("fruta") ||
               lower.Contains("pao") ||
               lower.Contains("pão");
    }
}
