using UnityEngine;

public static class FeatherItemRegistry
{
    public const string ItemName = "Pena";
    const string IconPath = "Icons/Pena";

    static Item featherItem;
    static Sprite featherSprite;
    static bool featherSpriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (featherItem != null)
            return featherItem;

        GameObject itemObject = new GameObject("PenaItemData");
        Object.DontDestroyOnLoad(itemObject);

        featherItem = itemObject.AddComponent<Item>();
        featherItem.itemName = ItemName;
        featherItem.itemType = ItemType.Resource;
        featherItem.category = InventoryCategory.Resources;
        featherItem.rarity = ItemRarity.Common;
        featherItem.description = "Material leve obtido de aves. Usado para fabricar flechas.";
        featherItem.weight = 0.02f;
        featherItem.maxStack = Item.ResourceStackLimit;
        featherItem.toolType = ToolType.None;
        featherItem.toolDamage = 0;
        featherItem.buyPrice = 0;
        featherItem.sellPrice = 2;
        featherItem.icon = GetSprite();

        return featherItem;
    }

    public static Sprite GetSprite()
    {
        if (featherSprite != null && (featherSpriteIsModelThumbnail || !Application.isPlaying))
            return featherSprite;

        Sprite modelThumbnail = ChickenFeatherVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            featherSprite = modelThumbnail;
            featherSpriteIsModelThumbnail = true;
            if (featherItem != null)
                featherItem.icon = featherSprite;
            return featherSprite;
        }

        if (featherSprite != null)
            return featherSprite;

        featherSprite = Resources.Load<Sprite>(IconPath);
        if (featherSprite == null)
            featherSprite = HuntingIconFactory.CreateFeatherFallback();

        featherSpriteIsModelThumbnail = false;
        return featherSprite;
    }
}

public static class RawChickenMeatItemRegistry
{
    public const string ItemName = "Carne Crua";
    const string IconPath = "Icons/CarneCruaGalinha";

    static Item rawMeatItem;
    static Sprite rawMeatSprite;
    static bool rawMeatSpriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (rawMeatItem != null)
            return rawMeatItem;

        GameObject itemObject = new GameObject("CarneCruaItemData");
        Object.DontDestroyOnLoad(itemObject);

        rawMeatItem = itemObject.AddComponent<Item>();
        rawMeatItem.itemName = ItemName;
        rawMeatItem.itemType = ItemType.Resource;
        rawMeatItem.category = InventoryCategory.Food;
        rawMeatItem.rarity = ItemRarity.Common;
        rawMeatItem.description = "Carne obtida atraves da caca. Pode ser preparada na fornalha futuramente.";
        rawMeatItem.weight = 0.35f;
        rawMeatItem.maxStack = 20;
        rawMeatItem.toolType = ToolType.None;
        rawMeatItem.toolDamage = 0;
        rawMeatItem.buyPrice = 0;
        rawMeatItem.sellPrice = 3;
        rawMeatItem.icon = GetSprite();

        return rawMeatItem;
    }

    public static Sprite GetSprite()
    {
        if (rawMeatSprite != null && (rawMeatSpriteIsModelThumbnail || !Application.isPlaying))
            return rawMeatSprite;

        Sprite modelThumbnail = RawChickenMeatVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            rawMeatSprite = modelThumbnail;
            rawMeatSpriteIsModelThumbnail = true;
            if (rawMeatItem != null)
                rawMeatItem.icon = rawMeatSprite;
            return rawMeatSprite;
        }

        if (rawMeatSprite != null)
            return rawMeatSprite;

        rawMeatSprite = Resources.Load<Sprite>(IconPath);
        if (rawMeatSprite == null)
            rawMeatSprite = HuntingIconFactory.CreateRawMeatFallback();

        rawMeatSpriteIsModelThumbnail = false;
        return rawMeatSprite;
    }
}

public static class CookedMeatItemRegistry
{
    public const string ItemName = "Carne Cozida";

    static Item cookedMeatItem;
    static Sprite cookedMeatSprite;

