using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WildBoar : MonoBehaviour
{
    enum BoarState
    {
        Idle,
        Patrol,
        Chase,
        AttackWindup,
        Charging,
        Recover,
        Hit,
        Dead
    }

    [Header("Vida")]
    public WildBoarConfig config;
    public int maxHealth = 120;

    [Header("IA")]
    public float detectionDistance = 12f;
    public float loseTargetDistance = 22f;
    public float attackDistance = 2f;
    public float patrolSpeed = 2.1f;
    public float chaseSpeed = 5.8f;
    public float chargeSpeed = 9.5f;
    public float rotationSpeed = 9f;
    public float patrolRadius = 10f;
    public float reachDistance = 0.75f;
    public Vector2 idleTimeRange = new Vector2(1.2f, 3.2f);

    [Header("Investida")]
    public int minDamage = 24;
    public int maxDamage = 38;
    public float attackCooldown = 4f;
    public float attackWindupDuration = 0.34f;
    public float chargeDuration = 0.78f;
    public float recoveryDuration = 0.85f;
    public float knockbackForce = 7.5f;
    public float knockbackDuration = 0.26f;

    [Header("Chao")]
    public bool useNavMeshWhenAvailable = true;
    public float navMeshSampleDistance = 2.5f;
    public float groundRayHeight = 14f;
    public float maxGroundRayDistance = 38f;
    public LayerMask groundMask = ~0;

    [Header("Drops")]
    [Range(0f, 1f)] public float thickLeatherChance = 0.6f;
    public int minThickLeatherDrop = 1;
    public int maxThickLeatherDrop = 3;
    [Range(0f, 1f)] public float sharpTuskChance = 0.25f;
    [Range(0f, 1f)] public float boarMeatChance = 0.1f;
    public int minBoarMeatDrop = 1;
    public int maxBoarMeatDrop = 2;
    [Range(0f, 1f)] public float trophyChance = 0.05f;
    public float dropRadius = 0.75f;
    public Item thickLeatherItemData;
    public Item sharpTuskItemData;
    public Item boarMeatItemData;
    public Item trophyItemData;

    [Header("Visual")]
    public bool buildBodyOnStart = true;
    public bool rebuildVisualOnStart = true;
    public float deathAnimationDuration = 0.85f;
    public Material bodyMaterial;
    public Material darkFurMaterial;
    public Material snoutMaterial;
    public Material tuskMaterial;
    public Material hoofMaterial;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 1.75f, 0f);
    public float healthUiVisibleDuration = 4f;
    public float damagePopupLifetime = 0.8f;
    public float damagePopupRiseSpeed = 1.4f;

    int currentHealth;
    Vector3 homePosition;
    Vector3 patrolTarget;
    Vector3 chargeDirection;
    float stateTimer;
    float nextAttackTime;
    float hitFlashTimer;
    bool hasPatrolTarget;
    bool chargeAlreadyHit;

    BoarState state = BoarState.Idle;
    Transform player;
    PlayerMovement playerMovement;
    NavMeshAgent agent;
    WildBoarSpawnPoint spawnPoint;
    Transform visualRoot;
    Transform bodyPart;
    Transform headPart;
    Transform leftFrontLeg;
    Transform rightFrontLeg;
    Transform leftBackLeg;
    Transform rightBackLeg;
    Renderer[] renderers;
    Color[] baseRenderColors;
    MobHealthBar healthBar;

    public int CurrentHealth => currentHealth;
    public bool IsDead => state == BoarState.Dead;

    void Start()
    {
        ApplyConfig();
        currentHealth = maxHealth;
        homePosition = transform.position;
        EnsureItemData();
        EnsureMaterials();

        if (buildBodyOnStart && (rebuildVisualOnStart || transform.Find("Visual") == null))
            BuildProceduralModel();

        CacheRenderers();
        EnsureMainCollider();
        EnsureStablePhysics();
        EnsureNavMeshAgent();
        SnapToGround();
        EnsureCombatUI();
        UpdateHealthUI(false);
        LanNetworkEntity.Ensure(this);
        EnterIdle();
    }

    void ApplyConfig()
    {
        if (config == null)
            return;

        maxHealth = Mathf.Max(1, config.maxHealth);
        detectionDistance = Mathf.Max(1f, config.detectionDistance);
        loseTargetDistance = Mathf.Max(detectionDistance, config.loseTargetDistance);
        attackDistance = Mathf.Max(0.5f, config.attackDistance);
        patrolSpeed = Mathf.Max(0.1f, config.patrolSpeed);
        chaseSpeed = Mathf.Max(patrolSpeed, config.chaseSpeed);
        chargeSpeed = Mathf.Max(chaseSpeed, config.chargeSpeed);
        patrolRadius = Mathf.Max(1f, config.patrolRadius);
        minDamage = Mathf.Max(1, config.minDamage);
        maxDamage = Mathf.Max(minDamage, config.maxDamage);
        attackCooldown = Mathf.Max(0.1f, config.attackCooldown);
        knockbackForce = Mathf.Max(0f, config.knockbackForce);
        knockbackDuration = Mathf.Max(0.05f, config.knockbackDuration);
        thickLeatherChance = Mathf.Clamp01(config.thickLeatherChance);
        minThickLeatherDrop = Mathf.Max(0, config.minThickLeatherDrop);
        maxThickLeatherDrop = Mathf.Max(minThickLeatherDrop, config.maxThickLeatherDrop);
        sharpTuskChance = Mathf.Clamp01(config.sharpTuskChance);
        boarMeatChance = Mathf.Clamp01(config.boarMeatChance);
        minBoarMeatDrop = Mathf.Max(0, config.minBoarMeatDrop);
        maxBoarMeatDrop = Mathf.Max(minBoarMeatDrop, config.maxBoarMeatDrop);
        trophyChance = Mathf.Clamp01(config.trophyChance);
    }

    void Update()
    {
        if (state == BoarState.Dead)
            return;

        if (IsCombatState())
            KeepHealthUIVisibleInCombat();

        ResolvePlayer();
        UpdateState();
        AnimateVisuals();
    }

    public void SetSpawnData(WildBoarSpawnPoint owner, Vector3 spawnHome)
    {
        spawnPoint = owner;
        homePosition = spawnHome;
        patrolTarget = spawnHome;
    }

    public void ForceTotemTarget(PlayerMovement target, bool enterChaseImmediately = true)
    {
        if (target == null || state == BoarState.Dead)
            return;

        playerMovement = target;
        player = target.transform;
        detectionDistance = Mathf.Max(detectionDistance, 250f);
        loseTargetDistance = Mathf.Max(loseTargetDistance, 350f);
        patrolRadius = Mathf.Max(patrolRadius, 40f);

        if (!enterChaseImmediately)
            return;

        if (state == BoarState.AttackWindup || state == BoarState.Charging || state == BoarState.Recover || state == BoarState.Hit)
            return;

        EnterChase();
    }

    public void Hit(int damage, PlayerMovement attacker = null)
    {
        if (state == BoarState.Dead)
            return;

        ApplyDamage(damage, attacker, true, out _);
    }

    public void ApplyNetworkHit(
        int damage,
        out int leatherAmount,
        out int tuskAmount,
        out int meatAmount,
        out int trophyAmount,
        out int remainingHealth,
        out bool destroyed)
    {
        leatherAmount = 0;
        tuskAmount = 0;
        meatAmount = 0;
        trophyAmount = 0;

        if (state == BoarState.Dead)
        {
            remainingHealth = 0;
            destroyed = true;
            return;
        }

        ApplyDamage(damage, null, false, out destroyed);
        remainingHealth = Mathf.Max(0, currentHealth);

        if (!destroyed)
            return;

        RollLootAmounts(out leatherAmount, out tuskAmount, out meatAmount, out trophyAmount);
    }

    public void ApplyNetworkState(int networkHealth, bool destroyed)
    {
        if (destroyed)
        {
            StartDeath(false);
            return;
        }

        currentHealth = Mathf.Clamp(networkHealth, 0, maxHealth);
        UpdateHealthUI(healthBar != null && healthBar.IsVisible);
        if (state == BoarState.Dead)
            EnterIdle();
    }

    public void PlayLocalHitFeedback(int damage)
    {
        EnsureCombatUI();
        ShowDamagePopup(Mathf.Max(1, damage));
        ShowHealthUITemporarily();
        UpdateHealthUI(true);
        hitFlashTimer = 0.2f;
        if (player != null)
            EnterChase();
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
            new Vector3(0f, 0.92f, 0f),
            new Vector3(1.05f, 0.78f, 1.6f),
            Quaternion.identity,
            bodyMaterial).transform;

        CreatePart("FurBack", PrimitiveType.Cube, visualRoot,
            new Vector3(0f, 1.38f, -0.05f),
            new Vector3(0.32f, 0.22f, 1.35f),
            Quaternion.Euler(-5f, 0f, 0f),
            darkFurMaterial);

        headPart = CreatePart("Head", PrimitiveType.Sphere, visualRoot,
            new Vector3(0f, 0.98f, 0.92f),
            new Vector3(0.62f, 0.5f, 0.58f),
            Quaternion.identity,
            bodyMaterial).transform;

        CreatePart("Snout", PrimitiveType.Cube, visualRoot,
            new Vector3(0f, 0.86f, 1.35f),
            new Vector3(0.46f, 0.24f, 0.34f),
            Quaternion.identity,
            snoutMaterial);

        CreatePart("Nose", PrimitiveType.Sphere, visualRoot,
            new Vector3(0f, 0.9f, 1.57f),
            new Vector3(0.35f, 0.18f, 0.14f),
            Quaternion.identity,
            snoutMaterial);

        CreatePart("TuskL", PrimitiveType.Cylinder, visualRoot,
            new Vector3(-0.28f, 0.84f, 1.46f),
            new Vector3(0.055f, 0.24f, 0.055f),
            Quaternion.Euler(58f, 0f, -28f),
            tuskMaterial);

        CreatePart("TuskR", PrimitiveType.Cylinder, visualRoot,
            new Vector3(0.28f, 0.84f, 1.46f),
            new Vector3(0.055f, 0.24f, 0.055f),
            Quaternion.Euler(58f, 0f, 28f),
            tuskMaterial);

        CreatePart("EarL", PrimitiveType.Cube, visualRoot,
            new Vector3(-0.34f, 1.34f, 0.72f),
            new Vector3(0.18f, 0.28f, 0.12f),
            Quaternion.Euler(0f, 0f, 32f),
            darkFurMaterial);

        CreatePart("EarR", PrimitiveType.Cube, visualRoot,
            new Vector3(0.34f, 1.34f, 0.72f),
            new Vector3(0.18f, 0.28f, 0.12f),
            Quaternion.Euler(0f, 0f, -32f),
            darkFurMaterial);

        leftFrontLeg = CreateLeg("LegFL", -0.34f, 0.5f);
        rightFrontLeg = CreateLeg("LegFR", 0.34f, 0.5f);
        leftBackLeg = CreateLeg("LegBL", -0.34f, -0.55f);
        rightBackLeg = CreateLeg("LegBR", 0.34f, -0.55f);

        CreatePart("Tail", PrimitiveType.Cylinder, visualRoot,
            new Vector3(0f, 1.06f, -0.9f),
            new Vector3(0.055f, 0.32f, 0.055f),
            Quaternion.Euler(-54f, 0f, 0f),
            darkFurMaterial);

        CacheRenderers();
    }

    Transform CreateLeg(string name, float x, float z)
    {
        Transform leg = CreatePart(name, PrimitiveType.Cylinder, visualRoot,
            new Vector3(x, 0.42f, z),
            new Vector3(0.16f, 0.42f, 0.16f),
            Quaternion.identity,
            bodyMaterial).transform;

        CreatePart($"{name}Hoof", PrimitiveType.Cube, visualRoot,
            new Vector3(x, 0.07f, z + 0.04f),
            new Vector3(0.18f, 0.1f, 0.2f),
            Quaternion.identity,
            hoofMaterial);

        return leg;
    }

    void UpdateState()
    {
        switch (state)
        {
            case BoarState.Idle:
                stateTimer -= Time.deltaTime;
                if (CanSeePlayer())
                {
                    EnterChase();
                    return;
                }

                if (stateTimer <= 0f)
                    EnterPatrol();
                break;

            case BoarState.Patrol:
                if (CanSeePlayer())
                {
                    EnterChase();
                    return;
                }

                Patrol();
                break;

            case BoarState.Chase:
                Chase();
                break;

            case BoarState.AttackWindup:
                stateTimer -= Time.deltaTime;
                FaceDirection(chargeDirection);
                if (stateTimer <= 0f)
                    EnterCharge();
                break;

            case BoarState.Charging:
                Charge();
                break;

            case BoarState.Recover:
            case BoarState.Hit:
                stateTimer -= Time.deltaTime;
                StopAgent();
                if (stateTimer <= 0f)
                    EnterChaseOrPatrol();
                break;
        }
    }

    bool CanSeePlayer()
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= detectionDistance;
    }

    void Patrol()
    {
        if (!hasPatrolTarget)
            PickPatrolTarget();

        Vector3 toTarget = patrolTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= reachDistance)
        {
            EnterIdle();
            return;
        }

        MoveTowards(patrolTarget, patrolSpeed);
    }

    void Chase()
    {
        if (player == null)
        {
            EnterPatrol();
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > loseTargetDistance)
        {
            EnterPatrol();
            return;
        }

        if (distance <= attackDistance && Time.time >= nextAttackTime)
        {
            EnterAttackWindup();
            return;
        }

        MoveTowards(player.position, chaseSpeed);
    }

    void Charge()
    {
        stateTimer -= Time.deltaTime;
        MoveInDirection(chargeDirection, chargeSpeed);
        TryHitPlayerDuringCharge();

        if (stateTimer <= 0f)
            EnterRecover();
    }

    void TryHitPlayerDuringCharge()
    {
        if (chargeAlreadyHit || playerMovement == null)
            return;

        Vector3 toPlayer = playerMovement.transform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > Mathf.Max(attackDistance, 1.5f))
            return;

        float facing = Vector3.Dot(transform.forward, toPlayer.normalized);
        if (facing < 0.25f)
            return;

        chargeAlreadyHit = true;
        int damage = Random.Range(minDamage, maxDamage + 1);
        playerMovement.TakeDamage(damage);
        playerMovement.ApplyKnockback(chargeDirection, knockbackForce, knockbackDuration);
    }

    void EnterIdle()
    {
        state = BoarState.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        hasPatrolTarget = false;
        StopAgent();
    }

    void EnterPatrol()
    {
        state = BoarState.Patrol;
        PickPatrolTarget();
    }

    void EnterChase()
    {
        state = BoarState.Chase;
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.35f);
        ShowHealthUITemporarily();
    }

    void EnterChaseOrPatrol()
    {
        if (CanSeePlayer())
            EnterChase();
        else
            EnterPatrol();
    }

    void EnterAttackWindup()
    {
        state = BoarState.AttackWindup;
        stateTimer = attackWindupDuration;
        nextAttackTime = Time.time + attackCooldown;
        chargeAlreadyHit = false;

        Vector3 target = player != null ? player.position : transform.position + transform.forward;
        chargeDirection = target - transform.position;
        chargeDirection.y = 0f;
        if (chargeDirection.sqrMagnitude < 0.001f)
            chargeDirection = transform.forward;

        chargeDirection.Normalize();
        StopAgent();
    }

    void EnterCharge()
    {
        state = BoarState.Charging;
        stateTimer = chargeDuration;
    }

    void EnterRecover()
    {
        state = BoarState.Recover;
        stateTimer = recoveryDuration;
        StopAgent();
    }

    void EnterHit()
    {
        state = BoarState.Hit;
        stateTimer = 0.24f;
        StopAgent();
    }

    void PickPatrolTarget()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = homePosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (TryGetGroundPosition(candidate, out Vector3 grounded))
            {
                patrolTarget = grounded;
                hasPatrolTarget = true;
                return;
            }
        }

        patrolTarget = homePosition;
        hasPatrolTarget = true;
    }

    void MoveTowards(Vector3 target, float speed)
    {
        if (TryMoveAgent(target, speed))
            return;

        Vector3 direction = target - transform.position;
        direction.y = 0f;
        MoveInDirection(direction, speed);
    }

    bool TryMoveAgent(Vector3 target, float speed)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        agent.speed = speed;
        agent.SetDestination(target);

        Vector3 desired = agent.desiredVelocity;
        desired.y = 0f;
        if (desired.sqrMagnitude > 0.01f)
            FaceDirection(desired.normalized);

        return true;
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
        if (agent != null && agent.enabled)
            agent.nextPosition = transform.position;

        FaceDirection(direction);
    }

    void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    void StopAgent()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    void ApplyDamage(int damage, PlayerMovement attacker, bool dropLootOnDeath, out bool destroyed)
    {
        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        hitFlashTimer = 0.22f;
        EnsureCombatUI();
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        if (attacker != null)
        {
            playerMovement = attacker;
            player = attacker.transform;
        }

        destroyed = currentHealth <= 0;
        if (!destroyed)
        {
            EnterHit();
            return;
        }

        currentHealth = 0;
        if (dropLootOnDeath)
        {
            BestiaryService.Instance?.RecordDefeat(BestiaryDatabase.WildBoarId);
            DropLoot();
        }

        StartDeath(true);
    }

    void StartDeath(bool notifySpawnPoint)
    {
        if (state == BoarState.Dead)
            return;

        state = BoarState.Dead;
        StopAgent();
        UpdateHealthUI(false);
        if (agent != null)
            agent.enabled = false;

        if (notifySpawnPoint && spawnPoint != null)
            spawnPoint.NotifyBoarDeath(this);

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
        Quaternion endRotation = Quaternion.Euler(0f, 0f, 82f);
        Vector3 startPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        Vector3 endPosition = startPosition + Vector3.down * 0.16f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, deathAnimationDuration);

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

    void DropLoot()
    {
        RollLootAmounts(out int leatherAmount, out int tuskAmount, out int meatAmount, out int trophyAmount);

        for (int i = 0; i < leatherAmount; i++)
            CreateDrop(thickLeatherItemData, "Couro Grosso Drop", new Color(0.45f, 0.25f, 0.11f, 1f), new Vector3(0.24f, 0.12f, 0.22f));

        for (int i = 0; i < tuskAmount; i++)
            CreateDrop(sharpTuskItemData, "Presa Afiada Drop", new Color(0.9f, 0.82f, 0.62f, 1f), new Vector3(0.08f, 0.28f, 0.08f));

        for (int i = 0; i < meatAmount; i++)
            CreateDrop(boarMeatItemData, "Carne de Javali Drop", new Color(0.62f, 0.17f, 0.12f, 1f), new Vector3(0.26f, 0.16f, 0.2f));

        for (int i = 0; i < trophyAmount; i++)
            CreateDrop(trophyItemData, "Trofeu de Javali Drop", new Color(0.82f, 0.57f, 0.16f, 1f), new Vector3(0.22f, 0.3f, 0.22f));
    }

    void RollLootAmounts(out int leatherAmount, out int tuskAmount, out int meatAmount, out int trophyAmount)
    {
        leatherAmount = Random.value <= thickLeatherChance ? Random.Range(minThickLeatherDrop, maxThickLeatherDrop + 1) : 0;
        tuskAmount = Random.value <= sharpTuskChance ? 1 : 0;
        meatAmount = Random.value <= boarMeatChance ? Random.Range(minBoarMeatDrop, maxBoarMeatDrop + 1) : 0;
        trophyAmount = Random.value <= trophyChance ? 1 : 0;
    }

    void CreateDrop(Item itemData, string dropName, Color color, Vector3 scale)
    {
        if (itemData == null)
            return;

        Vector2 circle = Random.insideUnitCircle * dropRadius;
        Vector3 spawnPos = transform.position + new Vector3(circle.x, 0.45f, circle.y);

        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = dropName;
        drop.transform.position = spawnPos;
        drop.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
        drop.transform.localScale = scale;

        Renderer renderer = drop.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial($"{dropName}Material", color);

        Item item = drop.AddComponent<Item>();
        item.itemName = itemData.itemName;
        item.icon = itemData.icon;
        item.itemType = itemData.itemType;
        item.category = itemData.category;
        item.rarity = itemData.rarity;
        item.description = itemData.description;
        item.weight = itemData.weight;
        item.maxStack = itemData.maxStack;
        item.toolType = itemData.toolType;
        item.toolDamage = itemData.toolDamage;
        item.buyPrice = itemData.buyPrice;
        item.sellPrice = itemData.sellPrice;

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.mass = 0.12f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce((Vector3.up * 1.35f) + new Vector3(circle.x, 0f, circle.y), ForceMode.Impulse);

        FloatingPickup floatingPickup = drop.AddComponent<FloatingPickup>();
        floatingPickup.groundMask = groundMask;
        floatingPickup.collectRadius = 1.25f;
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

    void EnsureCombatUI()
    {
        if (healthBar != null)
            return;

        if (healthBar == null)
            healthBar = GetComponentInChildren<MobHealthBar>(true);

        if (healthBar == null)
            healthBar = gameObject.AddComponent<MobHealthBar>();

        healthBar.Configure(uiWorldOffset, healthUiVisibleDuration, damagePopupLifetime, damagePopupRiseSpeed, new Vector2(1.9f, 0.72f), 0.01f);
    }

    void UpdateHealthUI(bool visible)
    {
        EnsureCombatUI();
        healthBar.SetHealth(currentHealth, maxHealth, visible);
    }

    void ShowHealthUITemporarily()
    {
        UpdateHealthUI(true);
    }

    bool IsCombatState()
    {
        return state == BoarState.Chase ||
               state == BoarState.AttackWindup ||
               state == BoarState.Charging ||
               state == BoarState.Recover ||
               state == BoarState.Hit;
    }

    void KeepHealthUIVisibleInCombat()
    {
        UpdateHealthUI(true);
    }

    void ShowDamagePopup(int damage)
    {
        EnsureCombatUI();
        healthBar.ShowDamage(currentHealth, maxHealth, damage);
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

        if (hitCollider.GetComponentInParent<WildBoar>() != null)
            return false;

        if (hitCollider.GetComponentInParent<WildChicken>() != null)
            return false;

        if (hitCollider.GetComponentInParent<Cow>() != null)
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

        if (agent != null && agent.enabled && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    void EnsureMainCollider()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.72f, 0.05f);
        capsule.radius = 0.62f;
        capsule.height = 1.45f;
        capsule.direction = 2;
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

    void EnsureNavMeshAgent()
    {
        if (!useNavMeshWhenAvailable)
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            return;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.radius = 0.62f;
        agent.height = 1.45f;
        agent.acceleration = 20f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 0.15f;
        agent.updateRotation = false;
        agent.enabled = true;
        agent.Warp(hit.position);
    }

    void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
            return;

        player = LanMultiplayerManager.FindWorldFocusTransform();
        playerMovement = player != null ? player.GetComponent<PlayerMovement>() ?? player.GetComponentInParent<PlayerMovement>() : null;
    }

    void EnsureItemData()
    {
        thickLeatherItemData ??= ThickLeatherItemRegistry.GetOrCreate();
        sharpTuskItemData ??= SharpTuskItemRegistry.GetOrCreate();
        boarMeatItemData ??= BoarMeatItemRegistry.GetOrCreate();
        trophyItemData ??= BoarTrophyItemRegistry.GetOrCreate();
    }

    void EnsureMaterials()
    {
        bodyMaterial ??= CreateRuntimeMaterial("WildBoarBodyRuntime", new Color(0.34f, 0.19f, 0.09f, 1f));
        darkFurMaterial ??= CreateRuntimeMaterial("WildBoarFurRuntime", new Color(0.18f, 0.11f, 0.06f, 1f));
        snoutMaterial ??= CreateRuntimeMaterial("WildBoarSnoutRuntime", new Color(0.58f, 0.31f, 0.21f, 1f));
        tuskMaterial ??= CreateRuntimeMaterial("WildBoarTuskRuntime", new Color(0.92f, 0.82f, 0.58f, 1f));
        hoofMaterial ??= CreateRuntimeMaterial("WildBoarHoofRuntime", new Color(0.08f, 0.06f, 0.045f, 1f));
    }

    void AnimateVisuals()
    {
        if (visualRoot == null)
            return;

        float speedFactor = state == BoarState.Charging ? 1.25f : state == BoarState.Chase ? 0.9f : state == BoarState.Patrol ? 0.45f : 0f;
        float phase = Time.time * Mathf.Lerp(4f, 12f, Mathf.Clamp01(speedFactor));
        float bob = Mathf.Sin(phase * 0.5f) * 0.03f * speedFactor;
        visualRoot.localPosition = new Vector3(0f, bob, 0f);

        if (bodyPart != null)
            bodyPart.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 3f * speedFactor, 0f, 0f);

        if (headPart != null)
            headPart.localRotation = Quaternion.Euler((state == BoarState.AttackWindup ? 12f : 0f) + Mathf.Sin(phase + 0.7f) * 5f * speedFactor, 0f, 0f);

        AnimateLeg(leftFrontLeg, phase, speedFactor, 1f);
        AnimateLeg(rightBackLeg, phase, speedFactor, 1f);
        AnimateLeg(rightFrontLeg, phase, speedFactor, -1f);
        AnimateLeg(leftBackLeg, phase, speedFactor, -1f);
        UpdateHitFlash();
    }

    void AnimateLeg(Transform leg, float phase, float speedFactor, float side)
    {
        if (leg == null)
            return;

        leg.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 28f * speedFactor * side, 0f, 0f);
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
            Color flashColor = Color.Lerp(baseColor, new Color(1f, 0.5f, 0.38f, baseColor.a), 0.75f);
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

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        baseRenderColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            baseRenderColors[i] = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;
        }
    }

    Color GetBaseRenderColor(int index)
    {
        if (baseRenderColors == null || index < 0 || index >= baseRenderColors.Length)
            return Color.white;

        return baseRenderColors[index];
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
