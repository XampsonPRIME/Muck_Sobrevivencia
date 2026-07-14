using System.Collections.Generic;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    public InventoryItem head;
    public InventoryItem chest;
    public InventoryItem legs;
    public InventoryItem boots;
    public InventoryItem mainHand;
    public InventoryItem shield;
    public InventoryItem accessory;

    public bool EquipFromInventory(Inventory inventory, InventoryItem sourceItem)
    {
        if (inventory == null || sourceItem == null || !sourceItem.IsEquipment())
            return false;

        EquipmentSlotType slot = ResolveSlot(sourceItem);
        if (slot == EquipmentSlotType.None)
            return false;

        InventoryItem equippedItem = sourceItem.Clone();
        equippedItem.quantity = 1;

        InventoryItem previousItem = GetEquipped(slot);
        bool sourceFreesSlot = sourceItem.quantity <= 1;
        if (previousItem != null && !sourceFreesSlot && !inventory.CanFitItem(previousItem))
        {
            MessageSystem.Instance?.ShowMessage("Sem espaco para trocar equipamento");
            return false;
        }

        if (!inventory.RemoveItem(sourceItem, 1))
            return false;

        if (previousItem != null)
            inventory.AddInventoryItem(previousItem);

        SetEquipped(slot, equippedItem);
        MessageSystem.Instance?.ShowMessage($"{equippedItem.itemName} equipado");
        return true;
    }

    public bool UnequipToInventory(Inventory inventory, EquipmentSlotType slot)
    {
        if (inventory == null || slot == EquipmentSlotType.None)
            return false;

        InventoryItem equippedItem = GetEquipped(slot);
        if (equippedItem == null)
            return false;

        if (!inventory.AddInventoryItem(equippedItem))
        {
            MessageSystem.Instance?.ShowMessage("Inventario cheio");
            return false;
        }

        SetEquipped(slot, null);
        MessageSystem.Instance?.ShowMessage($"{equippedItem.itemName} guardado");
        return true;
    }

    public InventoryItem GetEquipped(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Head:
                return head;
            case EquipmentSlotType.Chest:
                return chest;
            case EquipmentSlotType.Legs:
                return legs;
            case EquipmentSlotType.Boots:
                return boots;
            case EquipmentSlotType.MainHand:
                return mainHand;
            case EquipmentSlotType.Shield:
                return shield;
            case EquipmentSlotType.Accessory:
                return accessory;
            default:
                return null;
        }
    }

    public void SetEquipped(EquipmentSlotType slot, InventoryItem item)
    {
        switch (slot)
        {
            case EquipmentSlotType.Head:
                head = item;
                break;
            case EquipmentSlotType.Chest:
                chest = item;
                break;
            case EquipmentSlotType.Legs:
                legs = item;
                break;
            case EquipmentSlotType.Boots:
                boots = item;
                break;
            case EquipmentSlotType.MainHand:
                mainHand = item;
                break;
            case EquipmentSlotType.Shield:
                shield = item;
                break;
            case EquipmentSlotType.Accessory:
                accessory = item;
                break;
        }
    }

    public int GetTotalDefense()
    {
        int total = 0;
        foreach (InventoryItem item in GetEquippedItems())
        {
            if (item != null)
                total += Mathf.Max(0, item.defense);
        }

        return total;
    }

    public int GetMainHandDamage()
    {
        if (mainHand == null || mainHand.itemData == null)
            return 0;

        return Mathf.Max(0, mainHand.itemData.toolDamage);
    }

    public float GetMoveSpeedBonus()
    {
        float total = 0f;
        foreach (InventoryItem item in GetEquippedItems())
        {
            if (item != null)
                total += item.moveSpeedBonus;
        }

        return total;
    }

    public float GetEquippedWeight()
    {
        float total = 0f;
        foreach (InventoryItem item in GetEquippedItems())
        {
            if (item != null)
                total += item.GetTotalWeight();
        }

        return total;
    }

    public IEnumerable<InventoryItem> GetEquippedItems()
    {
        yield return head;
        yield return chest;
        yield return legs;
        yield return boots;
        yield return mainHand;
        yield return shield;
        yield return accessory;
    }

    public List<SaveEquipmentSlotData> CaptureEquipment()
    {
        List<SaveEquipmentSlotData> data = new List<SaveEquipmentSlotData>();
        CaptureSlot(data, EquipmentSlotType.Head, head);
        CaptureSlot(data, EquipmentSlotType.Chest, chest);
        CaptureSlot(data, EquipmentSlotType.Legs, legs);
        CaptureSlot(data, EquipmentSlotType.Boots, boots);
        CaptureSlot(data, EquipmentSlotType.MainHand, mainHand);
        CaptureSlot(data, EquipmentSlotType.Shield, shield);
        CaptureSlot(data, EquipmentSlotType.Accessory, accessory);
        return data;
    }

    public void LoadEquipment(List<SaveEquipmentSlotData> savedSlots)
    {
        ClearAll();

        if (savedSlots == null)
            return;

        foreach (SaveEquipmentSlotData savedSlot in savedSlots)
        {
            if (savedSlot == null || savedSlot.slot == EquipmentSlotType.None || string.IsNullOrWhiteSpace(savedSlot.itemName))
                continue;

            Item item = InventoryItemResolver.Resolve(savedSlot.itemName, savedSlot.prefabName);
            if (item == null)
                continue;

            InventoryItem inventoryItem = new InventoryItem(savedSlot.itemName, Mathf.Max(1, savedSlot.quantity), item);
            if (savedSlot.isBottle)
                inventoryItem.SetBottleState(savedSlot.bottleIsFilled);

            SetEquipped(savedSlot.slot, inventoryItem);
        }
    }

    public void ClearAll()
    {
        head = null;
        chest = null;
        legs = null;
        boots = null;
        mainHand = null;
        shield = null;
        accessory = null;
    }

    static EquipmentSlotType ResolveSlot(InventoryItem item)
    {
        if (item == null)
            return EquipmentSlotType.None;

        if (item.equipmentSlot != EquipmentSlotType.None)
            return item.equipmentSlot;

        if (item.itemType == ItemType.Tool)
            return EquipmentSlotType.MainHand;

        return EquipmentSlotType.None;
    }

    static void CaptureSlot(List<SaveEquipmentSlotData> data, EquipmentSlotType slot, InventoryItem item)
    {
        if (data == null)
            return;

        if (item == null || string.IsNullOrWhiteSpace(item.itemName))
        {
            data.Add(new SaveEquipmentSlotData { slot = slot });
            return;
        }

        data.Add(new SaveEquipmentSlotData
        {
            slot = slot,
            itemName = item.itemName,
            prefabName = item.prefabName,
            quantity = Mathf.Max(1, item.quantity),
            isBottle = item.isBottle,
            bottleIsFilled = item.bottleIsFilled
        });
    }
}
