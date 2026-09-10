using UnityEngine;

public static class RustyMetalItemRegistry
{
    public const string ItemName = "Metal Enferrujado";

    static Item rustyMetalItem;
    static Sprite rustyMetalSprite;

    public static Item GetOrCreate()
    {
        if (rustyMetalItem != null)
            return rustyMetalItem;

        GameObject itemObject = new GameObject("MetalEnferrujadoItemData");
        Object.DontDestroyOnLoad(itemObject);

        rustyMetalItem = itemObject.AddComponent<Item>();
        rustyMetalItem.itemName = ItemName;
        rustyMetalItem.itemType = ItemType.Resource;
        rustyMetalItem.toolType = ToolType.None;
        rustyMetalItem.toolDamage = 0;
        rustyMetalItem.buyPrice = 0;
        rustyMetalItem.sellPrice = 0;
        rustyMetalItem.icon = GetSprite();

        return rustyMetalItem;
    }

    public static Sprite GetSprite()
    {
        if (rustyMetalSprite != null)
            return rustyMetalSprite;

        rustyMetalSprite = Resources.Load<Sprite>("Icons/MetalEnferrujado");
        if (rustyMetalSprite != null)
            return rustyMetalSprite;

        Texture2D texture = Resources.Load<Texture2D>("Icons/MetalEnferrujado");
        if (texture == null)
            return null;

        rustyMetalSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        rustyMetalSprite.name = "MetalEnferrujadoRuntimeSprite";
        return rustyMetalSprite;
    }
}
