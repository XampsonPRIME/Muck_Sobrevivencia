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
        Rope,
        Bow,
        Arrows
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

        CraftingBenchUI ui = CraftingBenchUI.Instance ?? SceneObjectCache.Find<CraftingBenchUI>(true);
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
        Item resolvedOutputItem = outputItem != null ? outputItem : StickResourceItemRegistry.GetOrCreate();
        string outputName = resolvedOutputItem.itemName;
        if (!CanReceiveCraftResult(inventory, outputName, amountToCreate, resolvedOutputItem, out message))
            return false;

        inventory.RemoveItem(woodItem.itemName, amountToConsume);
        hotbar?.RemoveInventoryItem(woodItem, amountToConsume);
        inventory.AddItem(outputName, amountToCreate, resolvedOutputItem);

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

        if (recipe == Recipe.Bow)
            return TryCraftBow(inventory, hotbar, craftCount, out message);

        if (recipe == Recipe.Arrows)
            return TryCraftArrows(inventory, hotbar, craftCount, out message);

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
        if (!CanReceiveCraftResult(inventory, furnaceItem.itemName, amount, furnaceItem, out message))
            return false;

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
        if (!CanReceiveCraftResult(inventory, shieldItem.itemName, amount, shieldItem, out message))
            return false;

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
        if (!CanReceiveCraftResult(inventory, ropeItem.itemName, amount, ropeItem, out message))
            return false;

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

        if (!CanReceiveCraftResult(inventory, craftedItem.itemName, amount, craftedItem, out message))
            return false;

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

    public bool TryCraftArrows(Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int amount = Mathf.Max(1, craftCount);
        if (!HasItem(inventory, FeatherItemRegistry.ItemName, amount) ||
            !HasItem(inventory, "Graveto", amount) ||
            !HasItem(inventory, "Pedras", amount))
        {
            message = "Voce precisa de pena, graveto e pedra para fazer flechas.";
            return false;
        }

        Item arrowItem = ArrowItemRegistry.GetOrCreate();
        int arrowAmount = 2 * amount;
        if (!CanReceiveCraftResult(inventory, arrowItem.itemName, arrowAmount, arrowItem, out message))
            return false;

        ConsumeItem(inventory, hotbar, FeatherItemRegistry.ItemName, amount);
        ConsumeItem(inventory, hotbar, "Graveto", amount);
        ConsumeItem(inventory, hotbar, "Pedras", amount);

        inventory.AddItem(arrowItem.itemName, arrowAmount, arrowItem);

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{arrowAmount} {arrowItem.itemName}";
        return true;
    }

    public bool TryCraftBow(Inventory inventory, Hotbar hotbar, int craftCount, out string message)
    {
        message = "Craft indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        int amount = Mathf.Max(1, craftCount);
        InventoryItem woodItem = FindWoodItem(inventory);
        if (woodItem == null ||
            woodItem.quantity < 5 * amount ||
            !HasItem(inventory, RopeItemRegistry.ItemName, 3 * amount))
        {
            message = "Voce precisa de 5 madeiras e 3 cordas para fazer um arco.";
            return false;
        }

        Item bowItem = SimpleBowItemRegistry.GetOrCreate();
        if (!CanReceiveCraftResult(inventory, bowItem.itemName, amount, bowItem, out message))
            return false;

        inventory.RemoveItem(woodItem.itemName, 5 * amount);
        hotbar?.RemoveInventoryItem(woodItem, 5 * amount);
        ConsumeItem(inventory, hotbar, RopeItemRegistry.ItemName, 3 * amount);

        inventory.AddItem(bowItem.itemName, amount, bowItem);
        hotbar?.TryAddInventoryItem(new InventoryItem(bowItem.itemName, amount, bowItem));

        InventoryUI inventoryUi = SceneObjectCache.Find<InventoryUI>(gameObject.scene, true);
        if (inventoryUi != null)
            inventoryUi.Refresh();

        message = $"+{amount} {bowItem.itemName}";
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

        if (recipe == Recipe.Bow)
        {
            InventoryItem woodItem = FindWoodItem(inventory);
            int wood = woodItem != null ? woodItem.quantity / 5 : 0;
            int rope = GetItemQuantity(inventory, RopeItemRegistry.ItemName) / 3;
            return Mathf.Min(wood, rope);
        }

        if (recipe == Recipe.Arrows)
        {
            int feathers = GetItemQuantity(inventory, FeatherItemRegistry.ItemName);
            int sticks = GetItemQuantity(inventory, "Graveto");
            int stones = GetItemQuantity(inventory, "Pedras");
            return Mathf.Min(feathers, sticks, stones);
        }

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
        if (recipe == Recipe.Arrows)
            return 2;

        return UsesBasicToolRecipe(recipe) || recipe == Recipe.Furnace || recipe == Recipe.Shield || recipe == Recipe.Rope || recipe == Recipe.Bow ? 1 : GetOutputAmount();
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

    bool CanReceiveCraftResult(Inventory inventory, string itemName, int amount, Item itemData, out string message)
    {
        message = "Inventario cheio.";

        if (inventory == null || string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return false;

        InventoryItem result = new InventoryItem(itemName, amount, itemData);
        if (inventory.CanFitItem(result))
            return true;

        message = "Inventario cheio. Libere espaco antes de craftar.";
        return false;
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
            case Recipe.Bow:
                return SimpleBowItemRegistry.GetOrCreate();
            case Recipe.Arrows:
                return ArrowItemRegistry.GetOrCreate();
            default:
                return outputItem != null ? outputItem : StickResourceItemRegistry.GetOrCreate();
        }
    }

    Item LoadResourceItem(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        return prefab != null ? prefab.GetComponent<Item>() : null;
    }
}
