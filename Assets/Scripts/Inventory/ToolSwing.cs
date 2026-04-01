using UnityEngine;

public class ToolSwing : MonoBehaviour
{
    Vector3 startPos;
    Quaternion startRot;

    public float swingSpeed = 8f;
    public float swingAmount = 0.2f;
    public float swingRotation = 60f;

    bool isSwinging = false;
    float timer = 0f;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    public void Swing()
    {
        if (!isSwinging)
        {
            isSwinging = true;
            timer = 0f;
        }
    }

    void Update()
    {
        if (!isSwinging) return;

        timer += Time.deltaTime * swingSpeed;

        float progress = Mathf.Sin(timer);

        // movimento pra frente
        transform.localPosition = startPos + new Vector3(0, 0, -progress * swingAmount);

        // rotação
        transform.localRotation = startRot * Quaternion.Euler(-progress * swingRotation, 0, 0);

        // fim da animação
        if (timer >= Mathf.PI)
        {
            isSwinging = false;
            transform.localPosition = startPos;
            transform.localRotation = startRot;
        }
    }
}