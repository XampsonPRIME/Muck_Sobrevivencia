using UnityEngine;

public class MagicSpellPickup : MonoBehaviour
{
    public float bobHeight = 0.18f;
    public float bobSpeed = 2.2f;
    public float rotationSpeed = 55f;

    Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
        EnsureVisuals();
        EnsureCollider();
    }

    void Update()
    {
        transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    public void Collect(Inventory inventory, Hotbar hotbar)
    {
        Item magicItem = MagicSpellItemRegistry.GetOrCreate();
        if (magicItem == null)
            return;

        inventory?.AddItem(magicItem.itemName, 1, magicItem);
        hotbar?.AddItem(magicItem.itemName, magicItem.icon, magicItem);
        MessageSystem.Instance?.ShowMessage("Recebeu a Magia Ancestral");
        Destroy(gameObject);
    }

    void EnsureCollider()
    {
        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.8f;
            sphere.center = new Vector3(0f, 0.55f, 0f);
        }
    }

    void EnsureVisuals()
    {
        if (transform.childCount > 0)
            return;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
            Object.Destroy(visualCollider);

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.28f, 0.8f, 1f, 1f);
            renderer.sharedMaterial = material;
        }
    }
}
