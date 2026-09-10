using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public const int DefaultSlotCount = 42;
    public const float DefaultCarryWeight = 60f;
    public const float WeightWarningPercent = 0.8f;
    public const float WeightSlowPercent = 1f;
    public const float WeightRunBlockPercent = 1.2f;

    public List<InventoryItem> items = new List<InventoryItem>();
    [Min(1)] public int maxSlots = DefaultSlotCount;
    [Min(1f)] public float maxCarryWeight = DefaultCarryWeight;

    public int UsedSlots => items.Count;
    public int FreeSlots => Mathf.Max(0, maxSlots - UsedSlots);

    public int GetGoldAmount()
    {
        InventoryItem goldItem = GetItem("Gold");
        return goldItem != null ? Mathf.Max(0, goldItem.quantity) : 0;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        AddItem("Gold", amount, GoldItemRegistry.GetOrCreate());
    }

    public bool HasEnoughGold(int amount)
    {
        return GetGoldAmount() >= Mathf.Max(0, amount);
    }

    public bool TrySpendGold(int amount)
    {
        int clampedAmount = Mathf.Max(0, amount);
        if (clampedAmount == 0)
            return true;

        return RemoveItem("Gold", clampedAmount);
    }

    public bool AddItem(string itemName, int amount = 1, Item itemData = null)
    {
        return TryAddItem(itemName, amount, itemData);
    }

    public bool TryAddItem(string itemName, int amount = 1, Item itemData = null)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return false;

        InventoryItem incoming = new InventoryItem(itemName, amount, itemData);
        if (!CanFitItem(incoming))
            return false;

        int remaining = amount;
        foreach (InventoryItem stack in items)
        {
            if (remaining <= 0)
                break;

            if (stack == null || !stack.CanStackWith(incoming))
                continue;

            int room = Mathf.Max(0, stack.GetMaxStack() - stack.quantity);
            if (room <= 0)
                continue;

            int toMove = Mathf.Min(room, remaining);
            stack.quantity += toMove;
            remaining -= toMove;
            RefreshStackData(stack, itemData);
        }

        while (remaining > 0)
        {
            int stackSize = Mathf.Min(incoming.GetMaxStack(), remaining);
            InventoryItem newStack = new InventoryItem(itemName, stackSize, itemData);

            if (incoming.isBottle)
                newStack.SetBottleState(incoming.bottleIsFilled);

            items.Add(newStack);
            remaining -= stackSize;
        }

        return true;
    }

    public bool CanFitItem(InventoryItem incoming)
    {
        if (incoming == null || incoming.quantity <= 0 || string.IsNullOrWhiteSpace(incoming.itemName))
            return false;

        int remaining = incoming.quantity;
        foreach (InventoryItem stack in items)
        {
            if (remaining <= 0)
                return true;

            if (stack == null || !stack.CanStackWith(incoming))
                continue;

            remaining -= Mathf.Max(0, stack.GetMaxStack() - stack.quantity);
        }

        if (remaining <= 0)
            return true;

        int stackLimit = Mathf.Max(1, incoming.GetMaxStack());
        int neededSlots = Mathf.CeilToInt(remaining / (float)stackLimit);
        return neededSlots <= FreeSlots;
    }

    public bool RemoveItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return false;

        int available = GetItemQuantity(itemName);
        if (available < amount)
            return false;

        int remaining = amount;
        for (int i = items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventoryItem stack = items[i];
            if (stack == null || stack.itemName != itemName)
                continue;

            int toRemove = Mathf.Min(stack.quantity, remaining);
            stack.quantity -= toRemove;
            remaining -= toRemove;

            if (stack.quantity <= 0)
                items.RemoveAt(i);
        }

        return true;
    }

    public bool RemoveItem(InventoryItem item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        if (!items.Contains(item))
            return RemoveItem(item.itemName, amount);

        if (item.quantity < amount)
            return false;

        item.quantity -= amount;
        if (item.quantity <= 0)
            items.Remove(item);

        return true;
    }

    public InventoryItem GetItem(string itemName)
    {
        return items.Find(i => i != null && i.itemName == itemName);
    }

    public int GetItemQuantity(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return 0;

        int total = 0;
        foreach (InventoryItem item in items)
        {
            if (item != null && item.itemName == itemName)
                total += Mathf.Max(0, item.quantity);
        }

        return total;
    }

    public bool SetBottleState(string itemName, bool oldState, bool newState)
    {
        InventoryItem item = items.Find(i => i.itemName == itemName && i.isBottle && i.bottleIsFilled == oldState);
        if (item == null)
            return false;

        item.SetBottleState(newState);
        return true;
    }

    public bool AddInventoryItem(InventoryItem item)
    {
        if (item == null || item.quantity <= 0 || string.IsNullOrWhiteSpace(item.itemName))
            return false;

        if (!CanFitItem(item))
            return false;

        int remaining = item.quantity;
        foreach (InventoryItem stack in items)
        {
            if (remaining <= 0)
                break;

            if (stack == null || !stack.CanStackWith(item))
                continue;

            int room = Mathf.Max(0, stack.GetMaxStack() - stack.quantity);
            int toMove = Mathf.Min(room, remaining);
            stack.quantity += toMove;
            remaining -= toMove;
            RefreshStackData(stack, item.itemData);
        }

        while (remaining > 0)
        {
            InventoryItem clone = item.Clone();
            clone.quantity = Mathf.Min(item.GetMaxStack(), remaining);
            items.Add(clone);
            remaining -= clone.quantity;
        }

        return true;
    }

    public bool SplitStack(InventoryItem item)
    {
        if (item == null || item.quantity <= 1 || FreeSlots <= 0)
            return false;

        int splitAmount = Mathf.Max(1, item.quantity / 2);
        item.quantity -= splitAmount;

        InventoryItem split = item.Clone();
        split.quantity = splitAmount;
        items.Add(split);
        return true;
    }

    public bool DiscardItem(InventoryItem item, int amount)
    {
        return RemoveItem(item, amount);
    }

    public void SortByCategoryAndName()
    {
        items = items
            .Where(item => item != null && item.quantity > 0)
            .OrderBy(item => GetCategorySortOrder(item.category))
            .ThenBy(item => item.itemName)
            .ThenByDescending(item => item.quantity)
            .ToList();
    }

    public float GetTotalWeight()
    {
        float total = 0f;
        foreach (InventoryItem item in items)
        {
            if (item != null)
                total += item.GetTotalWeight();
        }

        return total;
    }

    public float GetWeightPercent()
    {
        return maxCarryWeight <= 0f ? 0f : GetTotalWeight() / maxCarryWeight;
    }

    public bool IsWeightWarning()
    {
        return GetWeightPercent() >= WeightWarningPercent;
    }

    public bool IsOverWeightLimit()
    {
        return GetWeightPercent() >= WeightSlowPercent;
    }

    public bool IsRunBlockedByWeight()
    {
        return GetWeightPercent() >= WeightRunBlockPercent;
    }

    public float GetMovementSpeedMultiplier()
    {
        float percent = GetWeightPercent();
        if (percent < WeightSlowPercent)
            return 1f;

        return Mathf.Lerp(0.72f, 0.48f, Mathf.InverseLerp(WeightSlowPercent, WeightRunBlockPercent, percent));
    }

    public List<InventoryItem> CreateSnapshot()
    {
        List<InventoryItem> snapshot = new List<InventoryItem>(items.Count);

        foreach (InventoryItem item in items)
        {
            if (item == null)
                continue;

            snapshot.Add(item.Clone());
        }

        return snapshot;
    }

    public void ClearAll()
    {
        items.Clear();
    }

    static void RefreshStackData(InventoryItem stack, Item itemData)
    {
        if (stack == null || itemData == null)
            return;

        stack.itemData = itemData;
        stack.icon = itemData.GetDisplayIcon();

        if (string.IsNullOrWhiteSpace(stack.prefabName))
            stack.prefabName = itemData.gameObject.name;
    }

    static int GetCategorySortOrder(InventoryCategory category)
    {
        switch (category)
        {
            case InventoryCategory.Weapons:
                return 0;
            case InventoryCategory.Tools:
                return 1;
            case InventoryCategory.Armor:
                return 2;
            case InventoryCategory.Food:
                return 3;
            case InventoryCategory.Consumables:
                return 4;
            case InventoryCategory.Resources:
                return 5;
            case InventoryCategory.Special:
                return 6;
            default:
                return 7;
        }
    }
}
