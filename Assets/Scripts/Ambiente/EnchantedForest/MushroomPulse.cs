using UnityEngine;

public class MushroomPulse : MonoBehaviour
{
    public float speed = 2f;
    public float scaleAmount = 0.05f;

    public Renderer rend;
    public float emissionStrength = 2f;
    

    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
    }

    void Update()
    {
        float pulse = Mathf.Sin(Time.time * speed);

        // escala
        float scale = 1 + pulse * scaleAmount;
        transform.localScale = initialScale * scale;

        // brilho (se tiver emission)
        if (rend != null)
        {
            float emission = Mathf.Lerp(0.5f, emissionStrength, (pulse + 1) / 2);
            rend.material.SetColor("_EmissionColor", Color.blue * emission);
        }
    }
}