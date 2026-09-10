using UnityEngine;

public static class StoneFragmentItemRegistry
{
    public const string ItemName = "Fragmento de Pedra";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("FragmentoPedraItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Resources;
        item.rarity = ItemRarity.Common;
        item.description = "Pedaco de rocha natural obtido de criaturas de terra.";
        item.weight = 0.24f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 2;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = EarthGolemIconFactory.CreateStoneFragment();

        return sprite;
    }
}

public static class ResilientMossItemRegistry
{
    public const string ItemName = "Musgo Resiliente";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("MusgoResilienteItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Resources;
        item.rarity = ItemRarity.Common;
        item.description = "Musgo resistente que cresce entre pedras antigas.";
        item.weight = 0.08f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 3;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = EarthGolemIconFactory.CreateMoss();

        return sprite;
    }
}

public static class EarthCoreItemRegistry
{
    public const string ItemName = "Nucleo de Terra";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("NucleoTerraItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Resource;
        item.category = InventoryCategory.Special;
        item.rarity = ItemRarity.Rare;
        item.description = "Cristal raro pulsando com energia elemental de terra.";
        item.weight = 0.65f;
        item.maxStack = Item.ResourceStackLimit;
        item.sellPrice = 28;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = EarthGolemIconFactory.CreateEarthCore();

        return sprite;
    }
}

public static class AncestralCoreItemRegistry
{
    public const string ItemName = "Nucleo Ancestral";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("NucleoAncestralItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Quest;
        item.category = InventoryCategory.Special;
        item.rarity = ItemRarity.Legendary;
        item.description = "Nucleo antigo arrancado de um guardiao corrompido. Ainda pulsa com energia dos artefatos.";
        item.weight = 1.2f;
        item.maxStack = 1;
        item.sellPrice = 0;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = KaelTorDropIconFactory.CreateAncestralCore();

        return sprite;
    }
}

public static class FirstArtifactFragmentItemRegistry
{
    public const string ItemName = "Fragmento do Primeiro Artefato";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("FragmentoPrimeiroArtefatoItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Quest;
        item.category = InventoryCategory.Special;
        item.rarity = ItemRarity.Legendary;
        item.description = "Fragmento ligado ao primeiro artefato. A Ordem da Aurora jamais deveria toca-lo sem entender seu selo.";
        item.weight = 0.4f;
        item.maxStack = 1;
        item.sellPrice = 0;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = KaelTorDropIconFactory.CreateArtifactFragment();

        return sprite;
    }
}

public static class KaelTorTrophyItemRegistry
{
    public const string ItemName = "Trofeu de Kael'Tor";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("TrofeuKaelTorItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Quest;
        item.category = InventoryCategory.Special;
        item.rarity = ItemRarity.Legendary;
        item.description = "Lasca runica do Vigia dos Artefatos. Um trofeu pesado demais para parecer uma vitoria simples.";
        item.weight = 1.4f;
        item.maxStack = 1;
        item.sellPrice = 0;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = KaelTorDropIconFactory.CreateTrophy();

        return sprite;
    }
}

public static class FirstArtifactItemRegistry
{
    public const string ItemName = "Primeiro Artefato";

    static Item item;
    static Sprite sprite;

    public static Item GetOrCreate()
    {
        if (item != null)
            return item;

        GameObject itemObject = new GameObject("PrimeiroArtefatoItemData");
        Object.DontDestroyOnLoad(itemObject);

        item = itemObject.AddComponent<Item>();
        item.itemName = ItemName;
        item.itemType = ItemType.Quest;
        item.category = InventoryCategory.Special;
        item.rarity = ItemRarity.Legendary;
        item.description = "Artefato ancestral recuperado nas ruinas de Kael'Tor. Sua presenca parece chamar algo nas profundezas.";
        item.weight = 2f;
        item.maxStack = 1;
        item.sellPrice = 0;
        item.icon = GetSprite();
        return item;
    }

    public static Sprite GetSprite()
    {
        if (sprite == null)
            sprite = KaelTorDropIconFactory.CreateFirstArtifact();

        return sprite;
    }
}

static class KaelTorDropIconFactory
{
    public static Sprite CreateAncestralCore()
    {
        Texture2D texture = CreateClearTexture("NucleoAncestralSprite", 56, 56);
        DrawEllipse(texture, 28, 28, 19, 19, new Color(0.28f, 0.18f, 0.08f, 1f));
        DrawEllipse(texture, 28, 28, 13, 13, new Color(0.9f, 0.62f, 0.16f, 1f));
        DrawEllipse(texture, 24, 23, 5, 5, new Color(1f, 0.88f, 0.42f, 1f));
        DrawRune(texture, 28, 28, new Color(1f, 0.16f, 0.06f, 1f));
        texture.Apply();
        return BuildSprite(texture);
    }

    public static Sprite CreateArtifactFragment()
    {
        Texture2D texture = CreateClearTexture("FragmentoPrimeiroArtefatoSprite", 56, 56);
        DrawDiamond(texture, 28, 28, 18, 23, new Color(0.82f, 0.18f, 0.08f, 1f));
        DrawDiamond(texture, 24, 22, 7, 8, new Color(1f, 0.76f, 0.2f, 1f));
        DrawDiamond(texture, 35, 35, 7, 9, new Color(0.36f, 0.1f, 0.08f, 1f));
        texture.Apply();
        return BuildSprite(texture);
    }

