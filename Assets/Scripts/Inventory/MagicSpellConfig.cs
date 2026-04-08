using UnityEngine;

public class MagicSpellConfig : MonoBehaviour
{
    public string itemName = "Magia Ancestral";
    public string magicName = "Magia Ancestral";
    public Sprite icon;
    public float consumeHoldTime = 0.6f;
    public Vector3 handLocalPosition = new Vector3(0.06f, 0.02f, 0.12f);
    public Vector3 handLocalEulerAngles = new Vector3(8f, 0f, 88f);
    public Vector3 handLocalScale = new Vector3(0.65f, 0.65f, 0.65f);

    public static MagicSpellConfig FindConfig()
    {
        return Object.FindFirstObjectByType<MagicSpellConfig>();
    }
}
