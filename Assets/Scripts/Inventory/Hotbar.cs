using UnityEngine;
using UnityEngine.UI;

public class Hotbar : MonoBehaviour
{
    const float RuntimeSlotSize = 68f;
    const float RuntimeSlotSpacing = 8f;

    public int desiredSlotCount = 8;
    public HotbarSlot[] slots;
    int currentIndex = 0;
    public int SelectedIndex => currentIndex;

    void Awake()
    {
        EnsureSlotCapacity();
        ResizeRuntimeHotbar();
    }

    public void AddItem(string itemName, Sprite icon, Item itemData)
    {
        EnsureSlotCapacity();

        foreach (HotbarSlot slot in slots)
        {
            if (slot != null && slot.itemData?.itemName == itemName)
                return;
        }

        foreach (HotbarSlot slot in slots)
        {
            if (slot != null && slot.IsEmpty())
            {
                slot.AddItem(itemName, icon, itemData);
                return;
            }
        }

        Debug.Log("Hotbar cheia!");
    }

    public HotbarSlot GetSelectedSlot()
    {
        EnsureSlotCapacity();
        if (slots == null || slots.Length == 0)
            return null;

        currentIndex = Mathf.Clamp(currentIndex, 0, slots.Length - 1);
        return slots[currentIndex];
    }

    public void SetSelectedIndex(int index)
    {
        EnsureSlotCapacity();
        if (index < 0 || slots == null || index >= slots.Length)
            return;

        currentIndex = index;
    }

    public void ClearAll()
    {
        EnsureSlotCapacity();
        if (slots == null)
            return;

        foreach (HotbarSlot slot in slots)
        {
            if (slot != null)
                slot.ClearSlot();
        }

        currentIndex = 0;
    }

    public void AddInventoryItem(InventoryItem inventoryItem)
    {
        TryAddInventoryItem(inventoryItem);
    }

    public bool TryAddInventoryItem(InventoryItem inventoryItem)
    {
        EnsureSlotCapacity();
        if (inventoryItem == null || inventoryItem.itemData == null || slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (slot == null || slot.IsEmpty())
                continue;

            bool sameBottleState = !inventoryItem.isBottle || slot.bottleIsFilled == inventoryItem.bottleIsFilled;
            if (slot.ItemName != inventoryItem.itemName || !sameBottleState)
                continue;

            slot.SetItem(
                inventoryItem.itemName,
                inventoryItem.GetDisplayIcon(),
                inventoryItem.itemData,
                slot.GetAmount() + inventoryItem.quantity
            );

            if (inventoryItem.isBottle)
                slot.SetBottleState(inventoryItem.bottleIsFilled);

            return true;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (slot == null || !slot.IsEmpty())
                continue;

            slot.SetItem(
                inventoryItem.itemName,
                inventoryItem.GetDisplayIcon(),
                inventoryItem.itemData,
                inventoryItem.quantity
            );

            if (inventoryItem.isBottle)
                slot.SetBottleState(inventoryItem.bottleIsFilled);

            return true;
        }

        return false;
    }

    public bool RemoveInventoryItem(InventoryItem inventoryItem, int amount = 1)
    {
        EnsureSlotCapacity();
        if (inventoryItem == null || slots == null || amount <= 0)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            HotbarSlot slot = slots[i];
            if (slot == null || slot.IsEmpty())
                continue;

            bool sameBottleState = !inventoryItem.isBottle || slot.bottleIsFilled == inventoryItem.bottleIsFilled;
            if (slot.ItemName != inventoryItem.itemName || !sameBottleState)
                continue;

            int remainingAmount = slot.GetAmount() - amount;
            if (remainingAmount > 0)
            {
                Item data = slot.GetItemData();
                slot.SetItem(slot.ItemName, data != null ? data.icon : null, data, remainingAmount);

                if (inventoryItem.isBottle)
                    slot.SetBottleState(inventoryItem.bottleIsFilled);
            }
            else
            {
                slot.ClearSlot();
            }

            return true;
        }

        return false;
    }

    void EnsureSlotCapacity()
    {
        if (desiredSlotCount <= 0)
            desiredSlotCount = 8;

        if (slots == null || slots.Length == 0)
            slots = GetComponentsInChildren<HotbarSlot>(true);

        if (slots == null || slots.Length == 0 || slots.Length >= desiredSlotCount)
        {
            ResizeRuntimeHotbar();
            return;
        }

        HotbarSlot template = slots[slots.Length - 1];
        if (template == null)
            return;

        HotbarSlot[] expandedSlots = new HotbarSlot[desiredSlotCount];
        for (int i = 0; i < slots.Length; i++)
            expandedSlots[i] = slots[i];

        Transform parent = template.transform.parent;
        for (int i = slots.Length; i < desiredSlotCount; i++)
        {
            HotbarSlot newSlot = Instantiate(template, parent);
            newSlot.name = $"HotbarSlot_{i + 1}";
            newSlot.ClearSlot();
            expandedSlots[i] = newSlot;
        }

        slots = expandedSlots;
        ResizeRuntimeHotbar();
    }

    void ResizeRuntimeHotbar()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null || slots == null || slots.Length == 0)
            return;

        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = RuntimeSlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        float spacing = RuntimeSlotSpacing;
        float width = slots.Length * RuntimeSlotSize + Mathf.Max(0, slots.Length - 1) * spacing;
        rect.sizeDelta = new Vector2(width, RuntimeSlotSize + 16f);
    }
}
