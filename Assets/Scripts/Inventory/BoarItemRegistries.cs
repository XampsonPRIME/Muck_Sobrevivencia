using UnityEngine;

public static class ThickLeatherItemRegistry
{
    public const string ItemName = "Couro Grosso";
    const string IconPath = "Icons/CouroDeJavali";

    static Item item;
    static Sprite sprite;
    static bool spriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("CouroGrossoItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Resources;
        item.rarity = ItemRarity.Common;
        item.description = "Couro resistente obtido de javalis selvagens.";
        item.weight = 0.45f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 4;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null && (spriteIsModelThumbnail || !Application.isPlaying))
            return sprite;

        Sprite modelThumbnail = BoarLeatherVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            sprite = modelThumbnail;
            spriteIsModelThumbnail = true;
            if (item != null)
                item.icon = sprite;
            return sprite;
        }

        if (sprite != null)
            return sprite;

        sprite = Resources.Load<Sprite>(IconPath);
        if (sprite == null)
            sprite = BoarDropIconFactory.CreateLeather();

        spriteIsModelThumbnail = false;
        return sprite;
    }
}

public static class SharpTuskItemRegistry
{
    public const string ItemName = "Presa Afiada";

    static Item item;
    static Sprite sprite;
    static bool spriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("PresaAfiadaItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Resources;
        item.rarity = ItemRarity.Uncommon;
        item.description = "Presa curva e afiada usada em futuras receitas de combate.";
        item.weight = 0.28f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 8;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null && (spriteIsModelThumbnail || !Application.isPlaying))
            return sprite;

        Sprite modelThumbnail = BoarTuskVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            sprite = modelThumbnail;
            spriteIsModelThumbnail = true;
            if (item != null)
                item.icon = sprite;
            return sprite;
        }

        if (sprite != null)
            return sprite;

        sprite = BoarDropIconFactory.CreateTusk();
        spriteIsModelThumbnail = false;

        return sprite;
    }
}

public static class BoarMeatItemRegistry
{
    public const string ItemName = "Carne de Javali";
    const string IconPath = "Icons/CarneCruaJavali";

    static Item item;
    static Sprite sprite;
    static bool spriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("CarneJavaliItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Food;
        item.rarity = ItemRarity.Uncommon;
        item.description = "Carne crua obtida de javalis. Pode ser preparada na fornalha.";
        item.weight = 0.55f;
        item.maxStack = 20;
        item.sellPrice = 5;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null && (spriteIsModelThumbnail || !Application.isPlaying))
            return sprite;

        Sprite modelThumbnail = RawBoarMeatVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            sprite = modelThumbnail;
            spriteIsModelThumbnail = true;
            if (item != null)
                item.icon = sprite;
            return sprite;
        }

        if (sprite != null)
            return sprite;

        sprite = Resources.Load<Sprite>(IconPath);
        if (sprite == null)
            sprite = BoarDropIconFactory.CreateMeat();

        spriteIsModelThumbnail = false;
        return sprite;
    }
}

public static class CookedBoarMeatItemRegistry
{
    public const string ItemName = "Carne de Javali Cozida";
    public const string ShortDisplayName = "Javali cozido";
    const string IconPath = "Icons/CarneCozidaJavali";

    static Item item;
    static Sprite sprite;
    static bool spriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("CarneJavaliCozidaItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Consumable;
        item.category = InventoryCategory.Food;
        item.rarity = ItemRarity.Uncommon;
        item.description = "Carne de javali preparada na fornalha. Recupera vida e fome.";
        item.weight = 0.48f;
        item.maxStack = 20;
        item.toolType = ToolType.None;
        item.toolDamage = 0;
        item.buyPrice = 0;
        item.sellPrice = 9;
        item.icon = GetSprite();

        ConsumableItem consumable = itemObject.AddComponent<ConsumableItem>();
        consumable.healthRestore = 34f;
        consumable.hungerRestore = 68f;
        consumable.thirstRestore = 0f;
        consumable.consumeHoldTime = 1.1f;
        consumable.handLocalPosition = new Vector3(0.06f, 0.03f, 0.12f);
        consumable.handLocalEulerAngles = new Vector3(0f, -20f, 65f);
        consumable.handLocalScale = new Vector3(1.25f, 1.25f, 1.25f);

        CookedBoarMeatVisualFactory.AttachTo(itemObject.transform);

        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite != null && (spriteIsModelThumbnail || !Application.isPlaying))
            return sprite;

