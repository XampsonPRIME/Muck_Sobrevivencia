using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    float lifetime;
    float riseSpeed;
    float timer;
    TextMeshProUGUI popupText;
    RectTransform rectTransform;

    public void Initialize(float popupLifetime, float popupRiseSpeed)
    {
        lifetime = popupLifetime;
        riseSpeed = popupRiseSpeed;
        popupText = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (rectTransform != null)
            rectTransform.anchoredPosition += Vector2.up * riseSpeed * 20f * Time.deltaTime;

        if (popupText != null)
        {
            Color color = popupText.color;
            color.a = Mathf.Lerp(1f, 0f, timer / lifetime);
            popupText.color = color;
        }

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}
