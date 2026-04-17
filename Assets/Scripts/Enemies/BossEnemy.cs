using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossEnemy : MonoBehaviour
{
    [Header("Progressao")]
    public string bossDisplayName = "Boss";
    public int bossLevel = 5;
    public int minimumPlayerLevel = 5;
    public int healthBonusPerLevel = 35;
    public float contactDamageBonusPerLevel = 4f;
    public float moveSpeedBonusPerLevel = 0.06f;
    public int goldBonusPerLevel = 5;
    public int xpBonusPerLevel = 35;

    [Header("Vida")]
    public int maxHealth = 150;

    [Header("Combate")]
    public float contactDamage = 28f;
    public float attackRange = 2.4f;
    public float attackCooldown = 1.3f;
    public float moveSpeed = 2.6f;
    public float returnSpeed = 3.1f;
    public float rotationSpeed = 7f;
    public float detectionRange = 18f;
    public float targetRefreshInterval = 0.35f;

    [Header("Ataque em Area")]
    public bool enableAreaAttack;
    public float areaAttackDamage = 34f;
    public float areaAttackRadius = 4.4f;
    public float areaAttackTriggerRange = 7f;
    public float areaAttackCooldown = 6f;
    public float areaAttackTelegraphDuration = 1.05f;
    public float areaAttackMinRange = 2.25f;
    public float areaAttackIndicatorYOffset = 0.04f;
    public float areaAttackIndicatorThickness = 0.08f;
    public Color areaAttackIndicatorColor = new Color(1f, 0.28f, 0.18f, 0.45f);

    [Header("Territorio")]
    public float maxChaseDistanceFromSpawn = 9f;
    public float patrolRadius = 3.5f;
    public float patrolPointReachDistance = 0.8f;
    public float patrolRefreshInterval = 2.8f;

    [Header("Recompensa")]
    public int minGoldDrop = 25;
    public int maxGoldDrop = 45;
    public int xpReward = 250;

    [Header("Chao")]
    public float groundRayHeight = 12f;
    public float maxGroundRayDistance = 30f;
    public LayerMask groundMask = ~0;
    public Vector3 colliderCenter = new Vector3(0f, 1.2f, 0f);
    public float colliderRadius = 0.85f;
    public float colliderHeight = 2.4f;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 2.4f, 0f);
    public float healthUiVisibleDuration = 3f;
    public float damagePopupLifetime = 0.9f;
    public float damagePopupRiseSpeed = 1.8f;

    int currentHealth;
    float nextAttackTime;
    float nextTargetRefreshTime;
    float nextPatrolRefreshTime;
    float nextAreaAttackTime;
    float healthUiHideTime;
    int baseMaxHealth;
    float baseContactDamage;
    float baseMoveSpeed;
    int baseMinGoldDrop;
    int baseMaxGoldDrop;
    int baseXpReward;
    bool baseStatsCached;
    public int CurrentHealth => currentHealth;
    public int BossLevel => bossLevel;
    public int MinimumPlayerLevel => Mathf.Max(1, minimumPlayerLevel <= 0 ? bossLevel : minimumPlayerLevel);
    public string DisplayName => string.IsNullOrWhiteSpace(bossDisplayName) ? gameObject.name : bossDisplayName;
    public bool IsPendingDestroy => isPendingDestroy;

    Vector3 spawnPosition;
    Vector3 patrolTarget;

    Transform targetTransform;
    string targetPlayerId;
    PlayerMovement lastAttacker;
    Canvas worldCanvas;
    Image healthFillImage;
    TextMeshProUGUI healthText;
    BossLegacyAnimationDriver animationDriver;
    Coroutine areaAttackRoutine;
    Coroutine destroyRoutine;
    bool isPerformingAreaAttack;
    bool isPendingDestroy;
    static Material bodyMaterial;
    static Material accentMaterial;

    void Awake()
    {
        CacheBaseStats();
        ApplyLevelScaling(bossLevel);
    }

    void Start()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        patrolTarget = spawnPosition;
        EnsureVisibleModel();
        EnsureMainCollider();
        EnsureStablePhysics();
        SnapToGround();
        LanNetworkEntity.Ensure(this);
        spawnPosition = transform.position;
        patrolTarget = spawnPosition;
        EnsureCombatUI();
        UpdateHealthUI(false);
        RefreshTarget();
        PickPatrolTarget(true);
        ResolveAnimationDriver();
        animationDriver?.PlayIdle();
    }

    void EnsureVisibleModel()
    {
        if (HasVisibleRenderer())
            return;

        EnsureMaterials();

        GameObject visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(transform, false);

        CreatePart("Body", PrimitiveType.Capsule, visualRoot.transform,
            new Vector3(0f, 1.35f, 0f),
            new Vector3(1.9f, 2.3f, 1.9f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("Head", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0f, 2.75f, 0.8f),
            new Vector3(1.15f, 0.95f, 1f),
            Quaternion.identity,
            bodyMaterial);

        CreatePart("HornLeft", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(-0.42f, 3.25f, 0.88f),
            new Vector3(0.16f, 0.55f, 0.16f),
            Quaternion.Euler(12f, 0f, -22f),
            accentMaterial);

        CreatePart("HornRight", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(0.42f, 3.25f, 0.88f),
            new Vector3(0.16f, 0.55f, 0.16f),
            Quaternion.Euler(12f, 0f, 22f),
            accentMaterial);

        CreatePart("ArmLeft", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(-1.1f, 1.55f, 0f),
            new Vector3(0.42f, 1.4f, 0.42f),
            Quaternion.Euler(0f, 0f, 10f),
            accentMaterial);

        CreatePart("ArmRight", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(1.1f, 1.55f, 0f),
            new Vector3(0.42f, 1.4f, 0.42f),
            Quaternion.Euler(0f, 0f, -10f),
            accentMaterial);
    }

    bool HasVisibleRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.transform != transform)
                return true;
        }

        return false;
    }

    void EnsureMaterials()
    {
        if (bodyMaterial == null)
        {
            bodyMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMaterial.color = new Color(0.34f, 0.08f, 0.08f, 1f);
        }

        if (accentMaterial == null)
        {
            accentMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            accentMaterial.color = new Color(0.8f, 0.68f, 0.2f, 1f);
        }
    }

    void CreatePart(string partName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
            Destroy(partCollider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
    }

    void Update()
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        if (isPendingDestroy)
        {
            UpdateUIFacing();
            return;
        }

        if (ShouldUseNetworkAuthority())
        {
            UpdateUIFacing();
            return;
        }

        if (Time.time >= nextTargetRefreshTime || targetTransform == null)
            RefreshTarget();

        if (isPerformingAreaAttack)
        {
            FaceTarget();
            UpdateUIFacing();
            return;
        }

        if (TryStartAreaAttack())
        {
            UpdateUIFacing();
            return;
        }

        UpdateMovement();
        TryAttackPlayer();
        UpdateUIFacing();
    }

    public void Hit(int damage, PlayerMovement attacker)
    {
        if (isPendingDestroy)
            return;

        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        lastAttacker = attacker;
        ResolveAnimationDriver();
        animationDriver?.PlayDamage();
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        if (currentHealth <= 0)
            Die();
    }

    public void ApplyNetworkHit(int damage, out int goldAmount, out int xpAmount, out bool unlockMagic, out int remainingHealth, out bool destroyed)
    {
        if (isPendingDestroy)
        {
            goldAmount = 0;
            xpAmount = 0;
            unlockMagic = false;
            remainingHealth = 0;
            destroyed = true;
            return;
        }

        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        ResolveAnimationDriver();
        animationDriver?.PlayDamage();
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        destroyed = currentHealth <= 0;
        goldAmount = destroyed ? Random.Range(minGoldDrop, maxGoldDrop + 1) : 0;
        xpAmount = destroyed ? xpReward : 0;
        unlockMagic = destroyed;
        remainingHealth = Mathf.Max(0, currentHealth);

        if (destroyed)
            BeginDeathSequence();
    }

    public void ApplyNetworkState(int networkHealth, bool destroyed)
    {
        currentHealth = Mathf.Max(0, networkHealth);
        UpdateHealthUI(!destroyed);

        if (destroyed)
            BeginDeathSequence();
    }

    public void ApplyNetworkState(Vector3 networkPosition, Quaternion networkRotation, int networkLevel, int networkHealth, bool destroyed)
    {
        ApplyLevelScaling(networkLevel);
        transform.SetPositionAndRotation(networkPosition, networkRotation);
        ApplyNetworkState(networkHealth, destroyed);
    }

    public bool CanBeChallengedBy(PlayerMovement player)
    {
        PlayerProgression progression = player != null ? player.GetComponent<PlayerProgression>() : null;
        int playerLevel = progression != null ? progression.currentLevel : 1;
        return CanBeChallengedByLevel(playerLevel);
    }

    public bool CanBeChallengedByLevel(int playerLevel)
    {
        return Mathf.Max(1, playerLevel) >= MinimumPlayerLevel;
    }

    public string BuildMinimumLevelMessage()
    {
        return $"{DisplayName} exige nivel {MinimumPlayerLevel}.";
    }

    public void PlayLocalHitFeedback(int damage)
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        ResolveAnimationDriver();
        animationDriver?.PlayDamage();
        ShowDamagePopup(Mathf.Max(1, damage));
        ShowHealthUITemporarily();
        UpdateHealthUI(true);
    }

    void UpdateMovement()
    {
        Vector3 targetPosition;
        float speed;

        if (ShouldChaseTarget(out targetPosition))
        {
            speed = moveSpeed;
        }
        else
        {
            targetPosition = GetPatrolTarget();
            speed = returnSpeed;
        }

        MoveTowards(targetPosition, speed);
    }

    bool ShouldChaseTarget(out Vector3 chaseTargetPosition)
    {
        chaseTargetPosition = transform.position;

        if (targetTransform == null)
            return false;

        Vector3 toPlayerFromSpawn = targetTransform.position - spawnPosition;
        toPlayerFromSpawn.y = 0f;

        Vector3 toPlayerFromBoss = targetTransform.position - transform.position;
        toPlayerFromBoss.y = 0f;

        if (toPlayerFromBoss.magnitude > detectionRange)
            return false;

        Vector3 clamped = Vector3.ClampMagnitude(toPlayerFromSpawn, maxChaseDistanceFromSpawn);
        chaseTargetPosition = spawnPosition + clamped;

        if (TryGetGroundPosition(chaseTargetPosition, out Vector3 groundedTarget))
            chaseTargetPosition = groundedTarget;

        return true;
    }

    Vector3 GetPatrolTarget()
    {
        Vector3 toPatrol = patrolTarget - transform.position;
        toPatrol.y = 0f;

        if (toPatrol.magnitude <= patrolPointReachDistance || Time.time >= nextPatrolRefreshTime)
            PickPatrolTarget();

        return patrolTarget;
    }

    void PickPatrolTarget(bool immediate = false)
    {
        nextPatrolRefreshTime = Time.time + patrolRefreshInterval;

        Vector2 circle = Random.insideUnitCircle * patrolRadius;
        Vector3 candidate = spawnPosition + new Vector3(circle.x, 0f, circle.y);

        if (TryGetGroundPosition(candidate, out Vector3 groundedTarget))
            patrolTarget = groundedTarget;
        else
            patrolTarget = spawnPosition;

        if (immediate && TryGetGroundPosition(transform.position, out Vector3 groundedCurrent))
            transform.position = groundedCurrent;
    }

    void MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        Vector3 direction = toTarget.normalized;
        float moveDistance = Mathf.Min(speed * Time.deltaTime, toTarget.magnitude);
        Vector3 nextPos = transform.position + direction * moveDistance;

        if (TryGetGroundPosition(nextPos, out Vector3 groundedPos))
            nextPos = groundedPos;

        transform.position = nextPos;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void FaceTarget()
    {
        if (targetTransform == null)
            return;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void TryAttackPlayer()
    {
        if (targetTransform == null || Time.time < nextAttackTime)
            return;

        Vector3 toPlayer = targetTransform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > attackRange)
            return;

        ResolveAnimationDriver();
        animationDriver?.PlayAttack();

        if (LanMultiplayerManager.Instance != null && LanMultiplayerManager.Instance.IsMultiplayerActive)
            LanMultiplayerManager.Instance.ApplyEnemyDamage(targetPlayerId, contactDamage);
        else
        {
            PlayerMovement targetPlayer = targetTransform.GetComponent<PlayerMovement>();
            targetPlayer?.TakeDamage(contactDamage);
        }

        nextAttackTime = Time.time + attackCooldown;
    }

    bool TryStartAreaAttack()
    {
        if (!enableAreaAttack || targetTransform == null || Time.time < nextAreaAttackTime)
            return false;

        Vector3 toPlayer = targetTransform.position - transform.position;
        toPlayer.y = 0f;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer > areaAttackTriggerRange || distanceToPlayer < areaAttackMinRange)
            return false;

        nextAreaAttackTime = Time.time + areaAttackCooldown;
        if (areaAttackRoutine != null)
            StopCoroutine(areaAttackRoutine);

        areaAttackRoutine = StartCoroutine(PerformAreaAttack(targetTransform.position));
        return true;
    }

    IEnumerator PerformAreaAttack(Vector3 targetPosition)
    {
        isPerformingAreaAttack = true;
        ResolveAnimationDriver();
        animationDriver?.PlayAttack();

        Vector3 attackCenter = targetPosition;
        if (TryGetGroundPosition(attackCenter, out Vector3 groundedTarget))
            attackCenter = groundedTarget;

        BossAreaAttackIndicator indicator = BossAreaAttackIndicator.Create(
            attackCenter + Vector3.up * areaAttackIndicatorYOffset,
            areaAttackRadius,
            areaAttackIndicatorThickness,
            areaAttackIndicatorColor,
            Mathf.Max(0.1f, areaAttackTelegraphDuration));

        yield return new WaitForSeconds(Mathf.Max(0.1f, areaAttackTelegraphDuration));

        if (indicator != null)
            indicator.TriggerImpact();

        ApplyAreaAttackDamage(attackCenter);
        isPerformingAreaAttack = false;
        areaAttackRoutine = null;
    }

    void ApplyAreaAttackDamage(Vector3 attackCenter)
    {
        if (LanMultiplayerManager.Instance != null && LanMultiplayerManager.Instance.IsMultiplayerActive)
        {
            LanMultiplayerManager.Instance.ApplyEnemyAreaDamage(attackCenter, areaAttackRadius, areaAttackDamage);
            return;
        }

        PlayerMovement[] players = LanMultiplayerManager.GetGameplayPlayers();
        float radiusSqr = areaAttackRadius * areaAttackRadius;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement player = players[i];
            if (player == null)
                continue;

            Vector3 toPlayer = player.transform.position - attackCenter;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude <= radiusSqr)
                player.TakeDamage(areaAttackDamage);
        }
    }

    void RefreshTarget()
    {
        nextTargetRefreshTime = Time.time + targetRefreshInterval;

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.IsMultiplayerActive &&
            LanMultiplayerManager.Instance.IsServerAuthority &&
            LanMultiplayerManager.Instance.TryFindClosestEnemyTarget(transform.position, out Transform networkTarget, out string playerId))
        {
            targetTransform = networkTarget;
            targetPlayerId = playerId;
            return;
        }

        PlayerMovement[] players = LanMultiplayerManager.GetGameplayPlayers();
        float bestDistance = float.MaxValue;
        PlayerMovement closestPlayer = null;

        foreach (PlayerMovement candidate in players)
        {
            if (candidate == null || GameState.IsPlayerDead)
                continue;

            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closestPlayer = candidate;
            }
        }

        targetTransform = closestPlayer != null ? closestPlayer.transform : null;
        targetPlayerId = null;
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

    void SnapToGround()
    {
        if (!TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            return;

        transform.position = groundedPosition;
        AlignBaseToGround(groundedPosition.y);
    }

    void AlignBaseToGround(float groundY)
    {
        bool foundBase = false;
        float lowestY = float.MaxValue;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            float candidate = col.bounds.min.y;
            if (candidate < lowestY)
            {
                lowestY = candidate;
                foundBase = true;
            }
        }

        if (!foundBase)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                float candidate = renderer.bounds.min.y;
                if (candidate < lowestY)
                {
                    lowestY = candidate;
                    foundBase = true;
                }
            }
        }

        if (!foundBase)
            return;

        transform.position += Vector3.up * (groundY - lowestY);
    }

    void Die()
    {
        if (isPendingDestroy)
            return;

        DropMagicPickup();
        AwardExperienceIfKilledByPlayer();
        DropGoldIfKilledByPlayer();
        BeginDeathSequence();
    }

    void AwardExperienceIfKilledByPlayer()
    {
        if (lastAttacker == null)
            return;

        PlayerProgression progression = lastAttacker.GetComponent<PlayerProgression>();
        if (progression != null)
            progression.AddExperience(xpReward, DisplayName);
    }

    void DropGoldIfKilledByPlayer()
    {
        if (lastAttacker == null)
            return;

        int amount = Random.Range(minGoldDrop, maxGoldDrop + 1);
        PlayerInteraction killerInteraction = lastAttacker.GetComponent<PlayerInteraction>();
        Inventory killerInventory = killerInteraction != null ? killerInteraction.inventory : null;

        if (killerInventory != null)
            killerInventory.AddItem("Gold", amount, GoldItemRegistry.GetOrCreate());

        MessageSystem.Instance?.ShowMessage($"+{amount} gold");
    }

    void DropMagicPickup()
    {
        GameObject pickupObject = new GameObject("MagicSpellPickup");
        pickupObject.transform.position = transform.position + Vector3.up * 0.2f;
        pickupObject.AddComponent<MagicSpellPickup>();
    }

    void EnsureCombatUI()
    {
        if (worldCanvas != null)
            return;

        GameObject canvasObject = new GameObject("BossCombatUI");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = uiWorldOffset;

        worldCanvas = canvasObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = Camera.main;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 30f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2.4f, 0.85f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        GameObject bgObject = CreateUiObject("HealthBg", canvasObject.transform);
        Image bgImage = bgObject.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        RectTransform bgRect = bgObject.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.12f, 0.52f);
        bgRect.anchorMax = new Vector2(0.88f, 0.82f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillObject = CreateUiObject("HealthFill", bgObject.transform);
        healthFillImage = fillObject.AddComponent<Image>();
        healthFillImage.color = new Color(0.82f, 0.18f, 0.14f, 1f);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        healthFillImage.type = Image.Type.Filled;
        healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        GameObject textObject = CreateUiObject("HealthText", canvasObject.transform);
        healthText = textObject.AddComponent<TextMeshProUGUI>();
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = 20f;
        healthText.fontStyle = FontStyles.Bold;
        healthText.color = Color.white;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.anchoredPosition = new Vector2(0f, 4f);

        canvasObject.SetActive(false);
    }

    void UpdateHealthUI(bool visible)
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            return;

        float normalizedHealth = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        healthFillImage.fillAmount = normalizedHealth;
        healthFillImage.color = Color.Lerp(new Color(0.75f, 0.08f, 0.08f), new Color(0.2f, 0.9f, 0.25f), normalizedHealth);
        healthText.text = $"{Mathf.Max(0, currentHealth)}/{maxHealth}";
        worldCanvas.gameObject.SetActive(visible);
    }

    void ShowHealthUITemporarily()
    {
        healthUiHideTime = Time.time + healthUiVisibleDuration;
        UpdateHealthUI(true);
    }

    void UpdateUIFacing()
    {
        if (worldCanvas == null)
            return;

        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            worldCanvas.worldCamera = activeCamera;
            Vector3 direction = worldCanvas.transform.position - activeCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                worldCanvas.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        if (worldCanvas.gameObject.activeSelf && Time.time > healthUiHideTime)
            worldCanvas.gameObject.SetActive(false);
    }

    void ShowDamagePopup(int damage)
    {
        if (worldCanvas == null)
            return;

        GameObject popupObject = CreateUiObject($"Damage_{damage}", worldCanvas.transform);
        popupObject.transform.localPosition = new Vector3(Random.Range(-0.16f, 0.16f), 0.28f, 0f);

        TextMeshProUGUI popupText = popupObject.AddComponent<TextMeshProUGUI>();
        popupText.text = damage.ToString();
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.fontSize = 28f;
        popupText.fontStyle = FontStyles.Bold;
        popupText.color = new Color(1f, 0.8f, 0.28f, 1f);

        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 46f);

        DamagePopup popup = popupObject.AddComponent<DamagePopup>();
        popup.Initialize(damagePopupLifetime, damagePopupRiseSpeed);
    }

    GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName);
        uiObject.transform.SetParent(parent, false);
        uiObject.AddComponent<RectTransform>();
        return uiObject;
    }

    void EnsureMainCollider()
    {
        Collider col = GetComponent<Collider>();
        CapsuleCollider capsule = col as CapsuleCollider;

        if (col != null && capsule == null)
            return;

        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.center = colliderCenter;
        capsule.radius = Mathf.Max(0.05f, colliderRadius);
        capsule.height = Mathf.Max(colliderRadius * 2f, colliderHeight);
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

    bool IsValidGroundHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null)
            return false;

        Transform hitTransform = collider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return false;

        if (hit.normal.y < 0.35f)
            return false;

        if (collider.GetComponentInParent<Cow>() != null)
            return false;

        if (collider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (collider.GetComponentInParent<BossEnemy>() != null)
            return false;

        if (collider.GetComponentInParent<PlayerMovement>() != null)
            return false;

        if (collider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        return true;
    }

    bool ShouldUseNetworkAuthority()
    {
        return LanMultiplayerManager.Instance != null &&
               LanMultiplayerManager.Instance.IsMultiplayerActive &&
               LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Client;
    }

    void CacheBaseStats()
    {
        if (baseStatsCached)
            return;

        baseMaxHealth = Mathf.Max(1, maxHealth);
        baseContactDamage = Mathf.Max(1f, contactDamage);
        baseMoveSpeed = Mathf.Max(0.1f, moveSpeed);
        baseMinGoldDrop = Mathf.Max(0, minGoldDrop);
        baseMaxGoldDrop = Mathf.Max(baseMinGoldDrop, maxGoldDrop);
        baseXpReward = Mathf.Max(1, xpReward);
        baseStatsCached = true;
    }

    public void ApplyLevelScaling(int level)
    {
        CacheBaseStats();

        bossLevel = Mathf.Max(1, level);
        if (minimumPlayerLevel <= 0)
            minimumPlayerLevel = bossLevel;

        int bonusLevels = Mathf.Max(0, bossLevel - 1);

        maxHealth = baseMaxHealth + bonusLevels * Mathf.Max(0, healthBonusPerLevel);
        contactDamage = baseContactDamage + bonusLevels * Mathf.Max(0f, contactDamageBonusPerLevel);
        moveSpeed = baseMoveSpeed + bonusLevels * Mathf.Max(0f, moveSpeedBonusPerLevel);
        minGoldDrop = baseMinGoldDrop + bonusLevels * Mathf.Max(0, goldBonusPerLevel);
        maxGoldDrop = Mathf.Max(minGoldDrop, baseMaxGoldDrop + bonusLevels * Mathf.Max(0, goldBonusPerLevel));
        xpReward = baseXpReward + bonusLevels * Mathf.Max(0, xpBonusPerLevel);
    }

    public void RefreshBaseStats()
    {
        baseStatsCached = false;
        CacheBaseStats();
        ApplyLevelScaling(bossLevel);

        if (currentHealth > 0)
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateHealthUI(worldCanvas != null && worldCanvas.gameObject.activeSelf);
    }

    void ResolveAnimationDriver()
    {
        if (animationDriver == null)
            animationDriver = GetComponent<BossLegacyAnimationDriver>() ?? GetComponentInChildren<BossLegacyAnimationDriver>(true);
    }

    void BeginDeathSequence()
    {
        if (isPendingDestroy)
            return;

        isPendingDestroy = true;
        currentHealth = 0;
        isPerformingAreaAttack = false;
        nextAttackTime = float.MaxValue;
        nextAreaAttackTime = float.MaxValue;
        targetTransform = null;
        targetPlayerId = null;

        if (areaAttackRoutine != null)
        {
            StopCoroutine(areaAttackRoutine);
            areaAttackRoutine = null;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        UpdateHealthUI(false);
        ResolveAnimationDriver();

        float destroyDelay = 0.05f;
        if (animationDriver != null)
        {
            animationDriver.PlayDeath();
            destroyDelay = Mathf.Max(destroyDelay, animationDriver.DeathDuration);
        }

        if (destroyRoutine != null)
            StopCoroutine(destroyRoutine);

        destroyRoutine = StartCoroutine(DestroyAfterDelay(destroyDelay));
    }

    IEnumerator DestroyAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Destroy(gameObject);
    }
}
