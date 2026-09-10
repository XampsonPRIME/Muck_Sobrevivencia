using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public Image icon;
    public TextMeshProUGUI amountText;

    InventoryItem currentItem;

    float lastClickTime;
   
    public string itemName;
    public Item itemData;

    public bool IsEmpty()
    {
        return itemData == null;
    }

    public InventoryItem CurrentItem => currentItem;

    public void Setup(InventoryItem item)
    {
        currentItem = item;

        Sprite displayIcon = item != null ? item.GetDisplayIcon() : null;

        if (icon != null && displayIcon != null)
        {
            icon.sprite = displayIcon;
            icon.enabled = true;
        }
        else if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (amountText != null)
            amountText.text = item.quantity > 1 ? item.quantity.ToString() : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            OnDoubleClick();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem == null || currentItem.itemData == null) return;

        DragDropController.BeginDrag(
            currentItem.itemData.icon,
            new DragPayload
            {
                sourceType = DragSourceType.Inventory,
                inventorySlot = this,
                itemData = currentItem.itemData,
                amount = currentItem.quantity
            }
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragDropController.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragDropController.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        DragPayload payload = DragDropController.CurrentPayload;
        if (payload == null) return;

        if (payload.sourceType == DragSourceType.Inventory)
        {
            if (payload.inventorySlot == this) return;

            SwapWithInventorySlot(payload.inventorySlot);
            return;
        }

        if (payload.sourceType == DragSourceType.Hotbar)
        {
            SwapWithHotbarSlot(payload.hotbarSlot);
        }
    }

    void OnDoubleClick()
    {
        if (currentItem == null || currentItem.itemData == null)
            return;

        Hotbar hotbar = FindFirstObjectByType<Hotbar>();
        if (hotbar == null)
            return;

        hotbar.AddItem(
            currentItem.itemName,
            currentItem.GetDisplayIcon(),
            currentItem.itemData
        );
    }

    void SwapWithInventorySlot(InventorySlotUI other)
    {
        if (other == null || other.currentItem == null || currentItem == null) return;

        InventoryItem a = currentItem;
        InventoryItem b = other.currentItem;

        string tmpName = a.itemName;
        int tmpQty = a.quantity;
        Item tmpData = a.itemData;
        ItemType tmpItemType = a.itemType;
        ToolType tmpToolType = a.toolType;

        a.itemName = b.itemName;
        a.quantity = b.quantity;
        a.itemData = b.itemData;
        a.itemType = b.itemType;
        a.toolType = b.toolType;

        b.itemName = tmpName;
        b.quantity = tmpQty;
        b.itemData = tmpData;
        b.itemType = tmpItemType;
        b.toolType = tmpToolType;

        Setup(a);
        other.Setup(b);
    }

    void SwapWithHotbarSlot(HotbarSlot hotbarSlot)
    {
        if (hotbarSlot == null || currentItem == null) return;

        Item hotbarItem = hotbarSlot.GetItemData();
        int hotbarAmount = hotbarSlot.GetAmount();

        if (hotbarItem == null || hotbarAmount <= 0) return;

        string invName = currentItem.itemName;
        int invAmount = currentItem.quantity;
        Item invItem = currentItem.itemData;

        currentItem.itemName = hotbarItem.itemName;
        currentItem.quantity = hotbarAmount;
        currentItem.itemData = hotbarItem;
        currentItem.itemType = hotbarItem.itemType;
        currentItem.toolType = hotbarItem.toolType;

        hotbarSlot.SetItem(invName, invItem != null ? invItem.icon : null, invItem, invAmount);

        Setup(currentItem);
    }
}
