using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarSlot : MonoBehaviour
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

    public bool isConsumable;
    public float healthRestore;
    public float hungerRestore;
    public float consumeHoldTime;
    public string prefabName;
    public Vector3 handLocalPosition;
    public Vector3 handLocalEulerAngles;
    public Vector3 handLocalScale = Vector3.one;

    public string ItemName => itemName;
    public int Amount => amount;

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

    public void RemoveOne()
    {
        amount--;

        if (amount <= 0)
            ClearSlot();
        else
            UpdateUI();
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
}
