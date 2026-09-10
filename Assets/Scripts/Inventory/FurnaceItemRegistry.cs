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

public static class CraftingBenchItemRegistry
{
    public const string ItemName = "Mesa de Craft";
    const string IconPath = "Icons/MesaDeCraft";

    static Item craftingBenchItem;
    static Sprite craftingBenchSprite;

    public static Item GetOrCreate()
    {
        if (craftingBenchItem != null)
            return craftingBenchItem;

        GameObject itemObject = new GameObject("MesaDeCraftItemData");
        Object.DontDestroyOnLoad(itemObject);

        craftingBenchItem = itemObject.AddComponent<Item>();
        craftingBenchItem.itemName = ItemName;
        craftingBenchItem.itemType = ItemType.Tool;
        craftingBenchItem.category = InventoryCategory.Tools;
        craftingBenchItem.toolType = ToolType.None;
        craftingBenchItem.toolDamage = 0;
        craftingBenchItem.description = "Coloque no chao para acessar as receitas de crafting.";
        craftingBenchItem.weight = 5f;
        craftingBenchItem.maxStack = 1;
        craftingBenchItem.buyPrice = 0;
        craftingBenchItem.sellPrice = 0;
        craftingBenchItem.icon = GetSprite();

        PlaceableItem placeable = itemObject.AddComponent<PlaceableItem>();
        placeable.kind = PlaceableKind.CraftingBench;
        placeable.placedObjectName = ItemName;
        placeable.placedScale = Vector3.one;

        return craftingBenchItem;
    }

    public static Sprite GetSprite()
    {
        if (craftingBenchSprite != null)
            return craftingBenchSprite;

        craftingBenchSprite = Resources.Load<Sprite>(IconPath);
        if (craftingBenchSprite != null)
            return craftingBenchSprite;

        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color wood = new Color(0.55f, 0.3f, 0.09f, 1f);
        Color darkWood = new Color(0.25f, 0.12f, 0.035f, 1f);
        Color cloth = new Color(0.12f, 0.48f, 0.66f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 13; y <= 20; y++)
        {
            for (int x = 3; x <= 28; x++)
                texture.SetPixel(x, y, y <= 14 || y >= 19 ? darkWood : wood);
        }

        for (int y = 20; y <= 22; y++)
        {
            for (int x = 7; x <= 24; x++)
                texture.SetPixel(x, y, cloth);
        }

        for (int y = 4; y <= 12; y++)
        {
            for (int x = 5; x <= 8; x++)
                texture.SetPixel(x, y, darkWood);
            for (int x = 23; x <= 26; x++)
                texture.SetPixel(x, y, darkWood);
        }

        texture.filterMode = FilterMode.Point;
        texture.Apply();
        craftingBenchSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
        craftingBenchSprite.name = "MesaDeCraftRuntimeSprite";

        return craftingBenchSprite;
    }
}