        Sprite modelThumbnail = CookedBoarMeatVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            sprite = modelThumbnail;
            spriteIsModelThumbnail = true;
            if (item != null)
                item.icon = sprite;
            return sprite;
        }

        if (sprite != null)
            return sprite;

        sprite = Resources.Load<Sprite>(IconPath);
        if (sprite == null)
            sprite = HuntingIconFactory.CreateCookedMeatFallback();

        spriteIsModelThumbnail = false;
        return sprite;
    }
}

public static class BoarTrophyItemRegistry
{
    public const string ItemName = "Trofeu de Javali";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("TrofeuJavaliItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Special;
        item.rarity = ItemRarity.Rare;
        item.description = "Trofeu raro de caca. Futuramente usado em decoracao e conquistas.";
        item.weight = 1.2f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 22;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = BoarDropIconFactory.CreateTrophy();

        return sprite;
    }
}

static class BoarDropIconFactory
{
    public static Sprite CreateLeather()
    {
        Texture2D texture = CreateClearTexture("CouroGrossoSprite", 48, 48);
        Color leather = new Color(0.45f, 0.25f, 0.11f, 1f);
        Color edge = new Color(0.25f, 0.13f, 0.06f, 1f);

        for (int y = 10; y <= 37; y++)
        {
            for (int x = 8; x <= 39; x++)
            {
                float dx = (x - 24f) / 17f;
                float dy = (y - 24f) / 13f;
                if (dx * dx + dy * dy <= 1f)
                    texture.SetPixel(x, y, Mathf.Abs(dx) > 0.82f || Mathf.Abs(dy) > 0.82f ? edge : leather);
            }
        }

        texture.Apply();
        return BuildSprite(texture, 48f);
    }

    public static Sprite CreateTusk()
    {
        Texture2D texture = CreateClearTexture("PresaAfiadaSprite", 48, 48);
        Color ivory = new Color(0.92f, 0.82f, 0.58f, 1f);
        Color shade = new Color(0.58f, 0.45f, 0.28f, 1f);

        for (int i = 8; i <= 38; i++)
        {
            int x = i;
            int y = Mathf.RoundToInt(36f - Mathf.Sin((i - 8f) / 30f * Mathf.PI) * 18f);
            for (int w = -2; w <= 2; w++)
                SetPixelSafe(texture, x, y + w, w == 2 ? shade : ivory);
        }

        texture.Apply();
        return BuildSprite(texture, 48f);
    }

    public static Sprite CreateMeat()
    {
        Texture2D texture = CreateClearTexture("CarneJavaliSprite", 48, 48);
        Color meat = new Color(0.62f, 0.17f, 0.12f, 1f);
        Color fat = new Color(0.96f, 0.68f, 0.5f, 1f);

        for (int y = 10; y <= 36; y++)
        {
            for (int x = 8; x <= 40; x++)
            {
                float dx = (x - 24f) / 17f;
                float dy = (y - 23f) / 12f;
                if (dx * dx + dy * dy <= 1f)
                    texture.SetPixel(x, y, meat);
            }
        }

        for (int i = 12; i <= 34; i++)
            SetPixelSafe(texture, i, 20 + (i % 3), fat);

        texture.Apply();
        return BuildSprite(texture, 48f);
    }

    public static Sprite CreateTrophy()
    {
        Texture2D texture = CreateClearTexture("TrofeuJavaliSprite", 48, 48);
        Color gold = new Color(0.82f, 0.57f, 0.16f, 1f);
        Color dark = new Color(0.35f, 0.2f, 0.07f, 1f);
        Color ivory = new Color(0.92f, 0.82f, 0.58f, 1f);

        DrawRect(texture, 18, 12, 12, 18, gold);
        DrawRect(texture, 15, 30, 18, 4, dark);
        DrawRect(texture, 12, 35, 24, 5, dark);

        for (int i = 0; i < 12; i++)
        {
            SetPixelSafe(texture, 14 - i, 19 - i / 2, ivory);
            SetPixelSafe(texture, 33 + i, 19 - i / 2, ivory);
        }

        texture.Apply();
        return BuildSprite(texture, 48f);
    }

    static Texture2D CreateClearTexture(string name, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Point;
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, clear);
        }

        return texture;
    }

    static Sprite BuildSprite(Texture2D texture, float pixelsPerUnit)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    static void DrawRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
                SetPixelSafe(texture, x, y, color);
        }
    }

    static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (texture == null || x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }
}
