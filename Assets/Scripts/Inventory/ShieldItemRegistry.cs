using UnityEngine;

public static class ShieldItemRegistry
{
    public const string ItemName = "Escudo";
    const string IconPath = "Icons/Escudo";

    static Item shieldItem;
    static Sprite shieldSprite;

    public static Item GetOrCreate()
    {
        if (shieldItem != null)
            return shieldItem;

        GameObject itemObject = new GameObject("EscudoItemData");
        Object.DontDestroyOnLoad(itemObject);

        shieldItem = itemObject.AddComponent<Item>();
        shieldItem.itemName = ItemName;
        shieldItem.itemType = ItemType.Tool;
        shieldItem.toolType = ToolType.None;
        shieldItem.toolDamage = 0;
        shieldItem.buyPrice = 0;
        shieldItem.sellPrice = 10;
        shieldItem.icon = GetSprite();

        return shieldItem;
    }

    public static Sprite GetSprite()
    {
        if (shieldSprite != null)
            return shieldSprite;

        shieldSprite = Resources.Load<Sprite>(IconPath);
        return shieldSprite;
    }
}
