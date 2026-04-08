using UnityEngine;

public class BottleItem : MonoBehaviour
{
    public Sprite emptyIcon;
    public Sprite filledIcon;
    public float filledThirstRestore = 45f;
    public float filledConsumeHoldTime = 0.8f;

    public bool CanDrink(bool isFilled)
    {
        return isFilled;
    }

    public Sprite GetIcon(bool isFilled)
    {
        if (!isFilled)
            return emptyIcon;

        return filledIcon != null ? filledIcon : emptyIcon;
    }
}
