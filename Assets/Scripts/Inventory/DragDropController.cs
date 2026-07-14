using UnityEngine;
using UnityEngine.UI;

public enum DragSourceType
{
    Inventory,
    Hotbar
}

public class DragPayload
{
    public DragSourceType sourceType;
    public InventorySlotUI inventorySlot;
    public HotbarSlot hotbarSlot;
    public Item itemData;
    public int amount;
}

public class DragDropController : MonoBehaviour
{
    static DragPayload currentPayload;
    static Image dragIcon;
    static Canvas rootCanvas;

    public static DragPayload CurrentPayload => currentPayload;

    public static void BeginDrag(Sprite icon, DragPayload payload)
    {
        currentPayload = payload;
        rootCanvas = ResolveDragCanvas();

        if (rootCanvas == null || icon == null)
            return;

        GameObject iconGO = new GameObject("DragIcon");
        iconGO.transform.SetParent(rootCanvas.transform, false);

        dragIcon = iconGO.AddComponent<Image>();
        dragIcon.sprite = icon;
        dragIcon.raycastTarget = false;

        RectTransform rt = dragIcon.rectTransform;
        rt.sizeDelta = new Vector2(72f, 72f);
        dragIcon.preserveAspect = true;
        dragIcon.transform.SetAsLastSibling();
    }

    public static void UpdateDrag(Vector2 screenPosition)
    {
        if (dragIcon != null)
        {
            dragIcon.rectTransform.position = screenPosition;
        }
    }

    public static void EndDrag()
    {
        if (dragIcon != null)
        {
            Object.Destroy(dragIcon.gameObject);
            dragIcon = null;
        }

        currentPayload = null;
    }

    static Canvas ResolveDragCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas bestCanvas = null;

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
                continue;

            if (bestCanvas == null || canvas.sortingOrder > bestCanvas.sortingOrder)
                bestCanvas = canvas;
        }

        return bestCanvas != null ? bestCanvas : Object.FindFirstObjectByType<Canvas>();
    }
}
