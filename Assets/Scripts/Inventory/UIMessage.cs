using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class UIMessage : MonoBehaviour
{
    public GameObject messagePrefab; // prefab da mensagem
    public Transform container; // painel onde fica a lista

    public float duration = 2f;
    public float fadeSpeed = 4f;

    List<GameObject> activeMessages = new List<GameObject>();

    public void ShowMessage(string message)
    {
        GameObject obj = Instantiate(messagePrefab, container);
        activeMessages.Add(obj);

        TextMeshProUGUI text = obj.GetComponentInChildren<TextMeshProUGUI>();
        Image bg = obj.GetComponentInChildren<Image>();
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();

        text.text = message;

        StartCoroutine(AnimateMessage(obj, cg));
    }

    IEnumerator AnimateMessage(GameObject obj, CanvasGroup cg)
    {
        // fade in
        while (cg.alpha < 1)
        {
            cg.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(duration);

        // fade out
        while (cg.alpha > 0)
        {
            cg.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        activeMessages.Remove(obj);
        Destroy(obj);
    }
}