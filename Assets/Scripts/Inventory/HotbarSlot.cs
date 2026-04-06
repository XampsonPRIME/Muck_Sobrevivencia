using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class HotbarSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public Image icon;
    public TextMeshProUGUI amountText;

    string itemName;
    int amount;

    public Item itemData;
    public ItemType itemType;
    public ToolType toolType;
    public int toolDamage;
    public bool isSelected = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ReturnOneToInventory();
        }
    }

    public bool IsEmpty()
    {
        return string.IsNullOrEmpty(itemName);
    }

    public bool CanStack(string name)
    {
        return itemName == name;
    }

    public void AddItem(string name, Sprite sprite, Item sourceItem = null)
    {
        if (IsEmpty())
        {
            itemName = name;
            icon.sprite = sprite;
            icon.enabled = true;

            itemData = sourceItem;
            itemType = sourceItem != null ? sourceItem.itemType : ItemType.Resource;
            toolType = sourceItem != null ? sourceItem.toolType : ToolType.None;
            toolDamage = sourceItem != null ? sourceItem.toolDamage : 0;
            prefabName = sourceItem != null ? sourceItem.gameObject.name : "";
            handLocalScale = Vector3.one;

            ConsumableItem consumable = sourceItem != null ? sourceItem.GetComponent<ConsumableItem>() : null;
            if (consumable != null)
            {
                isConsumable = true;
                healthRestore = consumable.healthRestore;
                hungerRestore = consumable.hungerRestore;
                consumeHoldTime = consumable.consumeHoldTime;
                handLocalPosition = consumable.handLocalPosition;
                handLocalEulerAngles = consumable.handLocalEulerAngles;
                handLocalScale = consumable.handLocalScale;
            }
            else
            {
                isConsumable = false;
                healthRestore = 0f;
                hungerRestore = 0f;
                consumeHoldTime = 0f;
                handLocalPosition = Vector3.zero;
                handLocalEulerAngles = Vector3.zero;
            }
        }

        amount++;
        UpdateUI();
    }

    public string GetItemName() => itemName;
    public int GetAmount() => amount;
    public Item GetItemData() => itemData;

    public void SetItem(string name, Sprite sprite, Item data, int newAmount)
    {
        if (data == null || newAmount <= 0)
        {
            ClearSlot();
            return;
        }

        itemName = name;
        itemData = data;
        amount = newAmount;

        icon.sprite = sprite;
        icon.enabled = true;

        itemType = data.itemType;
        toolType = data.toolType;
        toolDamage = data.toolDamage;

        UpdateUI();
    }

    // 🔥 REMOVER ITEM (ex: comer cogumelo)
    public void RemoveOne()
    {
        amount--;

        if (amount <= 0)
            ClearSlot();
        else
            UpdateUI();
    }

    public void Clear()
    {
        itemName = "";
        itemData = null;

        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }
    }

    public void ClearSlot()
    {
        itemName = "";
        amount = 0;

        icon.sprite = null;
        icon.enabled = false;
        amountText.text = "";

        itemData = null;
        itemType = ItemType.Resource;
        toolType = ToolType.None;
        toolDamage = 0;
        isConsumable = false;
        healthRestore = 0f;
        hungerRestore = 0f;
        consumeHoldTime = 0f;
        prefabName = "";
        handLocalPosition = Vector3.zero;
        handLocalEulerAngles = Vector3.zero;
        handLocalScale = Vector3.one;
        isSelected = false;
    }

    void UpdateUI()
    {
        amountText.text = amount > 1 ? amount.ToString() : "";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty() || itemData == null) return;

        DragDropController.BeginDrag(
            itemData.icon,
            new DragPayload
            {
                sourceType = DragSourceType.Hotbar,
                hotbarSlot = this,
                itemData = itemData,
                amount = amount
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

        if (payload.sourceType == DragSourceType.Hotbar)
        {
            if (payload.hotbarSlot == this) return;
            SwapWithHotbarSlot(payload.hotbarSlot);
            return;
        }

        if (payload.sourceType == DragSourceType.Inventory)
        {
            MoveOrSwapFromInventory(payload.inventorySlot, payload.itemData, payload.amount);
        }
    }

    void ReturnOneToInventory()
    {
        if (IsEmpty()) return;

        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        string nameToReturn = itemData != null ? itemData.itemName : itemName;
        inventory.AddItem(nameToReturn, 1, itemData);
        RemoveOne();
    }

    void SwapWithHotbarSlot(HotbarSlot other)
    {
        if (other == null) return;

        Item otherItem = other.GetItemData();
        int otherAmount = other.GetAmount();
        string otherName = other.GetItemName();

        Item thisItem = itemData;
        int thisAmount = amount;
        string thisName = itemName;

        SetItem(otherName, otherItem != null ? otherItem.icon : null, otherItem, otherAmount);
        other.SetItem(thisName, thisItem != null ? thisItem.icon : null, thisItem, thisAmount);
    }

    void MoveOrSwapFromInventory(InventorySlotUI inventorySlot, Item invItem, int invAmount)
    {
        if (inventorySlot == null || invItem == null || invAmount <= 0) return;

        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        if (IsEmpty())
        {
            SetItem(invItem.itemName, invItem.icon, invItem, invAmount);
            inventory.RemoveItem(invItem.itemName, invAmount);

            InventoryUI ui = FindFirstObjectByType<InventoryUI>();
            if (ui != null) ui.Refresh();
            return;
        }

        Item hotbarItem = itemData;
        int hotbarAmount = amount;
        string hotbarName = itemName;

        SetItem(invItem.itemName, invItem.icon, invItem, invAmount);

        InventoryItem invCurrent = inventorySlot.CurrentItem;
        invCurrent.itemName = hotbarName;
        invCurrent.quantity = hotbarAmount;
        invCurrent.itemData = hotbarItem;
        invCurrent.itemType = hotbarItem.itemType;
        invCurrent.toolType = hotbarItem.toolType;

        inventorySlot.Setup(invCurrent);
    }
}
