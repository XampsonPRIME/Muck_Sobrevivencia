using UnityEngine;

public static class RopeItemRegistry
{
    public const string ItemName = "Corda";
    const string IconPath = "Icons/Corda";

    static Item ropeItem;
    static Sprite ropeSprite;

    public static Item GetOrCreate()
    {
        if (ropeItem != null)
            return ropeItem;

        GameObject itemObject = new GameObject("CordaItemData");
        Object.DontDestroyOnLoad(itemObject);

        ropeItem = itemObject.AddComponent<Item>();
        ropeItem.itemName = ItemName;
        ropeItem.itemType = ItemType.Resource;
        ropeItem.category = InventoryCategory.Resources;
        ropeItem.rarity = ItemRarity.Common;
        ropeItem.description = "Fibra trancada usada em receitas de ferramentas e armas.";
        ropeItem.weight = 0.1f;
        ropeItem.maxStack = Item.ResourceStackLimit;
        ropeItem.toolType = ToolType.None;
        ropeItem.toolDamage = 0;
        ropeItem.buyPrice = 0;
        ropeItem.sellPrice = 2;
        ropeItem.icon = GetSprite();

        return ropeItem;
    }

    public static Sprite GetSprite()
    {
        if (ropeSprite != null)
            return ropeSprite;

        ropeSprite = Resources.Load<Sprite>(IconPath);
        return ropeSprite;
    }
}