    public static Item GetOrCreate()
    {
        if (cookedMeatItem != null)
            return cookedMeatItem;

        GameObject itemObject = new GameObject("CarneCozidaItemData");
        Object.DontDestroyOnLoad(itemObject);

        cookedMeatItem = itemObject.AddComponent<Item>();
        cookedMeatItem.itemName = ItemName;
        cookedMeatItem.itemType = ItemType.Consumable;
        cookedMeatItem.category = InventoryCategory.Food;
        cookedMeatItem.rarity = ItemRarity.Common;
        cookedMeatItem.description = "Carne preparada na fornalha. Recupera vida e fome.";
        cookedMeatItem.weight = 0.32f;
        cookedMeatItem.maxStack = 20;
        cookedMeatItem.toolType = ToolType.None;
        cookedMeatItem.toolDamage = 0;
        cookedMeatItem.buyPrice = 0;
        cookedMeatItem.sellPrice = 7;
        cookedMeatItem.icon = GetSprite();

        ConsumableItem consumable = itemObject.AddComponent<ConsumableItem>();
        consumable.healthRestore = 28f;
        consumable.hungerRestore = 55f;
        consumable.thirstRestore = 0f;
        consumable.consumeHoldTime = 1.1f;
        consumable.handLocalPosition = new Vector3(0.06f, 0.03f, 0.12f);
        consumable.handLocalEulerAngles = new Vector3(0f, -20f, 65f);
        consumable.handLocalScale = new Vector3(1.25f, 1.25f, 1.25f);

        return cookedMeatItem;
    }

    public static Sprite GetSprite()
    {
        if (cookedMeatSprite != null)
            return cookedMeatSprite;

        cookedMeatSprite = HuntingIconFactory.CreateCookedMeatFallback();
        return cookedMeatSprite;
    }
}

public static class CookedChickenMeatItemRegistry
{
    public const string ItemName = "Carne de Galinha Cozida";
    public const string ShortDisplayName = "Galinha cozida";
    const string IconPath = "Icons/CarneCozidaGalinha";

    static Item cookedChickenMeatItem;
    static Sprite cookedChickenMeatSprite;
    static bool cookedChickenMeatSpriteIsModelThumbnail;

    public static Item GetOrCreate()
    {
        if (cookedChickenMeatItem != null)
            return cookedChickenMeatItem;

        GameObject itemObject = new GameObject("CarneGalinhaCozidaItemData");
        Object.DontDestroyOnLoad(itemObject);

        cookedChickenMeatItem = itemObject.AddComponent<Item>();
        cookedChickenMeatItem.itemName = ItemName;
        cookedChickenMeatItem.itemType = ItemType.Consumable;
        cookedChickenMeatItem.category = InventoryCategory.Food;
        cookedChickenMeatItem.rarity = ItemRarity.Common;
        cookedChickenMeatItem.description = "Carne de galinha preparada na fornalha. Recupera vida e fome.";
        cookedChickenMeatItem.weight = 0.3f;
        cookedChickenMeatItem.maxStack = 20;
        cookedChickenMeatItem.toolType = ToolType.None;
        cookedChickenMeatItem.toolDamage = 0;
        cookedChickenMeatItem.buyPrice = 0;
        cookedChickenMeatItem.sellPrice = 7;
        cookedChickenMeatItem.icon = GetSprite();

        ConsumableItem consumable = itemObject.AddComponent<ConsumableItem>();
        consumable.healthRestore = 26f;
        consumable.hungerRestore = 52f;
        consumable.thirstRestore = 0f;
        consumable.consumeHoldTime = 1.1f;
        consumable.handLocalPosition = new Vector3(0.06f, 0.03f, 0.12f);
        consumable.handLocalEulerAngles = new Vector3(0f, -20f, 65f);
        consumable.handLocalScale = new Vector3(1.25f, 1.25f, 1.25f);

        CookedChickenMeatVisualFactory.AttachTo(itemObject.transform);

        return cookedChickenMeatItem;
    }

    public static Sprite GetSprite()
    {
        if (cookedChickenMeatSprite != null && (cookedChickenMeatSpriteIsModelThumbnail || !Application.isPlaying))
            return cookedChickenMeatSprite;

        Sprite modelThumbnail = CookedChickenMeatVisualFactory.CreateThumbnailSprite();
        if (modelThumbnail != null)
        {
            cookedChickenMeatSprite = modelThumbnail;
            cookedChickenMeatSpriteIsModelThumbnail = true;
            if (cookedChickenMeatItem != null)
                cookedChickenMeatItem.icon = cookedChickenMeatSprite;
            return cookedChickenMeatSprite;
        }

        if (cookedChickenMeatSprite != null)
            return cookedChickenMeatSprite;

        cookedChickenMeatSprite = Resources.Load<Sprite>(IconPath);
        if (cookedChickenMeatSprite == null)
            cookedChickenMeatSprite = HuntingIconFactory.CreateCookedMeatFallback();

        cookedChickenMeatSpriteIsModelThumbnail = false;
        return cookedChickenMeatSprite;
    }
}

