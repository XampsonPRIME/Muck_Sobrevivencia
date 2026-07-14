using UnityEngine;

[CreateAssetMenu(menuName = "Elarion/Inventory/Item Definition")]
public class InventoryItemDefinition : ScriptableObject
{
    [Header("Base")]
    public string itemName;
    public Sprite icon;
    public ItemType itemType = ItemType.Resource;
    public InventoryCategory category = InventoryCategory.Resources;
    public ItemRarity rarity = ItemRarity.Common;
    [TextArea(2, 5)] public string description;
    [Min(0f)] public float weight = 0.1f;
    [Min(1)] public int maxStack = Item.ResourceStackLimit;

    [Header("Ferramenta")]
    public ToolType toolType = ToolType.None;
    public int toolDamage = 0;

    [Header("Equipamento")]
    public EquipmentSlotType equipmentSlot = EquipmentSlotType.None;
    [Min(0)] public int defense = 0;
    [Min(0)] public int durability = 0;
    public float moveSpeedBonus = 0f;

    [Header("Comercio")]
    [Min(0)] public int buyPrice = 0;
    [Min(0)] public int sellPrice = 0;
}
