using UnityEngine;
using TMPro;
using System.Collections;

public class MessageItem : MonoBehaviour
{
    public TextMeshProUGUI text;
    public CanvasGroup canvasGroup;

    public float duration = 2f;
    public float fadeSpeed = 4f;

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
            canvasGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(duration);

        // fade out
        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        Destroy(gameObject);
    }
}