public static class ArrowItemRegistry
{
    public const string ItemName = "Flecha";
    const string IconPath = "Icons/Flecha";

    static Item arrowItem;
    static Sprite arrowSprite;

    public static Item GetOrCreate()
    {
        if (arrowItem != null)
            return arrowItem;

        GameObject itemObject = new GameObject("FlechaItemData");
        Object.DontDestroyOnLoad(itemObject);

        arrowItem = itemObject.AddComponent<Item>();
        arrowItem.itemName = ItemName;
        arrowItem.itemType = ItemType.Resource;
        arrowItem.category = InventoryCategory.Consumables;
        arrowItem.rarity = ItemRarity.Common;
        arrowItem.description = "Municao basica para arco simples.";
        arrowItem.weight = 0.05f;
        arrowItem.maxStack = 99;
        arrowItem.toolType = ToolType.None;
        arrowItem.toolDamage = 0;
        arrowItem.buyPrice = 0;
        arrowItem.sellPrice = 1;
        arrowItem.icon = GetSprite();

        return arrowItem;
    }

    public static Sprite GetSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        arrowSprite = Resources.Load<Sprite>(IconPath);
        if (arrowSprite == null)
            arrowSprite = HuntingIconFactory.CreateArrowFallback();

        return arrowSprite;
    }
}

public static class SimpleBowItemRegistry
{
    public const string ItemName = "Arco Simples";
    const string IconPath = "Icons/ArcoSimples";

    static Item bowItem;
    static Sprite bowSprite;

    public static Item GetOrCreate()
    {
        if (bowItem != null)
            return bowItem;

        GameObject itemObject = new GameObject("ArcoSimplesItemData");
        Object.DontDestroyOnLoad(itemObject);

        bowItem = itemObject.AddComponent<Item>();
        bowItem.itemName = ItemName;
        bowItem.itemType = ItemType.Tool;
        bowItem.category = InventoryCategory.Weapons;
        bowItem.rarity = ItemRarity.Common;
        bowItem.description = "Arco simples de madeira para combate a distancia.";
        bowItem.weight = 2.5f;
        bowItem.maxStack = Item.EquipmentStackLimit;
        bowItem.equipmentSlot = EquipmentSlotType.MainHand;
        bowItem.durability = 100;
        bowItem.toolType = ToolType.Bow;
        bowItem.toolDamage = 15;
        bowItem.buyPrice = 0;
        bowItem.sellPrice = 12;
        bowItem.icon = GetSprite();

        return bowItem;
    }

    public static Sprite GetSprite()
    {
        if (bowSprite != null)
            return bowSprite;

        bowSprite = Resources.Load<Sprite>(IconPath);
        if (bowSprite == null)
            bowSprite = HuntingIconFactory.CreateBowFallback();

        return bowSprite;
    }
}

