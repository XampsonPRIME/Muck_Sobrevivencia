using UnityEngine;

[DisallowMultipleComponent]
public class CraftingBench : MonoBehaviour, IPlayerInteractable
{
    public enum Recipe
    {
        Sticks,
        RustySword,
        Axe,
        Pickaxe,
        Furnace,
        Shield,
        Rope
    }

    [SerializeField] string inputItemNamePrefix = "Madeira";
    [SerializeField] int inputAmount = 1;
    [SerializeField] Item outputItem;
    [SerializeField] string fallbackOutputItemName = "Graveto";
    [SerializeField] int outputAmount = 4;

    static Sprite fallbackWoodIcon;

    public void Configure(Item gravetoItem)
    {
        if (gravetoItem != null)
            outputItem = gravetoItem;
    }

    public bool Interact(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null || playerInteraction.inventory == null)
            return false;

        CraftingBenchUI ui = CraftingBenchUI.Instance ?? FindFirstObjectByType<CraftingBenchUI>();
        if (ui == null)
        {
            GameObject uiObject = new GameObject("CraftingBenchUI");
            ui = uiObject.AddComponent<CraftingBenchUI>();
        }

        ui.Open(this, playerInteraction.inventory, playerInteraction.hotbar, playerInteraction);
        return true;
    }

    public bool TryCraftSticks(Inventory inventory, Hotbar hotbar, out string message)
    {
        return TryCraftSticks(inventory, hotbar, 1, out message);
    }

    public bool TryCraftSticks(Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int clampedCraftCount = Mathf.Max(1, craftCount);
        int amountToConsume = Mathf.Max(1, inputAmount) * clampedCraftCount;
        InventoryItem woodItem = FindWoodItem(inventory);

        if (woodItem == null || woodItem.quantity < amountToConsume)
        {
            message = "Voce precisa de madeira para fazer gravetos.";
            return false;
        }

        int amountToCreate = Mathf.Max(1, outputAmount) * clampedCraftCount;
        string outputName = outputItem != null ? outputItem.itemName : fallbackOutputItemName;

        inventory.RemoveItem(woodItem.itemName, amountToConsume);
        hotbar?.RemoveInventoryItem(woodItem, amountToConsume);
        inventory.AddItem(outputName, amountToCreate, outputItem);

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{amountToCreate} {outputName}";
        return true;
    }

    public bool TryCraftRecipe(Recipe recipe, Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        if (recipe == Recipe.RustySword || recipe == Recipe.Axe || recipe == Recipe.Pickaxe)
            return TryCraftToolRecipe(recipe, inventory, hotbar, craftCount, out message);

        if (recipe == Recipe.Furnace)
            return TryCraftFurnace(inventory, hotbar, craftCount, out message);

        if (recipe == Recipe.Shield)
            return TryCraftShield(inventory, hotbar, craftCount, out message);

        if (recipe == Recipe.Rope)
            return TryCraftRope(inventory, hotbar, craftCount, out message);

        return TryCraftSticks(inventory, hotbar, craftCount, out message);
    }

    public bool TryCraftFurnace(Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int amount = Mathf.Max(1, craftCount);
        if (!HasItem(inventory, "Pedras", 10 * amount))
        {
            message = "Voce precisa de 10 pedras para fazer a fornalha.";
            return false;
        }

        Item furnaceItem = FurnaceItemRegistry.GetOrCreate();
        ConsumeItem(inventory, hotbar, "Pedras", 10 * amount);
        inventory.AddItem(furnaceItem.itemName, amount, furnaceItem);
        hotbar?.TryAddInventoryItem(new InventoryItem(furnaceItem.itemName, amount, furnaceItem));

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{amount} {furnaceItem.itemName}";
        return true;
    }

    public bool TryCraftShield(Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int amount = Mathf.Max(1, craftCount);
        InventoryItem woodItem = FindWoodItem(inventory);
        if (!HasItem(inventory, RefinedIronItemRegistry.ItemName, 4 * amount) ||
            woodItem == null ||
            woodItem.quantity < 3 * amount)
        {
            message = "Voce precisa de 4 ferros refinados e 3 madeiras para fazer o escudo.";
            return false;
        }

        Item shieldItem = ShieldItemRegistry.GetOrCreate();
        ConsumeItem(inventory, hotbar, RefinedIronItemRegistry.ItemName, 4 * amount);
        inventory.RemoveItem(woodItem.itemName, 3 * amount);
        hotbar?.RemoveInventoryItem(woodItem, 3 * amount);

        inventory.AddItem(shieldItem.itemName, amount, shieldItem);
        hotbar?.TryAddInventoryItem(new InventoryItem(shieldItem.itemName, amount, shieldItem));

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{amount} {shieldItem.itemName}";
        return true;
    }

    public bool TryCraftRope(Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int amount = Mathf.Max(1, craftCount);
        if (!HasItem(inventory, WheatItemRegistry.ItemName, 4 * amount))
        {
            message = "Voce precisa de 4 trigos para fazer uma corda.";
            return false;
        }

        Item ropeItem = RopeItemRegistry.GetOrCreate();
        ConsumeItem(inventory, hotbar, WheatItemRegistry.ItemName, 4 * amount);

        inventory.AddItem(ropeItem.itemName, amount, ropeItem);
        hotbar?.TryAddInventoryItem(new InventoryItem(ropeItem.itemName, amount, ropeItem));

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{amount} {ropeItem.itemName}";
        return true;
    }

    public bool TryCraftToolRecipe(Recipe recipe, Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int amount = Mathf.Max(1, craftCount);
        if (!HasItem(inventory, "Graveto", 3 * amount) ||
            !HasItem(inventory, "Pedras", 2 * amount) ||
            !HasItem(inventory, RustyMetalItemRegistry.ItemName, amount))
        {
            message = $"Faltam recursos para criar {GetRecipeOutputName(recipe)}.";
            return false;
        }

        Item craftedItem = ResolveRecipeOutputItem(recipe);
        if (craftedItem == null)
        {
            message = "Item da receita nao encontrado.";
            return false;
        }

        ConsumeItem(inventory, hotbar, "Graveto", 3 * amount);
        ConsumeItem(inventory, hotbar, "Pedras", 2 * amount);
        ConsumeItem(inventory, hotbar, RustyMetalItemRegistry.ItemName, amount);

        inventory.AddItem(craftedItem.itemName, amount, craftedItem);
        hotbar?.TryAddInventoryItem(new InventoryItem(craftedItem.itemName, amount, craftedItem));

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{amount} {craftedItem.itemName}";
        return true;
    }

    public int GetAvailableCraftCount(Inventory inventory)
    {
        return GetAvailableCraftCount(Recipe.Sticks, inventory);
    }

    public int GetAvailableCraftCount(Recipe recipe, Inventory inventory)
    {
        if (UsesBasicToolRecipe(recipe))
        {
            int sticks = GetItemQuantity(inventory, "Graveto") / 3;
            int stones = GetItemQuantity(inventory, "Pedras") / 2;
            int rustyMetal = GetItemQuantity(inventory, RustyMetalItemRegistry.ItemName);
            return Mathf.Min(sticks, stones, rustyMetal);
        }

        if (recipe == Recipe.Furnace)
            return GetItemQuantity(inventory, "Pedras") / 10;

        if (recipe == Recipe.Shield)
        {
            InventoryItem woodItem = FindWoodItem(inventory);
            int refinedIron = GetItemQuantity(inventory, RefinedIronItemRegistry.ItemName) / 4;
            int wood = woodItem != null ? woodItem.quantity / 3 : 0;
            return Mathf.Min(refinedIron, wood);
        }

        if (recipe == Recipe.Rope)
            return GetItemQuantity(inventory, WheatItemRegistry.ItemName) / 4;

        return GetAvailableStickCraftCount(inventory);
    }

    int GetAvailableStickCraftCount(Inventory inventory)
    {
        InventoryItem woodItem = FindWoodItem(inventory);
        if (woodItem == null)
            return 0;

        return woodItem.quantity / Mathf.Max(1, inputAmount);
    }

    public Sprite GetRecipeOutputIcon(Recipe recipe)
    {
        Item item = ResolveRecipeOutputItem(recipe);
        return item != null ? item.icon : GetOutputIcon();
    }

    public string GetRecipeOutputName(Recipe recipe)
    {
        Item item = ResolveRecipeOutputItem(recipe);
        return item != null ? item.itemName : GetOutputName();
    }

    public int GetRecipeOutputAmount(Recipe recipe)
    {
        return UsesBasicToolRecipe(recipe) || recipe == Recipe.Furnace || recipe == Recipe.Shield || recipe == Recipe.Rope ? 1 : GetOutputAmount();
    }

    public string GetInputLabel()
    {
        return $"{Mathf.Max(1, inputAmount)} Madeira";
    }

    public int GetInputAmount()
    {
        return Mathf.Max(1, inputAmount);
    }

    public int GetOutputAmount()
    {
        return Mathf.Max(1, outputAmount);
    }

    public string GetOutputLabel()
    {
        string outputName = outputItem != null ? outputItem.itemName : fallbackOutputItemName;
        return $"{Mathf.Max(1, outputAmount)} {outputName}";
    }

    public string GetOutputName()
    {
        return outputItem != null ? outputItem.itemName : fallbackOutputItemName;
    }

    public Sprite GetOutputIcon()
    {
        return outputItem != null ? outputItem.icon : null;
    }

    public Sprite GetInputIcon(Inventory inventory)
    {
        InventoryItem woodItem = FindWoodItem(inventory);
        Sprite inventoryIcon = woodItem != null ? woodItem.GetDisplayIcon() : null;
        return inventoryIcon != null ? inventoryIcon : GetFallbackWoodIcon();
    }

    static Sprite GetFallbackWoodIcon()
    {
        if (fallbackWoodIcon != null)
            return fallbackWoodIcon;

        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color bark = new Color(0.43f, 0.23f, 0.08f, 1f);
        Color light = new Color(0.72f, 0.48f, 0.19f, 1f);
        Color ring = new Color(0.88f, 0.66f, 0.34f, 1f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 8; y <= 15; y++)
        {
            for (int x = 4; x <= 19; x++)
            {
                int edgeDistance = Mathf.Min(x - 4, 19 - x);
                Color color = edgeDistance <= 1 ? bark : light;
                texture.SetPixel(x, y, color);
            }
        }

        for (int y = 7; y <= 16; y++)
        {
            for (int x = 18; x <= 21; x++)
            {
                float dx = x - 19.5f;
                float dy = y - 11.5f;
                if (dx * dx / 5f + dy * dy / 18f <= 1f)
                    texture.SetPixel(x, y, ring);
            }
        }

        texture.Apply();
        fallbackWoodIcon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        fallbackWoodIcon.name = "MadeiraFallbackSprite";
        return fallbackWoodIcon;
    }

    InventoryItem FindWoodItem(Inventory inventory)
    {
        if (inventory == null || inventory.items == null)
            return null;

        for (int i = 0; i < inventory.items.Count; i++)
        {
            InventoryItem item = inventory.items[i];
            if (item == null || item.quantity < inputAmount || string.IsNullOrWhiteSpace(item.itemName))
                continue;

            if (item.itemName.Trim().StartsWith(inputItemNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    public InventoryItem FindItem(Inventory inventory, string itemName)
    {
        if (inventory == null || inventory.items == null || string.IsNullOrWhiteSpace(itemName))
            return null;

        for (int i = 0; i < inventory.items.Count; i++)
        {
            InventoryItem item = inventory.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                continue;

            if (string.Equals(item.itemName.Trim(), itemName, System.StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    int GetItemQuantity(Inventory inventory, string itemName)
    {
        InventoryItem item = FindItem(inventory, itemName);
        return item != null ? Mathf.Max(0, item.quantity) : 0;
    }

    bool HasItem(Inventory inventory, string itemName, int amount)
    {
        return GetItemQuantity(inventory, itemName) >= Mathf.Max(1, amount);
    }

    void ConsumeItem(Inventory inventory, Hotbar hotbar, string itemName, int amount)
    {
        InventoryItem item = FindItem(inventory, itemName);
        if (item == null)
            return;

        inventory.RemoveItem(item.itemName, amount);
        hotbar?.RemoveInventoryItem(item, amount);
    }

    bool UsesBasicToolRecipe(Recipe recipe)
    {
        return recipe == Recipe.RustySword || recipe == Recipe.Axe || recipe == Recipe.Pickaxe;
    }

    Item ResolveRecipeOutputItem(Recipe recipe)
    {
        switch (recipe)
        {
            case Recipe.RustySword:
                return RustySwordItemRegistry.GetOrCreate();
            case Recipe.Axe:
                return LoadResourceItem("VendorItems/Axe") ?? LoadResourceItem("Weapons/Axe");
            case Recipe.Pickaxe:
                return LoadResourceItem("VendorItems/Axepick") ?? LoadResourceItem("Weapons/Axepick");
            case Recipe.Furnace:
                return FurnaceItemRegistry.GetOrCreate();
            case Recipe.Shield:
                return ShieldItemRegistry.GetOrCreate();
            case Recipe.Rope:
                return RopeItemRegistry.GetOrCreate();
            default:
                return outputItem;
        }
    }

    Item LoadResourceItem(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        return prefab != null ? prefab.GetComponent<Item>() : null;
    }
}
