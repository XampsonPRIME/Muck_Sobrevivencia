using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class VendorOffer
{
    public Item itemPrefab;
    [Min(0)] public int startingStock = 1;
    public bool infiniteStock;
    [SerializeField] int currentStock = -1;

    public int CurrentStock
    {
        get
        {
            EnsureInitialized();
            return infiniteStock ? int.MaxValue : currentStock;
        }
    }

    public bool HasStock
    {
        get
        {
            EnsureInitialized();
            return infiniteStock || currentStock > 0;
        }
    }

    public void EnsureInitialized()
    {
        if (currentStock >= 0)
            return;

        currentStock = Mathf.Max(0, startingStock);
    }

    public void ConsumeOne()
    {
        EnsureInitialized();

        if (infiniteStock)
            return;

        currentStock = Mathf.Max(0, currentStock - 1);
    }

    public void ResetStock()
    {
        currentStock = Mathf.Max(0, startingStock);
    }

    public void SetCurrentStock(int stock)
    {
        currentStock = Mathf.Max(0, stock);
    }
}

[Serializable]
public class VendorStockOverride
{
    public string itemName;
    [Min(0)] public int stock = 1;
    public bool infiniteStock;
}

public class VendorShop : MonoBehaviour
{
    public string vendorName = "Vendedor";
    [Header("Fonte dos itens")]
    public bool loadOffersFromResources = true;
    public string resourcesFolder = "VendorItems";
    [Min(0)] public int defaultStock = 1;
    public bool defaultInfiniteStock;

    [Header("Overrides por item")]
    public List<VendorStockOverride> stockOverrides = new List<VendorStockOverride>();

    [Header("Itens configurados")]
    public List<VendorOffer> offers = new List<VendorOffer>();

    void Awake()
    {
        RebuildOffersIfNeeded();
        EnsureOffersInitialized();
    }

    void OnValidate()
    {
        RebuildOffersIfNeeded();
        EnsureOffersInitialized();
    }

    public IReadOnlyList<VendorOffer> GetOffers()
    {
        RebuildOffersIfNeeded();
        EnsureOffersInitialized();
        return offers;
    }

    public bool TryBuy(int offerIndex, Inventory inventory, Hotbar hotbar, out string message)
    {
        message = "Compra indisponivel.";

        if (inventory == null)
        {
            message = "Inventario nao encontrado.";
            return false;
        }

        if (offerIndex < 0 || offerIndex >= offers.Count)
            return false;

        VendorOffer offer = offers[offerIndex];
        if (offer == null || offer.itemPrefab == null)
        {
            message = "Item do vendedor nao configurado.";
            return false;
        }

        offer.EnsureInitialized();
        if (!offer.HasStock)
        {
            message = "Item esgotado.";
            return false;
        }

        int buyPrice = offer.itemPrefab.GetBuyPrice();
        if (!inventory.HasEnoughGold(buyPrice))
        {
            message = "Gold insuficiente.";
            return false;
        }

        if (!inventory.TrySpendGold(buyPrice))
        {
            message = "Nao foi possivel gastar o gold.";
            return false;
        }

        inventory.AddItem(offer.itemPrefab.itemName, 1, offer.itemPrefab);
        TryAddToHotbar(hotbar, offer.itemPrefab);
        offer.ConsumeOne();

        message = $"Comprou {offer.itemPrefab.itemName}.";
        return true;
    }

    public bool TrySell(Inventory inventory, Hotbar hotbar, InventoryItem inventoryItem, out string message)
    {
        message = "Venda indisponivel.";

        if (inventory == null || inventoryItem == null)
        {
            message = "Item invalido.";
            return false;
        }

        if (inventoryItem.itemName == "Gold")
        {
            message = "Nao da para vender gold.";
            return false;
        }

        int sellPrice = inventoryItem.GetSellPrice();
        if (sellPrice <= 0)
        {
            message = "Esse item nao pode ser vendido.";
            return false;
        }

        if (!inventory.RemoveItem(inventoryItem.itemName, 1))
        {
            message = "Nao foi possivel remover o item.";
            return false;
        }

        inventory.AddGold(sellPrice);
        hotbar?.RemoveInventoryItem(inventoryItem, 1);
        message = $"Vendeu {inventoryItem.itemName}.";
        return true;
    }

    public void RestockAll()
    {
        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i] == null)
                continue;

            offers[i].ResetStock();
        }
    }

    void EnsureOffersInitialized()
    {
        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i] == null)
                continue;

            if (Application.isPlaying)
                offers[i].EnsureInitialized();
            else
                offers[i].ResetStock();
        }
    }

    void RebuildOffersIfNeeded()
    {
        if (!loadOffersFromResources)
            return;

        Item[] loadedItems = LoadItemsFromResourcesFolder();
        if (loadedItems == null)
            return;

        Dictionary<string, VendorOffer> previousOffersByName = offers
            .Where(offer => offer != null && offer.itemPrefab != null && !string.IsNullOrWhiteSpace(offer.itemPrefab.itemName))
            .GroupBy(offer => offer.itemPrefab.itemName)
            .ToDictionary(group => group.Key, group => group.First());

        List<VendorOffer> rebuiltOffers = new List<VendorOffer>(loadedItems.Length);
        for (int i = 0; i < loadedItems.Length; i++)
        {
            Item item = loadedItems[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                continue;

            VendorOffer offer = new VendorOffer
            {
                itemPrefab = item,
                startingStock = defaultStock,
                infiniteStock = defaultInfiniteStock
            };

            ApplyOverride(offer, item.itemName);

            if (previousOffersByName.TryGetValue(item.itemName, out VendorOffer previousOffer) && previousOffer != null)
            {
                if (Application.isPlaying)
                {
                    offer.EnsureInitialized();

                    if (!offer.infiniteStock && !previousOffer.infiniteStock)
                    {
                        int previousCurrentStock = previousOffer.CurrentStock;
                        int consumedAmount = Mathf.Max(0, previousOffer.startingStock - previousCurrentStock);
                        int syncedStock = Mathf.Max(0, offer.startingStock - consumedAmount);
                        offer.SetCurrentStock(syncedStock);
                    }
                }
            }

            rebuiltOffers.Add(offer);
        }

        offers = rebuiltOffers;
    }

    Item[] LoadItemsFromResourcesFolder()
    {
        string trimmedFolder = string.IsNullOrWhiteSpace(resourcesFolder) ? string.Empty : resourcesFolder.Trim().Trim('/');
        return Resources.LoadAll<Item>(trimmedFolder);
    }

    void ApplyOverride(VendorOffer offer, string itemName)
    {
        if (offer == null || string.IsNullOrWhiteSpace(itemName))
            return;

        for (int i = 0; i < stockOverrides.Count; i++)
        {
            VendorStockOverride stockOverride = stockOverrides[i];
            if (stockOverride == null || string.IsNullOrWhiteSpace(stockOverride.itemName))
                continue;

            if (!string.Equals(stockOverride.itemName.Trim(), itemName.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            offer.startingStock = Mathf.Max(0, stockOverride.stock);
            offer.infiniteStock = stockOverride.infiniteStock;
            return;
        }
    }

    static void TryAddToHotbar(Hotbar hotbar, Item item)
    {
        if (hotbar == null || item == null)
            return;

        if (item.itemType != ItemType.Tool && item.itemType != ItemType.Consumable)
            return;

        hotbar.TryAddInventoryItem(new InventoryItem(item.itemName, 1, item));
    }
}