static class HuntingIconFactory
{
    public static Sprite CreateBowFallback()
    {
        Texture2D texture = CreateClearTexture("ArcoSimplesFallbackSprite", 48, 48);
        Color wood = new Color(0.52f, 0.29f, 0.11f, 1f);
        Color woodLight = new Color(0.82f, 0.56f, 0.25f, 1f);
        Color stringColor = new Color(0.9f, 0.84f, 0.66f, 1f);

        Vector2 center = new Vector2(22f, 24f);
        for (int y = 7; y <= 41; y++)
        {
            float t = (y - 7f) / 34f;
            float arc = Mathf.Sin(t * Mathf.PI);
            int x = Mathf.RoundToInt(30f - arc * 13f);

            for (int dx = -2; dx <= 2; dx++)
                SetPixelSafe(texture, x + dx, y, dx == 0 ? woodLight : wood);

            int stringX = Mathf.RoundToInt(Mathf.Lerp(32f, 30f, Mathf.Abs(t - 0.5f) * 2f));
            SetPixelSafe(texture, stringX, y, stringColor);
        }

        for (int y = 22; y <= 27; y++)
        {
            for (int x = 16; x <= 29; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 7f)
                    SetPixelSafe(texture, x, y, Color.Lerp(wood, woodLight, 0.45f));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
    }

    public static Sprite CreateFeatherFallback()
    {
        Texture2D texture = CreateClearTexture("PenaFallbackSprite", 48, 48);
        Color shaft = new Color(0.72f, 0.52f, 0.24f, 1f);
        Color plume = new Color(0.94f, 0.9f, 0.78f, 1f);
        Color shade = new Color(0.46f, 0.39f, 0.3f, 1f);

        for (int y = 6; y < 42; y++)
        {
            int centerX = Mathf.RoundToInt(10 + y * 0.66f);
            for (int x = centerX - 1; x <= centerX + 1; x++)
                SetPixelSafe(texture, x, y, shaft);

            int width = Mathf.RoundToInt(Mathf.Lerp(2f, 12f, Mathf.Sin((y - 6f) / 36f * Mathf.PI)));
            for (int x = centerX - width; x <= centerX + width; x++)
            {
                float side = Mathf.Abs(x - centerX) / Mathf.Max(1f, width);
                if (side <= 1f)
                    SetPixelSafe(texture, x, y, Color.Lerp(plume, shade, side * 0.5f));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
    }

    public static Sprite CreateRawMeatFallback()
    {
        Texture2D texture = CreateClearTexture("CarneCruaFallbackSprite", 32, 32);
        Color meat = new Color(0.72f, 0.18f, 0.18f, 1f);
        Color fat = new Color(1f, 0.76f, 0.62f, 1f);

        for (int y = 7; y <= 24; y++)
        {
            for (int x = 5; x <= 26; x++)
            {
                float dx = (x - 15.5f) / 11f;
                float dy = (y - 15.5f) / 8.5f;
                if (dx * dx + dy * dy <= 1f)
                    texture.SetPixel(x, y, meat);
            }
        }

        for (int y = 13; y <= 18; y++)
        {
            for (int x = 10; x <= 22; x++)
            {
                if ((x + y) % 3 == 0)
                    texture.SetPixel(x, y, fat);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
    }

    public static Sprite CreateCookedMeatFallback()
    {
        Texture2D texture = CreateClearTexture("CarneCozidaFallbackSprite", 32, 32);
        Color crust = new Color(0.48f, 0.19f, 0.08f, 1f);
        Color meat = new Color(0.86f, 0.42f, 0.18f, 1f);
        Color charred = new Color(0.14f, 0.07f, 0.04f, 1f);
        Color shine = new Color(1f, 0.72f, 0.38f, 1f);

        for (int y = 7; y <= 24; y++)
        {
            for (int x = 5; x <= 26; x++)
            {
                float dx = (x - 15.5f) / 11f;
                float dy = (y - 15.5f) / 8.5f;
                float distance = dx * dx + dy * dy;
                if (distance <= 1f)
                    texture.SetPixel(x, y, distance > 0.78f ? crust : meat);
            }
        }

        for (int y = 12; y <= 21; y += 4)
        {
            for (int x = 9; x <= 23; x++)
            {
                if ((x + y) % 2 == 0)
                    texture.SetPixel(x, y, charred);
            }
        }

        for (int y = 10; y <= 13; y++)
        {
            for (int x = 11; x <= 18; x++)
            {
                if ((x - 11) + (y - 10) < 8)
                    texture.SetPixel(x, y, shine);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
    }

    public static Sprite CreateArrowFallback()
    {
        Texture2D texture = CreateClearTexture("FlechaFallbackSprite", 48, 48);
        Color wood = new Color(0.56f, 0.32f, 0.12f, 1f);
        Color stone = new Color(0.72f, 0.72f, 0.68f, 1f);
        Color feather = new Color(0.92f, 0.88f, 0.74f, 1f);

        for (int i = 8; i <= 39; i++)
            SetPixelSafe(texture, i, i, wood);

        for (int y = 34; y <= 43; y++)
        {
            for (int x = 34; x <= 43; x++)
            {
                if (x + y > 76 && Mathf.Abs(x - y) < 7)
                    SetPixelSafe(texture, x, y, stone);
            }
        }

        for (int i = 8; i <= 17; i++)
        {
            SetPixelSafe(texture, i, i + 4, feather);
            SetPixelSafe(texture, i + 4, i, feather);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
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

    static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (texture == null || x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }
}
