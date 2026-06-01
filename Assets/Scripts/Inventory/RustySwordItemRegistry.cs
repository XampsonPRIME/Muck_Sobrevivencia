using UnityEngine;

public static class RustySwordItemRegistry
{
    public const string ItemName = "Espada Enferrujada";

    static Item swordItem;
    static Sprite swordSprite;

    public static Item GetOrCreate()
    {
        if (swordItem != null)
            return swordItem;

        GameObject itemObject = new GameObject("EspadaEnferrujadaItemData");
        Object.DontDestroyOnLoad(itemObject);

        swordItem = itemObject.AddComponent<Item>();
        swordItem.itemName = ItemName;
        swordItem.itemType = ItemType.Tool;
        swordItem.toolType = ToolType.Sword;
        swordItem.toolDamage = 4;
        swordItem.buyPrice = 0;
        swordItem.sellPrice = 6;
        swordItem.icon = GetSprite();

        return swordItem;
    }

    public static Sprite GetSprite()
    {
        if (swordSprite != null)
            return swordSprite;

        swordSprite = Resources.Load<Sprite>("Icons/EspadaEnferrujada");
        if (swordSprite != null)
            return swordSprite;

        Texture2D texture = Resources.Load<Texture2D>("Icons/EspadaEnferrujada");
        if (texture == null)
            return null;

        swordSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        swordSprite.name = "EspadaEnferrujadaRuntimeSprite";
        return swordSprite;
    }
}
