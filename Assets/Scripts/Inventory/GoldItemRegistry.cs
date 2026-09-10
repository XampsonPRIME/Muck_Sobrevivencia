using UnityEngine;

public static class GoldItemRegistry
{
    static Item goldItem;
    static Sprite goldSprite;

    public static Item GetOrCreate()
    {
        if (goldItem != null)
            return goldItem;

        GameObject itemObject = new GameObject("GoldItemData");
        Object.DontDestroyOnLoad(itemObject);

        goldItem = itemObject.AddComponent<Item>();
        goldItem.itemName = "Gold";
        goldItem.itemType = ItemType.Resource;
        goldItem.category = InventoryCategory.Special;
        goldItem.rarity = ItemRarity.Common;
        goldItem.description = "Moeda usada em comercio.";
        goldItem.weight = 0f;
        goldItem.maxStack = Item.ResourceStackLimit;
        goldItem.toolType = ToolType.None;
        goldItem.toolDamage = 0;
        goldItem.buyPrice = 0;
        goldItem.sellPrice = 0;
        goldItem.icon = GetGoldSprite();

        return goldItem;
    }

    public static Sprite GetGoldSprite()
    {
        if (goldSprite != null)
            return goldSprite;

        goldSprite = Resources.Load<Sprite>("Icons/Gold");
        if (goldSprite != null)
            return goldSprite;

        Texture2D texture = Resources.Load<Texture2D>("Icons/Gold");
        if (texture == null)
            return null;

        goldSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        goldSprite.name = "GoldRuntimeSprite";
        return goldSprite;
    }
}
