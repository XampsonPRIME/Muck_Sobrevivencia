using UnityEngine;

public class ConsumableItem : MonoBehaviour
{
    [Header("Recuperacao")]
    public float healthRestore = 5f;
    public float hungerRestore = 10f;
    public float thirstRestore = 12f;
    public Item itemAfterConsume;

    [Header("Consumo")]
    public float consumeHoldTime = 1f;

    [Header("Visual na Mao")]
    public Vector3 handLocalPosition = new Vector3(0.08f, 0.02f, 0.12f);
    public Vector3 handLocalEulerAngles = new Vector3(15f, 0f, 90f);
    public Vector3 handLocalScale = Vector3.one;
}