    public static Sprite CreateTrophy()
    {
        Texture2D texture = CreateClearTexture("TrofeuKaelTorSprite", 56, 56);
        DrawEllipse(texture, 28, 20, 12, 9, new Color(0.42f, 0.31f, 0.22f, 1f));
        DrawRect(texture, 24, 27, 8, 15, new Color(0.7f, 0.48f, 0.18f, 1f));
        DrawRect(texture, 17, 41, 22, 5, new Color(0.34f, 0.2f, 0.1f, 1f));
        DrawEllipse(texture, 22, 19, 3, 3, new Color(1f, 0.72f, 0.16f, 1f));
        DrawEllipse(texture, 34, 19, 3, 3, new Color(1f, 0.72f, 0.16f, 1f));
        texture.Apply();
        return BuildSprite(texture);
    }

    public static Sprite CreateFirstArtifact()
    {
        Texture2D texture = CreateClearTexture("PrimeiroArtefatoSprite", 56, 56);
        DrawDiamond(texture, 28, 28, 16, 24, new Color(0.95f, 0.65f, 0.12f, 1f));
        DrawDiamond(texture, 28, 28, 8, 14, new Color(1f, 0.92f, 0.42f, 1f));
        DrawEllipse(texture, 28, 28, 4, 4, new Color(1f, 0.12f, 0.05f, 1f));
        texture.Apply();
        return BuildSprite(texture);
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

    static Sprite BuildSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 56f);
    }

    static void DrawDiamond(Texture2D texture, int cx, int cy, int width, int height, Color color)
    {
        for (int y = cy - height; y <= cy + height; y++)
        {
            for (int x = cx - width; x <= cx + width; x++)
            {
                float dx = Mathf.Abs(x - cx) / Mathf.Max(1f, width);
                float dy = Mathf.Abs(y - cy) / Mathf.Max(1f, height);
                if (dx + dy <= 1f)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    static void DrawEllipse(Texture2D texture, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / Mathf.Max(1f, rx);
                float dy = (y - cy) / Mathf.Max(1f, ry);
                if (dx * dx + dy * dy <= 1f)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
                SetPixelSafe(texture, xx, yy, color);
        }
    }

    static void DrawRune(Texture2D texture, int cx, int cy, Color color)
    {
        DrawRect(texture, cx - 1, cy - 11, 2, 22, color);
        DrawRect(texture, cx - 7, cy - 1, 14, 2, color);
        DrawDiamond(texture, cx, cy, 4, 4, color);
    }

    static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }
}

static class EarthGolemIconFactory
{
    public static Sprite CreateStoneFragment()
    {
        Texture2D texture = CreateClearTexture("FragmentoPedraSprite", 48, 48);
        Color dark = new Color(0.28f, 0.25f, 0.2f, 1f);
        Color mid = new Color(0.48f, 0.42f, 0.34f, 1f);
        Color light = new Color(0.72f, 0.62f, 0.46f, 1f);

        DrawDiamond(texture, 24, 24, 17, 14, mid);
        DrawDiamond(texture, 20, 20, 8, 7, light);
        DrawDiamond(texture, 30, 30, 8, 6, dark);
        texture.Apply();
        return BuildSprite(texture);
    }

    public static Sprite CreateMoss()
    {
        Texture2D texture = CreateClearTexture("MusgoResilienteSprite", 48, 48);
        Color dark = new Color(0.13f, 0.25f, 0.08f, 1f);
        Color mid = new Color(0.28f, 0.48f, 0.12f, 1f);
        Color light = new Color(0.55f, 0.72f, 0.22f, 1f);

        for (int i = 0; i < 9; i++)
        {
            int cx = 12 + i * 3 + (i % 2) * 2;
            int cy = 28 - (i % 3) * 3;
            DrawEllipse(texture, cx, cy, 6, 4, i % 2 == 0 ? mid : light);
            DrawRect(texture, cx - 1, cy, 2, 10, dark);
        }

        texture.Apply();
        return BuildSprite(texture);
    }

    public static Sprite CreateEarthCore()
    {
        Texture2D texture = CreateClearTexture("NucleoTerraSprite", 48, 48);
        Color outer = new Color(0.45f, 0.33f, 0.18f, 1f);
        Color core = new Color(0.78f, 0.62f, 0.24f, 1f);
        Color glow = new Color(0.95f, 0.86f, 0.38f, 1f);

        DrawDiamond(texture, 24, 24, 18, 18, outer);
        DrawDiamond(texture, 24, 24, 11, 12, core);
        DrawDiamond(texture, 22, 20, 4, 5, glow);
        texture.Apply();
        return BuildSprite(texture);
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

    static Sprite BuildSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);
    }

    static void DrawDiamond(Texture2D texture, int cx, int cy, int width, int height, Color color)
    {
        for (int y = cy - height; y <= cy + height; y++)
        {
            for (int x = cx - width; x <= cx + width; x++)
            {
                float dx = Mathf.Abs(x - cx) / Mathf.Max(1f, width);
                float dy = Mathf.Abs(y - cy) / Mathf.Max(1f, height);
                if (dx + dy <= 1f)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    static void DrawEllipse(Texture2D texture, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / Mathf.Max(1f, rx);
                float dy = (y - cy) / Mathf.Max(1f, ry);
                if (dx * dx + dy * dy <= 1f)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
                SetPixelSafe(texture, xx, yy, color);
        }
    }

    static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }
}
