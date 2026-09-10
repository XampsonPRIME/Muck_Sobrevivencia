using UnityEngine;
using TMPro;
using System.Collections;

public class MessageItem : MonoBehaviour
{
    public TextMeshProUGUI text;
    public CanvasGroup canvasGroup;

    public float duration = 2f;
    public float fadeSpeed = 4f;
    public bool responsiveNotification;

    public void Setup(string message)
    {
        text.text = message;
        StartCoroutine(Show());
    }

    IEnumerator Show()
    {
        // fade in
        while (canvasGroup.alpha < 1)
        {
            canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(duration);

        // fade out
        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed;
            yield return null;
        }

        Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (!responsiveNotification) return;
        var rect = transform as RectTransform;
        var parent = transform.parent as RectTransform;
        if (rect == null || parent == null || text == null) return;
        float scale = GetComponentInParent<Canvas>().scaleFactor;
        Rect safe = Screen.safeArea;
        float width = Mathf.Min(1000f, safe.width / scale - 48f);
        width = Mathf.Max(120f, width);
        float height = text.GetPreferredValues(text.text, width - 48f, Mathf.Infinity).y + 32f;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2((safe.center.x - Screen.width * 0.5f) / scale,
            -(Screen.height - safe.yMax) / scale - (GameState.IsPowerSelectionOpen ? 160f : 32f));
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(24f, 16f);
        text.rectTransform.offsetMax = new Vector2(-24f, -16f);
    }
}
