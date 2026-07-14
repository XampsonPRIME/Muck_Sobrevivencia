using UnityEngine;

public static class OakWoodItemRegistry
{
    public const string ItemName = "Madeira de Carvalho";

    static Item oakWoodItem;
    static Sprite oakWoodSprite;
    static bool spriteIsFallback;

    public static Item GetOrCreate()
    {
        if (oakWoodItem != null)
            return oakWoodItem;

        GameObject itemObject = new GameObject("MadeiraDeCarvalhoItemData");
        Object.DontDestroyOnLoad(itemObject);

        oakWoodItem = itemObject.AddComponent<Item>();
        oakWoodItem.itemName = ItemName;
        oakWoodItem.itemType = ItemType.Resource;
        oakWoodItem.category = InventoryCategory.Resources;
        oakWoodItem.rarity = ItemRarity.Common;
        oakWoodItem.description = "Madeira resistente extraida de carvalhos antigos.";
        oakWoodItem.weight = 0.5f;
        oakWoodItem.maxStack = Item.ResourceStackLimit;
        oakWoodItem.toolType = ToolType.None;
        oakWoodItem.toolDamage = 0;
        oakWoodItem.buyPrice = 0;
        oakWoodItem.sellPrice = 1;
        oakWoodItem.icon = GetSprite();

        return oakWoodItem;
    }

    public static Sprite GetSprite()
    {
        if (oakWoodSprite != null && (!spriteIsFallback || !Application.isPlaying))
            return oakWoodSprite;

        Sprite modelThumbnail = OakWoodDropVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            oakWoodSprite = modelThumbnail;
            spriteIsFallback = false;
            if (oakWoodItem != null)
                oakWoodItem.icon = oakWoodSprite;

            return oakWoodSprite;
        }

        if (oakWoodSprite != null)
            return oakWoodSprite;

        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color barkDark = new Color(0.28f, 0.16f, 0.07f, 1f);
        Color bark = new Color(0.48f, 0.28f, 0.11f, 1f);
        Color cut = new Color(0.76f, 0.52f, 0.24f, 1f);
        Color ring = new Color(0.56f, 0.34f, 0.14f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 8; y <= 16; y++)
        {
            for (int x = 3; x <= 20; x++)
            {
                if ((x == 3 || x == 20) && (y < 10 || y > 14))
                    continue;

                Color color = y == 8 || y == 16 ? barkDark : bark;
                if ((x + y) % 5 == 0)
                    color = Color.Lerp(color, barkDark, 0.45f);

                texture.SetPixel(x, y, color);
            }
        }

        for (int y = 7; y <= 17; y++)
        {
            for (int x = 2; x <= 7; x++)
            {
                float dx = (x - 4.5f) / 3.2f;
                float dy = (y - 12f) / 5.4f;
                float d = dx * dx + dy * dy;
                if (d > 1f)
                    continue;

                texture.SetPixel(x, y, d > 0.62f ? ring : cut);
            }
        }

        texture.Apply();
        oakWoodSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        oakWoodSprite.name = "MadeiraDeCarvalhoRuntimeSprite";
        spriteIsFallback = true;
        return oakWoodSprite;
    }
}
