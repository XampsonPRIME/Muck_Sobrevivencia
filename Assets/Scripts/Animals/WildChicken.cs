using System.Collections;
using UnityEngine;

public class WildChicken : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 20;

    [Header("Fuga")]
    public float fleeDistance = 8f;
    public float fleeDuration = 1.8f;
    public float moveSpeed = 1.4f;
    public float fleeSpeed = 4f;
    public float rotationSpeed = 9f;
    public float wanderRadius = 7f;
    public float reachDistance = 0.45f;
    public Vector2 idleTimeRange = new Vector2(1.1f, 2.8f);

    [Header("Chao")]
    public float groundRayHeight = 14f;
    public float maxGroundRayDistance = 38f;
    public LayerMask groundMask = ~0;

    [Header("Drop")]
    public int minFeatherDrop = 1;
    public int maxFeatherDrop = 3;
    public int minRawMeatDrop = 1;
    public int maxRawMeatDrop = 2;
    public float dropRadius = 0.55f;
    public Item featherItemData;
    public Item rawMeatItemData;

    [Header("Visual")]
    public bool buildBodyOnStart = true;
    public bool rebuildVisualOnStart = true;
    public float deathAnimationDuration = 0.65f;
    public Material bodyMaterial;
    public Material wingMaterial;
    public Material beakMaterial;
    public Material combMaterial;
    public Material legMaterial;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 1.45f, 0f);
    public float healthUiVisibleDuration = 3f;

    int currentHealth;
    Vector3 homePosition;
    Vector3 targetPosition;
    Vector3 fleeDirection;
    float idleTimer;
    float fleeTimer;
    float hitFlashTimer;
    bool hasTarget;
    bool isDead;
    bool isFleeing;

    Transform player;
    Transform visualRoot;
    Transform bodyPart;
    Transform headPart;
    Transform leftWingPart;
    Transform rightWingPart;
    Transform leftLegPart;
    Transform rightLegPart;
    Renderer[] renderers;
    Color[] baseRenderColors;
    WildChickenSpawnPoint spawnPoint;
    MobHealthBar healthBar;

    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    void Start()
    {
        currentHealth = maxHealth;
        homePosition = transform.position;
        EnsureItemData();
        EnsureMaterials();

        if (buildBodyOnStart && (rebuildVisualOnStart || transform.Find("Visual") == null))
            BuildProceduralModel();

        CacheRenderers();
        EnsureMainCollider();
        EnsureStablePhysics();
        SnapToGround();
        EnsureHealthBar();
        UpdateHealthBar(false);
        LanNetworkEntity.Ensure(this);
        PickNewTarget(true);
    }

    void Update()
    {
        if (isDead)
            return;

        ResolvePlayer();
        DetectThreats();

        if (isFleeing)
            HandleFlee();
        else
            HandleWander();

        AnimateVisuals();
    }

    public void SetSpawnData(WildChickenSpawnPoint owner, Vector3 spawnHome)
    {
        spawnPoint = owner;
        homePosition = spawnHome;
        targetPosition = spawnHome;
    }

    public void Hit(int damage, Vector3 threatPosition)
    {
        if (isDead)
            return;

        ApplyDamage(damage, threatPosition, true, out _);
    }

    public void ApplyNetworkHit(int damage, out int featherAmount, out int rawMeatAmount, out int remainingHealth, out bool destroyed)
    {
        featherAmount = 0;
        rawMeatAmount = 0;

        if (isDead)
        {
            remainingHealth = 0;
            destroyed = true;
            return;
        }

        ApplyDamage(damage, transform.position - transform.forward, false, out destroyed);
        remainingHealth = Mathf.Max(0, currentHealth);

        if (!destroyed)
            return;

        featherAmount = Random.Range(minFeatherDrop, maxFeatherDrop + 1);
        rawMeatAmount = Random.Range(minRawMeatDrop, maxRawMeatDrop + 1);
    }

    public void ApplyNetworkState(int networkHealth, bool destroyed)
    {
        if (destroyed)
        {
            StartDeath(false);
            return;
        }

        currentHealth = Mathf.Clamp(networkHealth, 0, maxHealth);
        isDead = false;
        UpdateHealthBar(healthBar != null);
    }

    public void PlayLocalHitFeedback(int damage)
    {
        hitFlashTimer = 0.18f;
        ShowHealthFeedback(Mathf.Max(1, damage));
        StartFleeFrom(transform.position - transform.forward, fleeDuration * 0.6f);
    }

    public void BuildProceduralModel()
    {
        EnsureMaterials();

        Transform oldVisual = transform.Find("Visual");
        if (oldVisual != null)
            DestroyRuntimeObject(oldVisual.gameObject);

        GameObject root = new GameObject("Visual");
        root.transform.SetParent(transform, false);
        visualRoot = root.transform;

        bodyPart = CreatePart("Body", PrimitiveType.Sphere, visualRoot,
            new Vector3(0f, 0.62f, 0f),
            new Vector3(0.8f, 0.62f, 0.95f),
            Quaternion.identity,
            bodyMaterial).transform;

        headPart = CreatePart("Head", PrimitiveType.Sphere, visualRoot,
            new Vector3(0f, 1.1f, 0.42f),
            new Vector3(0.42f, 0.38f, 0.42f),
            Quaternion.identity,
            bodyMaterial).transform;

        CreatePart("Beak", PrimitiveType.Cube, visualRoot,
            new Vector3(0f, 1.06f, 0.75f),
            new Vector3(0.16f, 0.12f, 0.34f),
            Quaternion.identity,
            beakMaterial);

        CreatePart("Comb", PrimitiveType.Cube, visualRoot,
            new Vector3(0f, 1.36f, 0.38f),
            new Vector3(0.14f, 0.24f, 0.1f),
            Quaternion.Euler(8f, 0f, 0f),
            combMaterial);

        leftWingPart = CreatePart("WingL", PrimitiveType.Cube, visualRoot,
            new Vector3(-0.42f, 0.64f, 0.02f),
            new Vector3(0.12f, 0.38f, 0.58f),
            Quaternion.Euler(0f, 0f, -18f),
            wingMaterial).transform;

        rightWingPart = CreatePart("WingR", PrimitiveType.Cube, visualRoot,
            new Vector3(0.42f, 0.64f, 0.02f),
            new Vector3(0.12f, 0.38f, 0.58f),
            Quaternion.Euler(0f, 0f, 18f),
            wingMaterial).transform;

        leftLegPart = CreatePart("LegL", PrimitiveType.Cylinder, visualRoot,
            new Vector3(-0.2f, 0.23f, 0.14f),
            new Vector3(0.055f, 0.23f, 0.055f),
            Quaternion.identity,
            legMaterial).transform;

        rightLegPart = CreatePart("LegR", PrimitiveType.Cylinder, visualRoot,
            new Vector3(0.2f, 0.23f, 0.14f),
            new Vector3(0.055f, 0.23f, 0.055f),
            Quaternion.identity,
            legMaterial).transform;

        CreatePart("FootL", PrimitiveType.Cube, visualRoot,
            new Vector3(-0.2f, 0.03f, 0.22f),
            new Vector3(0.13f, 0.04f, 0.24f),
            Quaternion.identity,
            legMaterial);

        CreatePart("FootR", PrimitiveType.Cube, visualRoot,
            new Vector3(0.2f, 0.03f, 0.22f),
            new Vector3(0.13f, 0.04f, 0.24f),
            Quaternion.identity,
            legMaterial);

        CreatePart("Tail", PrimitiveType.Cube, visualRoot,
            new Vector3(0f, 0.85f, -0.48f),
            new Vector3(0.32f, 0.34f, 0.18f),
            Quaternion.Euler(-34f, 0f, 0f),
            wingMaterial);

        CacheRenderers();
    }

    void ApplyDamage(int damage, Vector3 threatPosition, bool dropLootOnDeath, out bool destroyed)
    {
        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        hitFlashTimer = 0.22f;
        ShowHealthFeedback(finalDamage);
        StartFleeFrom(threatPosition, fleeDuration);

        destroyed = currentHealth <= 0;
        if (!destroyed)
            return;

        currentHealth = 0;
        if (dropLootOnDeath)
        {
            BestiaryService.Instance?.RecordDefeat(BestiaryDatabase.WildChickenId);
            DropLoot();
        }

        StartDeath(true);
    }

    void StartDeath(bool notifySpawnPoint)
    {
        if (isDead)
            return;

        isDead = true;
        isFleeing = false;
        hasTarget = false;
        healthBar?.Hide();

        if (notifySpawnPoint && spawnPoint != null)
            spawnPoint.NotifyChickenDeath(this);

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        Quaternion startRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        Quaternion endRotation = Quaternion.Euler(0f, 0f, 86f);
        Vector3 startPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        Vector3 endPosition = startPosition + Vector3.down * 0.12f;
        float duration = Mathf.Max(0.1f, deathAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                visualRoot.localPosition = Vector3.Lerp(startPosition, endPosition, t);
            }

            SetRenderAlpha(Mathf.Lerp(1f, 0.1f, t));
            yield return null;
        }

        Destroy(gameObject);
    }

    void DetectThreats()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= fleeDistance)
            StartFleeFrom(player.position, fleeDuration);
    }

    void HandleFlee()
    {
        fleeTimer -= Time.deltaTime;
        if (fleeTimer <= 0f)
        {
            isFleeing = false;
            PickNewTarget(false);
            return;
        }

        if (player != null && Vector3.Distance(transform.position, player.position) <= fleeDistance * 1.15f)
            fleeDirection = BuildFleeDirection(player.position);

        MoveInDirection(fleeDirection, fleeSpeed);
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

        MoveInDirection(toTarget.normalized, moveSpeed);
    }

    void MoveInDirection(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();
        Vector3 nextPos = transform.position + direction * speed * Time.deltaTime;
        if (TryGetGroundPosition(nextPos, out Vector3 groundedPos))
            nextPos = groundedPos;

        transform.position = nextPos;

        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    void PickNewTarget(bool immediate)
    {
        for (int i = 0; i < 12; i++)
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

    void StartFleeFrom(Vector3 threatPosition, float duration)
    {
        fleeDirection = BuildFleeDirection(threatPosition);
        fleeTimer = Mathf.Max(fleeTimer, duration);
        isFleeing = true;
        hasTarget = false;
        idleTimer = 0f;
    }

    Vector3 BuildFleeDirection(Vector3 threatPosition)
    {
        Vector3 away = transform.position - threatPosition;
        away.y = 0f;

        if (away.sqrMagnitude < 0.001f)
            away = -transform.forward;

        float jitter = Random.Range(-24f, 24f);
        return Quaternion.Euler(0f, jitter, 0f) * away.normalized;
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

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit))
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

    bool IsValidGroundHit(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null)
            return false;

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return false;

        if (hit.normal.y < 0.35f)
            return false;

        if (hitCollider.GetComponentInParent<WildChicken>() != null)
            return false;

        if (hitCollider.GetComponentInParent<Cow>() != null)
            return false;

        if (hitCollider.GetComponentInParent<WildBoar>() != null)
            return false;

        if (hitCollider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (hitCollider.GetComponentInParent<BossEnemy>() != null)
            return false;

        if (hitCollider.GetComponentInParent<PlayerMovement>() != null)
            return false;

        if (hitCollider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        return true;
    }

    void SnapToGround()
    {
        if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            transform.position = groundedPosition;
    }

    void DropLoot()
    {
        EnsureItemData();

        int featherAmount = Random.Range(minFeatherDrop, maxFeatherDrop + 1);
        int rawMeatAmount = Random.Range(minRawMeatDrop, maxRawMeatDrop + 1);

        for (int i = 0; i < featherAmount; i++)
            CreateDrop(featherItemData, "Pena Drop", new Color(0.92f, 0.88f, 0.72f, 1f), new Vector3(0.08f, 0.02f, 0.32f));

        for (int i = 0; i < rawMeatAmount; i++)
            CreateDrop(rawMeatItemData, "Carne Crua Drop", new Color(0.72f, 0.18f, 0.16f, 1f), new Vector3(0.22f, 0.16f, 0.18f));
    }

    void CreateDrop(Item itemData, string dropName, Color color, Vector3 scale)
    {
        Vector2 circle = Random.insideUnitCircle * Mathf.Min(dropRadius, 0.3f);
        if (itemData != null && ChickenFeatherVisualFactory.IsFeather(itemData.itemName))
            circle.x -= 0.72f;
        else if (itemData != null && RawChickenMeatVisualFactory.IsRawChickenMeat(itemData.itemName))
            circle.x += 0.72f;

        Vector3 spawnPos = transform.position + new Vector3(circle.x, 0.35f, circle.y);

        if (itemData != null && ChickenFeatherVisualFactory.IsFeather(itemData.itemName))
        {
            ChickenFeatherVisualFactory.Spawn(
                spawnPos,
                itemData,
                groundMask,
                0.65f,
                new Vector3(circle.x, 0f, circle.y));
            return;
        }

        if (itemData != null && RawChickenMeatVisualFactory.IsRawChickenMeat(itemData.itemName))
        {
            RawChickenMeatVisualFactory.Spawn(
                spawnPos,
                itemData,
                groundMask,
                0.65f,
                new Vector3(circle.x, 0f, circle.y));
            return;
        }

        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = dropName;
        drop.transform.position = spawnPos;
        drop.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
        drop.transform.localScale = scale;

        Renderer renderer = drop.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial($"{dropName}Material", color);

        Item item = drop.AddComponent<Item>();
        if (itemData != null)
        {
            item.itemName = itemData.itemName;
            item.icon = itemData.icon;
            item.itemType = itemData.itemType;
            item.toolType = itemData.toolType;
            item.toolDamage = itemData.toolDamage;
            item.buyPrice = itemData.buyPrice;
            item.sellPrice = itemData.sellPrice;
        }

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.mass = 0.08f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce((Vector3.up * 1.2f) + new Vector3(circle.x, 0f, circle.y), ForceMode.Impulse);

        FloatingPickup floatingPickup = drop.AddComponent<FloatingPickup>();
        floatingPickup.groundMask = groundMask;
        floatingPickup.collectRadius = 1.2f;
    }

    void AnimateVisuals()
    {
        if (visualRoot == null)
            return;

        float speedFactor = isFleeing ? 1f : hasTarget ? 0.45f : 0f;
        float walkPhase = Time.time * Mathf.Lerp(4f, 9f, speedFactor);
        float bob = Mathf.Sin(Time.time * 3.8f) * (isFleeing ? 0.035f : 0.018f);

        visualRoot.localPosition = new Vector3(0f, bob, 0f);

        if (bodyPart != null)
            bodyPart.localRotation = Quaternion.Euler(Mathf.Sin(walkPhase) * 2f, 0f, 0f);

        if (headPart != null)
            headPart.localRotation = Quaternion.Euler(Mathf.Sin(walkPhase + 0.6f) * 7f, 0f, 0f);

        if (leftWingPart != null)
            leftWingPart.localRotation = Quaternion.Euler(0f, 0f, -18f - Mathf.Sin(walkPhase) * (isFleeing ? 24f : 7f));

        if (rightWingPart != null)
            rightWingPart.localRotation = Quaternion.Euler(0f, 0f, 18f + Mathf.Sin(walkPhase) * (isFleeing ? 24f : 7f));

        if (leftLegPart != null)
            leftLegPart.localRotation = Quaternion.Euler(Mathf.Sin(walkPhase) * 24f * speedFactor, 0f, 0f);

        if (rightLegPart != null)
            rightLegPart.localRotation = Quaternion.Euler(-Mathf.Sin(walkPhase) * 24f * speedFactor, 0f, 0f);

        UpdateHitFlash();
    }

    void UpdateHitFlash()
    {
        if (hitFlashTimer > 0f)
            hitFlashTimer -= Time.deltaTime;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.material == null)
                continue;

            Color baseColor = GetBaseRenderColor(i);
            Color flashColor = Color.Lerp(baseColor, new Color(1f, 0.62f, 0.55f, baseColor.a), 0.75f);
            renderer.material.color = hitFlashTimer > 0f ? flashColor : baseColor;
        }
    }

    void SetRenderAlpha(float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.material == null)
                continue;

            Color color = GetBaseRenderColor(i);
            color.a = Mathf.Clamp01(alpha);
            renderer.material.color = color;
        }
    }

    void EnsureMainCollider()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.65f, 0.05f);
        capsule.radius = 0.42f;
        capsule.height = 1.25f;
    }

    void EnsureStablePhysics()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void EnsureItemData()
    {
        if (featherItemData == null)
            featherItemData = FeatherItemRegistry.GetOrCreate();

        if (rawMeatItemData == null)
            rawMeatItemData = RawChickenMeatItemRegistry.GetOrCreate();
    }

    void EnsureHealthBar()
    {
        if (healthBar == null)
            healthBar = GetComponent<MobHealthBar>() ?? gameObject.AddComponent<MobHealthBar>();

        healthBar.Configure(uiWorldOffset, healthUiVisibleDuration);
    }

    void UpdateHealthBar(bool visible)
    {
        EnsureHealthBar();
        healthBar.SetHealth(currentHealth, maxHealth, visible);
    }

    void ShowHealthFeedback(int damage)
    {
        EnsureHealthBar();
        healthBar.ShowDamage(currentHealth, maxHealth, damage);
    }

    void EnsureMaterials()
    {
        if (bodyMaterial == null)
            bodyMaterial = CreateRuntimeMaterial("WildChickenBodyRuntime", new Color(0.96f, 0.9f, 0.72f, 1f));

        if (wingMaterial == null)
            wingMaterial = CreateRuntimeMaterial("WildChickenWingRuntime", new Color(0.86f, 0.76f, 0.52f, 1f));

        if (beakMaterial == null)
            beakMaterial = CreateRuntimeMaterial("WildChickenBeakRuntime", new Color(0.98f, 0.66f, 0.18f, 1f));

        if (combMaterial == null)
            combMaterial = CreateRuntimeMaterial("WildChickenCombRuntime", new Color(0.76f, 0.06f, 0.04f, 1f));

        if (legMaterial == null)
            legMaterial = CreateRuntimeMaterial("WildChickenLegRuntime", new Color(0.92f, 0.58f, 0.16f, 1f));
    }

    Material CreateRuntimeMaterial(string materialName, Color color)
    {
        return RuntimeMaterialUtility.Create(materialName, color);
    }

    GameObject CreatePart(string partName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
            DestroyRuntimeObject(partCollider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return part;
    }

    void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
            return;

        player = LanMultiplayerManager.FindWorldFocusTransform();
    }

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        baseRenderColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.sharedMaterial != null)
                baseRenderColors[i] = renderer.sharedMaterial.color;
            else
                baseRenderColors[i] = Color.white;
        }
    }

    Color GetBaseRenderColor(int index)
    {
        if (baseRenderColors == null || index < 0 || index >= baseRenderColors.Length)
            return Color.white;

        return baseRenderColors[index];
    }

    void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
