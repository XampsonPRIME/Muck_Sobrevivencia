using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public Image icon;
    public TextMeshProUGUI amountText;

    InventoryItem currentItem;
    GameObject tooltipObject;
    RectTransform rectTransform;
    Canvas parentCanvas;
    bool mouseInsideSlot;

    float lastClickTime;
   
    public string itemName;
    public Item itemData;

    public bool IsEmpty()
    {
        return itemData == null;
    }

    public InventoryItem CurrentItem => currentItem;

    void OnDisable()
    {
        HideTooltip();
    }

    void OnDestroy()
    {
        HideTooltip();
    }

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (!GameState.IsInventoryOpen || currentItem == null || string.IsNullOrWhiteSpace(currentItem.itemName))
        {
            if (mouseInsideSlot)
            {
                mouseInsideSlot = false;
                HideTooltip();
            }

            return;
        }

        if (Mouse.current == null || rectTransform == null)
            return;

        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();

        Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        bool containsMouse = RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            Mouse.current.position.ReadValue(),
            uiCamera);

        if (containsMouse == mouseInsideSlot)
            return;

        mouseInsideSlot = containsMouse;

        if (mouseInsideSlot)
            ShowTooltip();
        else
            HideTooltip();
    }

    public void Setup(InventoryItem item)
    {
        currentItem = item;
        itemName = item != null ? item.itemName : string.Empty;
        itemData = item != null ? item.itemData : null;

        Sprite displayIcon = item != null ? item.GetDisplayIcon() : null;

        if (icon != null && displayIcon != null)
        {
            icon.sprite = displayIcon;
            icon.enabled = true;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            ConfigureIconRect();
        }
        else if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (amountText != null)
        {
            amountText.text = item != null && item.quantity > 1 ? item.quantity.ToString() : "";
            amountText.raycastTarget = false;
            amountText.fontSize = 17f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
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
        HideTooltip();

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

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
        Hotbar hotbar = player != null ? player.GetComponent<Hotbar>() ?? FindFirstObjectByType<Hotbar>() : FindFirstObjectByType<Hotbar>();
        if (inventory == null || hotbar == null)
            return;

        InventoryItem itemToMove = currentItem.Clone();
        if (!hotbar.TryAddInventoryItem(itemToMove))
            return;

        if (!inventory.RemoveItem(itemToMove.itemName, itemToMove.quantity))
            return;

        InventoryUI ui = FindFirstObjectByType<InventoryUI>();
        if (ui != null)
            ui.Refresh();
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

    void ShowTooltip()
    {
        if (currentItem == null || string.IsNullOrWhiteSpace(currentItem.itemName))
            return;

        HideTooltip();

        Canvas canvas = parentCanvas != null ? parentCanvas : GetComponentInParent<Canvas>();
        Transform tooltipParent = canvas != null ? canvas.transform : transform;

        tooltipObject = new GameObject("ItemNameTooltip", typeof(RectTransform), typeof(Image));
        tooltipObject.transform.SetParent(tooltipParent, false);
        tooltipObject.transform.SetAsLastSibling();

        RectTransform tooltipRect = tooltipObject.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.sizeDelta = new Vector2(220f, 38f);

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
        tooltipRect.position = topCenter + Vector3.up * 12f;

        Canvas tooltipCanvas = tooltipObject.AddComponent<Canvas>();
        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingOrder = canvas != null ? canvas.sortingOrder + 50 : 20000;

        Image background = tooltipObject.GetComponent<Image>();
        background.color = new Color(0.05f, 0.045f, 0.035f, 0.96f);
        background.raycastTarget = false;

        Outline outline = tooltipObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.82f, 0.22f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(tooltipObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 3f);
        labelRect.offsetMax = new Vector2(-6f, -3f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = currentItem.itemName;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.96f, 0.84f, 1f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
    }

    void HideTooltip()
    {
        if (tooltipObject == null)
            return;

        Destroy(tooltipObject);
        tooltipObject = null;
    }

    void ConfigureIconRect()
    {
        RectTransform iconRect = icon != null ? icon.GetComponent<RectTransform>() : null;
        if (iconRect == null)
            return;

        iconRect.anchorMin = new Vector2(0.08f, 0.16f);
        iconRect.anchorMax = new Vector2(0.92f, 0.92f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.localScale = Vector3.one;
    }
}
