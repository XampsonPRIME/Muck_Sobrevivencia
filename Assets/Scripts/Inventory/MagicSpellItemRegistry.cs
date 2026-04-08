using UnityEngine;

public static class MagicSpellItemRegistry
{
    static Item magicItem;
    static Sprite magicSprite;

    public static Item GetOrCreate()
    {
        MagicSpellConfig config = MagicSpellConfig.FindConfig();

        if (magicItem != null)
        {
            ApplyConfig(magicItem, config);
            return magicItem;
        }

        GameObject itemObject = new GameObject("MagicSpellItemData");
        Object.DontDestroyOnLoad(itemObject);

        magicItem = itemObject.AddComponent<Item>();
        magicItem.itemType = ItemType.Consumable;
        magicItem.toolType = ToolType.None;
        magicItem.toolDamage = 0;

        ConsumableItem consumable = itemObject.AddComponent<ConsumableItem>();
        consumable.healthRestore = 0f;
        consumable.hungerRestore = 0f;
        consumable.thirstRestore = 0f;

        MagicSpellConsumable magicConsumable = itemObject.AddComponent<MagicSpellConsumable>();
        ApplyConfig(magicItem, config, consumable, magicConsumable);
        return magicItem;
    }

    static void ApplyConfig(Item item, MagicSpellConfig config)
    {
        if (item == null)
            return;

        ConsumableItem consumable = item.GetComponent<ConsumableItem>();
        MagicSpellConsumable magicConsumable = item.GetComponent<MagicSpellConsumable>();
        ApplyConfig(item, config, consumable, magicConsumable);
    }

    static void ApplyConfig(Item item, MagicSpellConfig config, ConsumableItem consumable, MagicSpellConsumable magicConsumable)
    {
        if (item == null)
            return;

        string itemName = config != null && !string.IsNullOrWhiteSpace(config.itemName) ? config.itemName : "Magia Ancestral";
        item.itemName = itemName;
        item.icon = GetMagicSprite(config);

        if (consumable != null)
        {
            consumable.consumeHoldTime = config != null ? config.consumeHoldTime : 0.6f;
            consumable.handLocalPosition = config != null ? config.handLocalPosition : new Vector3(0.06f, 0.02f, 0.12f);
            consumable.handLocalEulerAngles = config != null ? config.handLocalEulerAngles : new Vector3(8f, 0f, 88f);
            consumable.handLocalScale = config != null ? config.handLocalScale : new Vector3(0.65f, 0.65f, 0.65f);
        }

        if (magicConsumable != null)
            magicConsumable.magicName = config != null && !string.IsNullOrWhiteSpace(config.magicName) ? config.magicName : itemName;
    }

    public static Sprite GetMagicSprite(MagicSpellConfig config = null)
    {
        if (config != null && config.icon != null)
            return config.icon;

        if (magicSprite != null)
            return magicSprite;

        magicSprite = Resources.Load<Sprite>("Icons/Gold");
        if (magicSprite != null)
            return magicSprite;

        Texture2D texture = Resources.Load<Texture2D>("Icons/Gold");
        if (texture == null)
            return null;

        magicSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        magicSprite.name = "MagicSpellRuntimeSprite";
        return magicSprite;
    }
}
