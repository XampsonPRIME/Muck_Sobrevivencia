using UnityEngine;

public class Cow : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 4;

    [Header("Drop")]
    public int minMeatDrop = 1;
    public int maxMeatDrop = 3;
    public Item meatItemData;
    public GameObject meatDropPrefab;
    public float dropRadius = 0.6f;

    [Header("Visual")]
    public bool buildBodyOnStart = true;
    public bool rebuildVisualOnStart = true;
    public Material bodyMaterial;
    public Material spotMaterial;
    public Material hoofMaterial;

    [Header("Movimento")]
    public float moveSpeed = 1.8f;
    public float rotationSpeed = 5f;
    public float wanderRadius = 6f;
    public float reachDistance = 0.6f;
    public Vector2 idleTimeRange = new Vector2(1.5f, 4f);
    public float groundRayHeight = 15f;
    public float maxGroundRayDistance = 40f;
    public LayerMask groundMask = ~0;

    int currentHealth;
    Vector3 homePosition;
    Vector3 targetPosition;
    float idleTimer;
    bool hasTarget;

    CowSpawnPoint spawnPoint;

    void Start()
    {
        currentHealth = maxHealth;
        homePosition = transform.position;

        EnsureMaterials();

        Transform visual = transform.Find("Visual");
        if (buildBodyOnStart && (rebuildVisualOnStart || visual == null))
            BuildProceduralModel();

        EnsureMainCollider();
        SnapToGround();
        PickNewTarget(true);
    }

    void Update()
    {
        HandleWander();
    }

    public void SetSpawnData(CowSpawnPoint owner, Vector3 spawnHome)
    {
        spawnPoint = owner;
        homePosition = spawnHome;
        targetPosition = spawnHome;
    }

    public void Hit(int damage)
    {
        currentHealth -= Mathf.Max(1, damage);

        if (currentHealth <= 0)
            Die();
    }

    public void BuildProceduralModel()
    {
        EnsureMaterials();

        Transform oldVisual = transform.Find("Visual");
        if (oldVisual != null)
            DestroyObject(oldVisual.gameObject);

        GameObject visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(transform, false);

        CreatePart("Body", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0f, 1f, 0f),
            new Vector3(1.6f, 1f, 0.8f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("Head", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0.95f, 1.15f, 0f),
            new Vector3(0.65f, 0.55f, 0.55f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("Snout", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(1.35f, 1.03f, 0f),
            new Vector3(0.35f, 0.28f, 0.32f),
            Quaternion.identity,
            spotMaterial);

        CreatePart("LegFL", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(0.5f, 0.45f, 0.28f),
            new Vector3(0.18f, 0.45f, 0.18f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("LegFR", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(0.5f, 0.45f, -0.28f),
            new Vector3(0.18f, 0.45f, 0.18f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("LegBL", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(-0.5f, 0.45f, 0.28f),
            new Vector3(0.18f, 0.45f, 0.18f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("LegBR", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(-0.5f, 0.45f, -0.28f),
            new Vector3(0.18f, 0.45f, 0.18f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("HoofFL", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0.5f, 0.05f, 0.28f),
            new Vector3(0.18f, 0.1f, 0.18f),
            Quaternion.identity,
            hoofMaterial);

        CreatePart("HoofFR", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0.5f, 0.05f, -0.28f),
            new Vector3(0.18f, 0.1f, 0.18f),
            Quaternion.identity,
            hoofMaterial);

        CreatePart("HoofBL", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(-0.5f, 0.05f, 0.28f),
            new Vector3(0.18f, 0.1f, 0.18f),
            Quaternion.identity,
            hoofMaterial);

        CreatePart("HoofBR", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(-0.5f, 0.05f, -0.28f),
            new Vector3(0.18f, 0.1f, 0.18f),
            Quaternion.identity,
            hoofMaterial);

        CreatePart("EarL", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(1.02f, 1.46f, 0.22f),
            new Vector3(0.18f, 0.08f, 0.12f),
            Quaternion.Euler(0f, 0f, 20f),
            bodyMaterial);

        CreatePart("EarR", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(1.02f, 1.46f, -0.22f),
            new Vector3(0.18f, 0.08f, 0.12f),
            Quaternion.Euler(0f, 0f, -20f),
            bodyMaterial);

        CreatePart("HornL", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(1.08f, 1.5f, 0.14f),
            new Vector3(0.05f, 0.1f, 0.05f),
            Quaternion.Euler(0f, 0f, 55f),
            hoofMaterial);

        CreatePart("HornR", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(1.08f, 1.5f, -0.14f),
            new Vector3(0.05f, 0.1f, 0.05f),
            Quaternion.Euler(0f, 0f, -55f),
            hoofMaterial);

        CreatePart("SpotA", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0.15f, 1.15f, 0.33f),
            new Vector3(0.45f, 0.28f, 0.08f),
            Quaternion.identity,
            spotMaterial);

        CreatePart("SpotB", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(-0.25f, 0.92f, -0.33f),
            new Vector3(0.35f, 0.22f, 0.08f),
            Quaternion.identity,
            spotMaterial);

        CreatePart("Tail", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(-0.88f, 1.1f, 0f),
            new Vector3(0.05f, 0.22f, 0.05f),
            Quaternion.Euler(0f, 0f, 30f),
            hoofMaterial);
    }

    void HandleWander()
    {
        if (idleTimer > 0f)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
                PickNewTarget(false);

            return;
        }

        if (!hasTarget)
        {
            PickNewTarget(false);
            return;
        }

        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= reachDistance)
        {
            hasTarget = false;
            idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
            return;
        }

        Vector3 direction = toTarget.normalized;
        Vector3 nextPos = transform.position + direction * moveSpeed * Time.deltaTime;

        if (TryGetGroundPosition(nextPos, out Vector3 groundedPos))
            nextPos = groundedPos;

        transform.position = nextPos;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    void PickNewTarget(bool immediate)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = homePosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (TryGetGroundPosition(candidate, out Vector3 groundedPos))
            {
                targetPosition = groundedPos;
                hasTarget = true;
                if (!immediate)
                    idleTimer = 0f;
                return;
            }
        }

        hasTarget = false;
        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
    }

    bool TryGetGroundPosition(Vector3 position, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = position + Vector3.up * groundRayHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            maxGroundRayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.MaxValue;
        bool foundGround = false;
        groundedPosition = position;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundedPosition = hit.point;
                foundGround = true;
            }
        }

        return foundGround;
    }

    void SnapToGround()
    {
        if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            transform.position = groundedPosition;
    }

    void Die()
    {
        DropMeat();

        if (spawnPoint != null)
            spawnPoint.NotifyCowDeath(this);

        Destroy(gameObject);
    }

    void DropMeat()
    {
        int amount = Random.Range(minMeatDrop, maxMeatDrop + 1);

        for (int i = 0; i < amount; i++)
        {
            Vector2 circle = Random.insideUnitCircle * dropRadius;
            Vector3 spawnPos = transform.position + new Vector3(circle.x, 0.4f, circle.y);

            GameObject drop = meatDropPrefab != null
                ? Instantiate(meatDropPrefab, spawnPos, Quaternion.identity)
                : CreateMeatDrop(spawnPos);

            Item item = drop.GetComponent<Item>();
            if (item == null)
                item = drop.AddComponent<Item>();

            if (meatItemData != null)
            {
                item.itemName = meatItemData.itemName;
                item.icon = meatItemData.icon;
                item.itemType = meatItemData.itemType;
                item.toolType = meatItemData.toolType;
                item.toolDamage = meatItemData.toolDamage;
            }
            else
            {
                item.itemName = "Carne";
                item.itemType = ItemType.Consumable;
                item.toolType = ToolType.None;
                item.toolDamage = 0;
            }

            ConsumableItem consumable = drop.GetComponent<ConsumableItem>();
            if (consumable == null)
                consumable = drop.AddComponent<ConsumableItem>();

            consumable.healthRestore = 20f;
            consumable.hungerRestore = 35f;
            consumable.consumeHoldTime = 1.2f;
            consumable.handLocalPosition = new Vector3(0.06f, 0.03f, 0.12f);
            consumable.handLocalEulerAngles = new Vector3(0f, -20f, 65f);
            consumable.handLocalScale = new Vector3(1.4f, 1.4f, 1.4f);
        }
    }

    GameObject CreateMeatDrop(Vector3 position)
    {
        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = "Carne Drop";
        drop.transform.position = position;
        drop.transform.localScale = new Vector3(0.25f, 0.18f, 0.18f);

        Renderer renderer = drop.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = CreateRuntimeMaterial("CowMeatRuntime", new Color(0.72f, 0.2f, 0.2f));
            renderer.sharedMaterial = material;
        }

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.mass = 0.2f;

        return drop;
    }

    void EnsureMainCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.95f, 0f);
            box.size = new Vector3(1.8f, 1.9f, 1f);
        }
    }

    void EnsureMaterials()
    {
        if (bodyMaterial == null)
            bodyMaterial = CreateRuntimeMaterial("CowBodyRuntime", new Color(0.94f, 0.93f, 0.88f));

        if (spotMaterial == null)
            spotMaterial = CreateRuntimeMaterial("CowSpotRuntime", new Color(0.15f, 0.12f, 0.1f));

        if (hoofMaterial == null)
            hoofMaterial = CreateRuntimeMaterial("CowHoofRuntime", new Color(0.25f, 0.18f, 0.12f));
    }

    Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            shader = Shader.Find("Diffuse");

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        return material;
    }

    GameObject CreatePart(string partName, PrimitiveType primitiveType, Transform parent,
        Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
            DestroyObject(partCollider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return part;
    }

    void DestroyObject(Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
