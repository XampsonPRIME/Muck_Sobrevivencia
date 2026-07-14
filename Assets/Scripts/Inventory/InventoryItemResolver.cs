using System;
using UnityEngine;

public static class InventoryItemResolver
{
    public static Item Resolve(string itemName, string prefabName = "")
    {
        if (string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(prefabName))
            return null;

        if (string.Equals(itemName, "Gold", StringComparison.OrdinalIgnoreCase))
            return GoldItemRegistry.GetOrCreate();

        if (string.Equals(itemName, "Magia Ancestral", StringComparison.OrdinalIgnoreCase))
            return MagicSpellItemRegistry.GetOrCreate();

        if (string.Equals(itemName, OakWoodItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return OakWoodItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RustyMetalItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RustyMetalItemRegistry.GetOrCreate();

        if (string.Equals(itemName, IronItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(itemName, "Ferro", StringComparison.OrdinalIgnoreCase))
            return IronItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RefinedIronItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RefinedIronItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RustySwordItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RustySwordItemRegistry.GetOrCreate();

        if (string.Equals(itemName, FurnaceItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return FurnaceItemRegistry.GetOrCreate();

        if (string.Equals(itemName, SimpleBowItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return SimpleBowItemRegistry.GetOrCreate();

        if (string.Equals(itemName, FeatherItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return FeatherItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RawChickenMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RawChickenMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedChickenMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedChickenMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedBoarMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedBoarMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CowMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CowMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CowLeatherItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CowLeatherItemRegistry.GetOrCreate();

        if (string.Equals(itemName, CookedCowMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return CookedCowMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, ArrowItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return ArrowItemRegistry.GetOrCreate();

        if (string.Equals(itemName, RopeItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return RopeItemRegistry.GetOrCreate();

        if (string.Equals(itemName, ShieldItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return ShieldItemRegistry.GetOrCreate();

        if (string.Equals(itemName, ThickLeatherItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return ThickLeatherItemRegistry.GetOrCreate();

        if (string.Equals(itemName, SharpTuskItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return SharpTuskItemRegistry.GetOrCreate();

        if (string.Equals(itemName, BoarMeatItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return BoarMeatItemRegistry.GetOrCreate();

        if (string.Equals(itemName, BoarTrophyItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return BoarTrophyItemRegistry.GetOrCreate();

        if (string.Equals(itemName, StoneFragmentItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return StoneFragmentItemRegistry.GetOrCreate();

        if (string.Equals(itemName, ResilientMossItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return ResilientMossItemRegistry.GetOrCreate();

        if (string.Equals(itemName, EarthCoreItemRegistry.ItemName, StringComparison.OrdinalIgnoreCase))
            return EarthCoreItemRegistry.GetOrCreate();

        if (string.Equals(itemName, "Machado", StringComparison.OrdinalIgnoreCase))
            return LoadResourceItem("VendorItems/Axe");

        if (string.Equals(itemName, "Picareta", StringComparison.OrdinalIgnoreCase))
            return LoadResourceItem("VendorItems/Axepick");

        Item resolved = ResolveFromResources(itemName, prefabName);
        if (resolved != null)
            resolved.ApplyDefinition();

        return resolved;
    }

    static Item LoadResourceItem(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        Item item = prefab != null ? prefab.GetComponent<Item>() : null;
        item?.ApplyDefinition();
        return item;
    }

    static Item ResolveFromResources(string itemName, string prefabName)
    {
        GameObject[] prefabs = Resources.FindObjectsOfTypeAll<GameObject>();

        if (!string.IsNullOrWhiteSpace(prefabName))
        {
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null || prefab.name != prefabName)
                    continue;

                Item item = prefab.GetComponent<Item>();
                if (item != null)
                    return item;
            }
        }

        if (!string.IsNullOrWhiteSpace(itemName))
        {
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                    continue;

                Item item = prefab.GetComponent<Item>();
                if (item != null && string.Equals(item.itemName, itemName, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
        }

        return null;
    }
}
