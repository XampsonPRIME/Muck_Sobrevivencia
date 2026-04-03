using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image icon;
    public TextMeshProUGUI amountText;

    InventoryItem currentItem;

    float lastClickTime;
    float doubleClickDelay = 0.3f;
    

    public void Setup(InventoryItem item)
    {
        currentItem = item;

        if (item.itemData.icon != null)
        {
            icon.sprite = item.itemData.icon;
            icon.enabled = true;
        }
        else
        {
            icon.enabled = false;
        }

        amountText.text = item.quantity > 1 ? item.quantity.ToString() : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Time.time - lastClickTime < doubleClickDelay)
        {
            OnDoubleClick();
        }

        lastClickTime = Time.time;
    }

    void OnDoubleClick()
    {
        Debug.Log("🔥 Double Click: " + currentItem.itemName);

        Hotbar hotbar = FindFirstObjectByType<Hotbar>();

        if (hotbar == null) return;

        // 🔥 manda pra hotbar
        hotbar.AddItem(
            currentItem.itemName,
            currentItem.itemData.icon,
            currentItem.itemData
        );
    }
}