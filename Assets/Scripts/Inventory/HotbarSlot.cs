using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class HotbarSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    const float RuntimeSlotSize = 68f;
    const float RuntimeIconSize = 54f;
    const float RuntimeAmountSize = 30f;
    static readonly Color EmptySlotColor = new Color(0.035f, 0.032f, 0.028f, 0.72f);
    static readonly Color FilledSlotColor = new Color(0.075f, 0.06f, 0.04f, 0.86f);
    static readonly Color SelectedSlotColor = new Color(0.34f, 0.2f, 0.06f, 0.96f);
    static readonly Color NormalOutlineColor = new Color(0.18f, 0.13f, 0.08f, 0.95f);
    static readonly Color SelectedOutlineColor = new Color(1f, 0.72f, 0.22f, 1f);

    public Image icon;
    public TextMeshProUGUI amountText;
    TextMeshProUGUI keyText;

    string itemName;
    int amount;

    public Item itemData;
    public ItemType itemType;
    public ToolType toolType;
    public int toolDamage;
    public bool isConsumable;
    public float healthRestore;
    public float hungerRestore;
    public float thirstRestore;
    public float consumeHoldTime;
    public string prefabName;
    public Vector3 handLocalPosition;
    public Vector3 handLocalEulerAngles;
    public Vector3 handLocalScale = Vector3.one;
    public bool isBottle;
    public bool bottleIsFilled;
    public bool isSelected = false;

    void Awake()
    {
        ConfigureRuntimeVisuals();
    }

    void OnEnable()
    {
        ConfigureRuntimeVisuals();
    }

    void LateUpdate()
    {
        ConfigureRuntimeVisuals();
    }

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
            ApplyItemData(sourceItem);
        }

        amount++;
        UpdateUI();
    }

    public string GetItemName() => itemName;
    public string ItemName => itemName;
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
        ApplyItemData(data);

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
        thirstRestore = 0f;
        consumeHoldTime = 0f;
        prefabName = "";
        handLocalPosition = Vector3.zero;
        handLocalEulerAngles = Vector3.zero;
        handLocalScale = Vector3.one;
        isBottle = false;
        bottleIsFilled = false;
        isSelected = false;
        ConfigureRuntimeVisuals();
    }

    void UpdateUI()
    {
        ConfigureRuntimeVisuals();
        amountText.text = amount > 1 ? amount.ToString() : "";
    }

    void ApplyItemData(Item sourceItem)
    {
        if (sourceItem != null)
            sourceItem.ApplyDefinition();

        itemData = sourceItem;
        itemType = sourceItem != null ? sourceItem.itemType : ItemType.Resource;
        toolType = sourceItem != null ? sourceItem.toolType : ToolType.None;
        toolDamage = sourceItem != null ? sourceItem.toolDamage : 0;
        prefabName = sourceItem != null ? sourceItem.gameObject.name : "";

        isConsumable = false;
        healthRestore = 0f;
        hungerRestore = 0f;
        thirstRestore = 0f;
        consumeHoldTime = 0f;
        handLocalPosition = Vector3.zero;
        handLocalEulerAngles = Vector3.zero;
        handLocalScale = Vector3.one;
        isBottle = false;
        bottleIsFilled = false;

        ConsumableItem consumable = sourceItem != null ? sourceItem.GetComponent<ConsumableItem>() : null;
        if (consumable == null)
        {
            RefreshIcon();
            return;
        }

        isConsumable = true;
        healthRestore = consumable.healthRestore;
        hungerRestore = consumable.hungerRestore;
        thirstRestore = consumable.thirstRestore;
        consumeHoldTime = consumable.consumeHoldTime;
        handLocalPosition = consumable.handLocalPosition;
        handLocalEulerAngles = consumable.handLocalEulerAngles;
        handLocalScale = consumable.handLocalScale;

        BottleItem bottle = sourceItem.GetComponent<BottleItem>();
        if (bottle != null)
        {
            isBottle = true;
            bottleIsFilled = false;
            thirstRestore = 0f;
        }

        RefreshIcon();
    }

    public void SetBottleState(bool isFilled)
    {
        if (!isBottle || itemData == null)
            return;

        bottleIsFilled = isFilled;

        BottleItem bottle = itemData.GetComponent<BottleItem>();
        ConsumableItem consumable = itemData.GetComponent<ConsumableItem>();
        if (bottle == null)
            return;

        thirstRestore = isFilled ? bottle.filledThirstRestore : 0f;
        consumeHoldTime = isFilled ? bottle.filledConsumeHoldTime : (consumable != null ? consumable.consumeHoldTime : consumeHoldTime);

        if (icon != null)
            RefreshIcon();
    }

    void RefreshIcon()
    {
        if (icon == null)
            return;

        Sprite sprite = itemData != null ? itemData.GetDisplayIcon() : null;

        if (isBottle && itemData.icon != null)
        {
            BottleItem bottle = itemData.GetComponent<BottleItem>();
            if (bottle != null)
                sprite = bottle.GetIcon(bottleIsFilled);
        }

        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
        ConfigureRuntimeVisuals();
    }

    void ConfigureRuntimeVisuals()
    {
        RectTransform slotRect = GetComponent<RectTransform>();
        if (slotRect != null)
            slotRect.sizeDelta = new Vector2(RuntimeSlotSize, RuntimeSlotSize);

        Image background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = isSelected ? SelectedSlotColor : (IsEmpty() ? EmptySlotColor : FilledSlotColor);
        background.raycastTarget = true;

        Outline outline = GetComponent<Outline>();
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();
        outline.effectColor = isSelected ? SelectedOutlineColor : NormalOutlineColor;
        outline.effectDistance = isSelected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);

        if (icon != null)
        {
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(RuntimeIconSize, RuntimeIconSize);
            }

            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        if (amountText != null)
        {
            RectTransform amountRect = amountText.GetComponent<RectTransform>();
            if (amountRect != null)
            {
                amountRect.anchorMin = new Vector2(1f, 0f);
                amountRect.anchorMax = new Vector2(1f, 0f);
                amountRect.pivot = new Vector2(1f, 0f);
                amountRect.anchoredPosition = new Vector2(-5f, 4f);
                amountRect.sizeDelta = new Vector2(RuntimeAmountSize, RuntimeAmountSize);
            }

            amountText.fontSize = 22f;
            amountText.fontStyle = FontStyles.Bold;
            amountText.alignment = TextAlignmentOptions.BottomRight;
            amountText.raycastTarget = false;
        }

        EnsureKeyLabel();
    }

    void EnsureKeyLabel()
    {
        if (keyText == null)
        {
            Transform existing = transform.Find("KeyLabel");
            if (existing != null)
                keyText = existing.GetComponent<TextMeshProUGUI>();

            if (keyText == null)
            {
                GameObject keyObject = new GameObject("KeyLabel", typeof(RectTransform));
                keyObject.transform.SetParent(transform, false);
                keyText = keyObject.AddComponent<TextMeshProUGUI>();
            }
        }

        if (keyText == null)
            return;

        RectTransform keyRect = keyText.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(0f, 1f);
        keyRect.pivot = new Vector2(0f, 1f);
        keyRect.anchoredPosition = new Vector2(5f, -4f);
        keyRect.sizeDelta = new Vector2(28f, 22f);

        keyText.text = (transform.GetSiblingIndex() + 1).ToString();
        keyText.fontSize = 15f;
        keyText.fontStyle = FontStyles.Bold;
        keyText.alignment = TextAlignmentOptions.TopLeft;
        keyText.color = isSelected ? new Color(1f, 0.92f, 0.58f, 1f) : new Color(0.72f, 0.66f, 0.56f, 0.92f);
        keyText.raycastTarget = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty() || itemData == null) return;

        DragDropController.BeginDrag(
            itemData.GetDisplayIcon(),
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

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
        if (inventory == null) return;

        string nameToReturn = itemData != null ? itemData.itemName : itemName;
        if (inventory.AddItem(nameToReturn, 1, itemData))
            RemoveOne();
        else
            MessageSystem.Instance?.ShowMessage("Inventario cheio");
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

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
        if (inventory == null) return;

        if (IsEmpty())
        {
            if (!inventory.RemoveItem(invItem.itemName, invAmount))
                return;

            SetItem(invItem.itemName, invItem.icon, invItem, invAmount);

            InventoryUI ui = SceneObjectCache.Find<InventoryUI>(true);
            if (ui != null) ui.Refresh();
            return;
        }

        Item hotbarItem = itemData;
        int hotbarAmount = amount;
        string hotbarName = itemName;

        SetItem(invItem.itemName, invItem.icon, invItem, invAmount);

        InventoryItem invCurrent = inventorySlot.CurrentItem;
        invCurrent.CopyFrom(new InventoryItem(hotbarName, hotbarAmount, hotbarItem));

        inventorySlot.Setup(invCurrent);
    }
}
