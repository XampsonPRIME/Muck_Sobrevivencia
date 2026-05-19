using UnityEngine;

[DisallowMultipleComponent]
public class CraftingBench : MonoBehaviour, IPlayerInteractable
{
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

    public int GetAvailableCraftCount(Inventory inventory)
    {
        InventoryItem woodItem = FindWoodItem(inventory);
        if (woodItem == null)
            return 0;

        return woodItem.quantity / Mathf.Max(1, inputAmount);
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
}
