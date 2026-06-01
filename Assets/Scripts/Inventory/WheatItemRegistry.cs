using UnityEngine;

public static class WheatItemRegistry
{
    public const string ItemName = "Trigo";
    const string IconPath = "Icons/Trigo";

    static Item wheatItem;
    static Sprite wheatSprite;

    public static Item GetOrCreate()
    {
        if (wheatItem != null)
            return wheatItem;

        GameObject itemObject = new GameObject("TrigoItemData");
        Object.DontDestroyOnLoad(itemObject);

        wheatItem = itemObject.AddComponent<Item>();
        wheatItem.itemName = ItemName;
        wheatItem.itemType = ItemType.Resource;
        wheatItem.toolType = ToolType.None;
        wheatItem.toolDamage = 0;
        wheatItem.buyPrice = 0;
        wheatItem.sellPrice = 1;
        wheatItem.icon = GetSprite();

        return wheatItem;
    }

    public static Sprite GetSprite()
    {
        if (wheatSprite != null)
            return wheatSprite;

        wheatSprite = Resources.Load<Sprite>(IconPath);
        return wheatSprite;
    }
}
