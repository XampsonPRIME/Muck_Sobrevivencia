using UnityEngine;
using UnityEngine.UI;

public class MagicCooldownHUDView : MonoBehaviour
{
    public Image backgroundImage;
    public Image iconImage;
    public Image cooldownFillImage;

    void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (iconImage == null)
        {
            Transform icon = transform.Find("Icon");
            if (icon != null)
                iconImage = icon.GetComponent<Image>();
        }

        if (cooldownFillImage == null)
        {
            Transform fill = transform.Find("CooldownFill");
            if (fill != null)
                cooldownFillImage = fill.GetComponent<Image>();
        }
    }
}
