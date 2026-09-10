using UnityEngine;

public static class IronItemRegistry
{
    public const string ItemName = "Ferro Bruto";
    const string IconPath = "Icons/FerroBruto";

    static Item ironItem;
    static Sprite ironSprite;

    public static Item GetOrCreate()
    {
        if (ironItem != null)
            return ironItem;

        GameObject itemObject = new GameObject("FerroItemData");
        Object.DontDestroyOnLoad(itemObject);

        ironItem = itemObject.AddComponent<Item>();
        ironItem.itemName = ItemName;
        ironItem.itemType = ItemType.Resource;
        ironItem.toolType = ToolType.None;
        ironItem.toolDamage = 0;
        ironItem.buyPrice = 0;
        ironItem.sellPrice = 2;
        ironItem.icon = GetSprite();

        return ironItem;
    }

    public static Sprite GetSprite()
    {
        if (ironSprite != null)
            return ironSprite;

        ironSprite = Resources.Load<Sprite>(IconPath);
        if (ironSprite != null)
            return ironSprite;

        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color dark = new Color(0.2f, 0.2f, 0.18f, 1f);
        Color mid = new Color(0.42f, 0.42f, 0.38f, 1f);
        Color light = new Color(0.68f, 0.66f, 0.58f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 5; y <= 18; y++)
        {
            for (int x = 4; x <= 19; x++)
            {
                float dx = (x - 11.5f) / 8.5f;
                float dy = (y - 11.5f) / 7.2f;
                if (dx * dx + dy * dy > 1f)
                    continue;

                Color color = x + y > 24 ? dark : mid;
                if (x > 8 && x < 15 && y > 7 && y < 12)
                    color = light;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        ironSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        ironSprite.name = "FerroRuntimeSprite";
        return ironSprite;
    }
}
