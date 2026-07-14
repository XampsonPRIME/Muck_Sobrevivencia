using UnityEngine;

public class HotbarSelectorUI : MonoBehaviour
{
    public RectTransform selector;
    public Hotbar hotbar;

    public float offsetY = 42f;

    void Update()
    {
        if (hotbar == null || selector == null)
            return;

        for (int i = 0; i < hotbar.slots.Length; i++)
        {
            if (hotbar.slots[i] != null && hotbar.slots[i].isSelected)
            {
                MoveSelector(hotbar.slots[i]);
                selector.gameObject.SetActive(true);
                return;
            }
        }

        selector.gameObject.SetActive(false);
    }

    void MoveSelector(HotbarSlot slot)
    {
        RectTransform slotRect = slot.GetComponent<RectTransform>();

        selector.position = new Vector3(
            slotRect.position.x,
            slotRect.position.y + offsetY,
            selector.position.z
        );
    }
}
