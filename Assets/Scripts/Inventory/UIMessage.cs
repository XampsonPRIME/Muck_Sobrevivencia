using UnityEngine;
using TMPro;
using System.Collections;

public class UIMessage : MonoBehaviour
{
    public TextMeshProUGUI text;
    public float duration = 1.5f;

    Coroutine currentRoutine;

    void Start()
    {
        SetAlpha(0f); // invisível no começo
    }

    public void ShowMessage(string message)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowRoutine(message));
    }

    IEnumerator ShowRoutine(string message)
    {
        text.text = message;

        SetAlpha(1f); // mostra

        yield return new WaitForSeconds(duration);

        SetAlpha(0f); // esconde
    }

    void SetAlpha(float value)
    {
        Color c = text.color;
        c.a = value;
        text.color = c;
    }
}