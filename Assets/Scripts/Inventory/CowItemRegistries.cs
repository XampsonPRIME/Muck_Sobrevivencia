using UnityEngine;

public static class CowLeatherItemRegistry
{
    public const string ItemName = "Couro de Vaca";
    const string IconPath = "Icons/CouroDeVaca";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("CouroDeVacaItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Resources;
        item.rarity = ItemRarity.Common;
        item.description = "Couro obtido de vacas. Material basico para futuras receitas de equipamento e artesanato.";
        item.weight = 0.35f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 3;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null)
            return sprite;

        sprite = Resources.Load<Sprite>(IconPath);
        if (sprite != null)
            return sprite;

        sprite = CowItemIconFactory.CreateLeatherFallback();
        return sprite;
    }
}

public static class CowMeatItemRegistry
{
    public const string ItemName = "Carne de Vaca";
    const string IconPath = "Icons/CarneCruaVaca";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("CarneVacaItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Food;
        item.rarity = ItemRarity.Common;
        item.description = "Carne obtida de vacas. Pode ser preparada na fornalha.";
        item.weight = 0.6f;
        item.maxStack = 20;
        item.toolType = ToolType.None;
        item.toolDamage = 0;
        item.buyPrice = 0;
        item.sellPrice = 5;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null)
            return sprite;

        sprite = Resources.Load<Sprite>(IconPath);
        if (sprite == null)
            sprite = CowItemIconFactory.CreateMeatFallback();

        return sprite;
    }
}

public static class CookedCowMeatItemRegistry
{
    public const string ItemName = "Carne de Vaca Cozida";
    public const string ShortDisplayName = "Vaca cozida";
    const string IconPath = "Icons/CarneCozidaVaca";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("CarneVacaCozidaItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Consumable;
        item.category = InventoryCategory.Food;
        item.rarity = ItemRarity.Common;
        item.description = "Carne de vaca preparada na fornalha. Recupera bastante fome.";
        item.weight = 0.52f;
        item.maxStack = 20;
        item.toolType = ToolType.None;
        item.toolDamage = 0;
        item.buyPrice = 0;
        item.sellPrice = 9;
        item.icon = GetSprite();

        ConsumableItem consumable = itemObject.AddComponent<ConsumableItem>();
        consumable.healthRestore = 32f;
        consumable.hungerRestore = 70f;
        consumable.thirstRestore = 0f;
        consumable.consumeHoldTime = 1.1f;
        consumable.handLocalPosition = new Vector3(0.06f, 0.03f, 0.12f);
        consumable.handLocalEulerAngles = new Vector3(0f, -20f, 65f);
        consumable.handLocalScale = new Vector3(1.25f, 1.25f, 1.25f);

        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null)
            return sprite;

        sprite = Resources.Load<Sprite>(IconPath);
        if (sprite == null)
            sprite = HuntingIconFactory.CreateCookedMeatFallback();

        return sprite;
    }
}

static class CowItemIconFactory
{
    public static Sprite CreateMeatFallback()
    {
        Texture2D texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        texture.name = "CarneVacaFallbackSprite";
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        Color meat = new Color(0.62f, 0.15f, 0.12f, 1f);
        Color fat = new Color(0.95f, 0.68f, 0.55f, 1f);
        Color edge = new Color(0.35f, 0.08f, 0.06f, 1f);

        for (int y = 10; y <= 36; y++)
        {
            for (int x = 7; x <= 40; x++)
            {
                float dx = (x - 24f) / 18f;
                float dy = (y - 23f) / 12f;
                float distance = dx * dx + dy * dy;
                if (distance <= 1f)
                    texture.SetPixel(x, y, distance > 0.82f ? edge : meat);
            }
        }

        for (int i = 11; i <= 35; i++)
            texture.SetPixel(i, 20 + (i % 4), fat);

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
    }

    public static Sprite CreateLeatherFallback()
    {
        Texture2D texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        texture.name = "CouroDeVacaFallbackSprite";
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        Color hide = new Color(0.72f, 0.56f, 0.38f, 1f);
        Color dark = new Color(0.22f, 0.12f, 0.06f, 1f);
        Color light = new Color(0.93f, 0.86f, 0.72f, 1f);

        for (int y = 9; y <= 38; y++)
        {
            for (int x = 5; x <= 42; x++)
            {
                float dx = (x - 24f) / 20f;
                float dy = (y - 24f) / 14f;
                if (dx * dx + dy * dy > 1f)
                    continue;

                Color color = hide;
                if (Mathf.Sin(x * 0.32f) + Mathf.Cos(y * 0.45f) > 0.65f)
                    color = light;
                else if (Mathf.Sin((x + y) * 0.23f) < -0.35f)
                    color = dark;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
    }
}
