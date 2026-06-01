using UnityEngine;

public static class FurnaceItemRegistry
{
    public const string ItemName = "Fornalha";
    const string IconPath = "Icons/Fornalha";

    static Item furnaceItem;
    static Sprite furnaceSprite;

    public static Item GetOrCreate()
    {
        if (furnaceItem != null)
            return furnaceItem;

        GameObject itemObject = new GameObject("FornalhaItemData");
        Object.DontDestroyOnLoad(itemObject);

        furnaceItem = itemObject.AddComponent<Item>();
        furnaceItem.itemName = ItemName;
        furnaceItem.itemType = ItemType.Tool;
        furnaceItem.toolType = ToolType.None;
        furnaceItem.toolDamage = 0;
        furnaceItem.buyPrice = 0;
        furnaceItem.sellPrice = 0;
        furnaceItem.icon = GetSprite();

        PlaceableItem placeable = itemObject.AddComponent<PlaceableItem>();
        placeable.placedObjectName = "Fornalha";
        placeable.placedScale = Vector3.one;

        return furnaceItem;
    }

    public static Sprite GetSprite()
    {
        if (furnaceSprite != null)
            return furnaceSprite;

        furnaceSprite = Resources.Load<Sprite>(IconPath);
        return furnaceSprite;
    }
}
