using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniKrug : MonoBehaviour
{
    const string DefaultNightZombieVisualPath = "Enemies/MiniKrugZombieVisual";
    const float DungeonKeyDropChance = 0.28f;

    [Header("Progressao")]
    public int enemyLevel = 1;
    public int healthBonusPerLevel = 2;
    public float contactDamageBonusPerLevel = 1.5f;
    public float moveSpeedBonusPerLevel = 0.08f;
    public int goldBonusPerLevel = 1;
    public int xpBonusPerLevel = 6;

    [Header("Vida")]
    public int maxHealth = 2;

    [Header("Combate")]
    public float contactDamage = 8f;
    public float attackRange = 1.25f;
    public float attackHitPadding = 0.65f;
    public float attackCooldown = 1f;
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;
    public float targetRefreshInterval = 0.4f;
    public bool pursueTarget = true;
    public float engageRange = 999f;
    public bool huntPlayerAtNight = true;

    [Header("Drop")]
    public int minGoldDrop = 5;
    public int maxGoldDrop = 10;
    public int xpReward = 20;

    [Header("Identidade")]
    public string rewardDisplayName = "MiniKrug";

    [Header("Colisao")]
    public Vector3 colliderCenter = new Vector3(0f, 0.42f, 0f);
    public float colliderRadius = 0.38f;
    public float colliderHeight = 0.9f;

    [Header("Chao")]
    public float groundRayHeight = 10f;
    public float maxGroundRayDistance = 25f;
    public LayerMask groundMask = ~0;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 1.4f, 0f);
    public float healthUiVisibleDuration = 2f;
    public float damagePopupLifetime = 0.8f;
    public float damagePopupRiseSpeed = 1.4f;

    [Header("Visual")]
    public float zombieVisualScale = 1.45f;

    int currentHealth;
    float nextAttackTime;
    float nextTargetRefreshTime;
    float healthUiHideTime;
    int baseMaxHealth;
    float baseContactDamage;
    float baseMoveSpeed;
    int baseMinGoldDrop;
    int baseMaxGoldDrop;
    int baseXpReward;
    bool baseStatsCached;
    public int CurrentHealth => currentHealth;
    public int EnemyLevel => enemyLevel;
    public bool IsPendingDestroy => isPendingDestroy;

    Transform targetTransform;
    string targetPlayerId;
    PlayerMovement lastAttacker;
    MiniKrugSpawnPoint spawnPoint;
    System.Action<MiniKrug> deathCallback;
    Canvas worldCanvas;
    Image healthFillImage;
    TextMeshProUGUI healthText;
    MiniKrugLegacyAnimationDriver animationDriver;
    MiniKrugZombieAnimatorDriver zombieAnimationDriver;
    Coroutine destroyRoutine;
    GameObject visualOverrideInstance;
    bool isPendingDestroy;

    void Awake()
    {
        CacheBaseStats();
        ApplyLevelScaling(enemyLevel);
    }

    void Start()
    {
        currentHealth = isPendingDestroy ? 0 : Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
        ApplyVisualOverrideIfNeeded();
        EnsureMainCollider();
        EnsureStablePhysics();
        SnapToGround();
        EnsureCombatUI();
        UpdateHealthUI(false);
        RefreshTarget();
        PlayIdleAnimation();
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

        bool targetInEngageRange = IsTargetInsideEngageRange();
        bool canAggressivelyEngageTarget = targetInEngageRange || ShouldIgnoreEngageRangeBecauseOfNight();

        if (pursueTarget && canAggressivelyEngageTarget)
            FollowTarget();
        else
            FaceTarget();

        TryAttackPlayer(canAggressivelyEngageTarget);
        UpdateUIFacing();
    }

    public void SetSpawnData(MiniKrugSpawnPoint owner)
    {
        spawnPoint = owner;
    }

    public void SetDeathCallback(System.Action<MiniKrug> callback)
    {
        deathCallback = callback;
    }

    public void SetEnemyLevel(int level)
    {
        ApplyLevelScaling(level);
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
        UpdateHealthUI(worldCanvas != null && worldCanvas.gameObject.activeSelf);
    }

    public void RefreshBaseStats()
    {
        baseStatsCached = false;
        CacheBaseStats();
        ApplyLevelScaling(enemyLevel);

        if (currentHealth > 0)
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UpdateHealthUI(worldCanvas != null && worldCanvas.gameObject.activeSelf);
    }

    public void Hit(int damage, PlayerMovement attacker)
    {
        if (isPendingDestroy)
            return;

        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        lastAttacker = attacker;
        PlayDamageAnimation();
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        if (currentHealth <= 0)
            Die();
    }

    public void ApplyNetworkHit(int damage, out int goldAmount, out int xpAmount, out int remainingHealth, out bool destroyed)
    {
        if (isPendingDestroy)
        {
            destroyed = true;
            goldAmount = 0;
            xpAmount = 0;
            remainingHealth = 0;
            return;
        }

        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        PlayDamageAnimation();
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        destroyed = currentHealth <= 0;
        goldAmount = destroyed ? Random.Range(minGoldDrop, maxGoldDrop + 1) : 0;
        xpAmount = destroyed ? xpReward : 0;
        remainingHealth = Mathf.Max(0, currentHealth);

        if (destroyed)
        {
            NotifySpawnOwners();
            BeginDeathSequence();
        }
    }

    public void ApplyNetworkState(Vector3 networkPosition, Quaternion networkRotation, int networkLevel, int networkHealth, bool destroyed)
    {
        ApplyLevelScaling(networkLevel);
        transform.SetPositionAndRotation(networkPosition, networkRotation);
        currentHealth = Mathf.Max(0, networkHealth);
        UpdateHealthUI(!destroyed);

        if (destroyed)
            BeginDeathSequence();
    }

    public void PlayLocalHitFeedback(int damage)
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        ShowDamagePopup(Mathf.Max(1, damage));
        ShowHealthUITemporarily();
        UpdateHealthUI(true);
    }

    void FollowTarget()
    {
        if (targetTransform == null)
            return;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        Vector3 direction = toTarget.normalized;
        Vector3 nextPos = transform.position + direction * moveSpeed * Time.deltaTime;

        if (TryGetGroundPosition(nextPos, out Vector3 groundedPos))
            nextPos = groundedPos;

        transform.position = nextPos;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void TryAttackPlayer(bool targetInEngageRange)
    {
        if (targetTransform == null || Time.time < nextAttackTime)
            return;

        if (!targetInEngageRange)
            return;

        Vector3 toPlayer = targetTransform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > attackRange + Mathf.Max(0f, attackHitPadding))
            return;

        PlayAttackAnimation();

        PlayerMovement targetPlayer = targetTransform.GetComponent<PlayerMovement>();
        targetPlayer?.RegisterBossOrMiniBossCombat();

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.IsMultiplayerActive &&
            !string.IsNullOrWhiteSpace(targetPlayerId))
            LanMultiplayerManager.Instance.ApplyEnemyDamage(targetPlayerId, contactDamage);
        else
        {
            targetPlayer?.TakeDamage(contactDamage);
        }

        nextAttackTime = Time.time + attackCooldown;
    }

    bool IsTargetInsideEngageRange()
    {
        if (targetTransform == null)
            return false;

        if (engageRange <= 0f)
            return true;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude <= engageRange * engageRange;
    }

    bool ShouldIgnoreEngageRangeBecauseOfNight()
    {
        if (!huntPlayerAtNight)
            return false;

        DayNightCycle cycle = DayNightCycle.Instance;
        return cycle != null && cycle.IsNight;
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
            if (candidate == null)
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
        if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            transform.position = groundedPosition;
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

        enemyLevel = Mathf.Max(1, level);
        int bonusLevels = Mathf.Max(0, enemyLevel - 1);

        maxHealth = baseMaxHealth + bonusLevels * Mathf.Max(0, healthBonusPerLevel);
        contactDamage = baseContactDamage + bonusLevels * Mathf.Max(0f, contactDamageBonusPerLevel);
        moveSpeed = baseMoveSpeed + bonusLevels * Mathf.Max(0f, moveSpeedBonusPerLevel);
        minGoldDrop = baseMinGoldDrop + bonusLevels * Mathf.Max(0, goldBonusPerLevel);
        maxGoldDrop = Mathf.Max(minGoldDrop, baseMaxGoldDrop + bonusLevels * Mathf.Max(0, goldBonusPerLevel));
        xpReward = baseXpReward + bonusLevels * Mathf.Max(0, xpBonusPerLevel);
    }

    void Die()
    {
        if (isPendingDestroy)
            return;

        AwardExperienceIfKilledByPlayer();
        DropGoldIfKilledByPlayer();
        TryDropDungeonKey();
        NotifySpawnOwners();
        BeginDeathSequence();
    }

    void AwardExperienceIfKilledByPlayer()
    {
        if (lastAttacker == null)
            return;

        PlayerProgression progression = lastAttacker.GetComponent<PlayerProgression>();
        if (progression != null)
            progression.AddExperience(xpReward, rewardDisplayName);
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

    void TryDropDungeonKey()
    {
        if (lastAttacker == null || !ShouldDropDungeonKey())
            return;

        if (Random.value > DungeonKeyDropChance)
            return;

        Inventory inventory = lastAttacker.GetComponent<Inventory>();
        Hotbar hotbar = lastAttacker.GetComponent<Hotbar>() ?? FindFirstObjectByType<Hotbar>();
        Item keyItem = MushroomTyrantDungeonKeyRegistry.GetOrCreate();

        if (inventory == null || keyItem == null)
            return;

        inventory.AddItem(MushroomTyrantDungeonKeyRegistry.ItemName, 1, keyItem);

        if (hotbar != null && keyItem.icon != null)
            hotbar.TryAddInventoryItem(new InventoryItem(MushroomTyrantDungeonKeyRegistry.ItemName, 1, keyItem));

        MessageSystem.Instance?.ShowMessage("A chave da Camara do Cogumelo Tirano caiu.");
    }

    bool ShouldDropDungeonKey()
    {
        return string.Equals(rewardDisplayName, "Mushroom Mon", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(rewardDisplayName, "Mushroom", System.StringComparison.OrdinalIgnoreCase) ||
               gameObject.name.IndexOf("mushroom", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void EnsureCombatUI()
    {
        if (worldCanvas != null)
            return;

        GameObject canvasObject = new GameObject("CombatUI");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = uiWorldOffset;

        worldCanvas = canvasObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = Camera.main;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 30f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1.8f, 0.7f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        GameObject bgObject = CreateUiObject("HealthBg", canvasObject.transform);
        Image bgImage = bgObject.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        RectTransform bgRect = bgObject.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.2f, 0.55f);
        bgRect.anchorMax = new Vector2(0.8f, 0.8f);
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
        healthText.fontSize = 18f;
        healthText.color = Color.white;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.anchoredPosition = new Vector2(0f, 2f);

        canvasObject.SetActive(false);
    }

    void UpdateHealthUI(bool visible)
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            return;

        float normalizedHealth = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        healthFillImage.fillAmount = normalizedHealth;
        healthFillImage.color = Color.Lerp(new Color(0.7f, 0.08f, 0.08f), new Color(0.2f, 0.9f, 0.25f), normalizedHealth);
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
        popupObject.transform.localPosition = new Vector3(Random.Range(-0.12f, 0.12f), 0.22f, 0f);

        TextMeshProUGUI popupText = popupObject.AddComponent<TextMeshProUGUI>();
        popupText.text = damage.ToString();
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.fontSize = 24f;
        popupText.fontStyle = FontStyles.Bold;
        popupText.color = new Color(1f, 0.93f, 0.35f, 1f);

        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 40f);

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

    void ResolveAnimationDrivers()
    {
        if (animationDriver == null)
            animationDriver = GetComponent<MiniKrugLegacyAnimationDriver>() ?? GetComponentInChildren<MiniKrugLegacyAnimationDriver>(true);

        if (zombieAnimationDriver == null)
            zombieAnimationDriver = GetComponent<MiniKrugZombieAnimatorDriver>() ?? GetComponentInChildren<MiniKrugZombieAnimatorDriver>(true);
    }

    void ApplyVisualOverrideIfNeeded()
    {
        if (visualOverrideInstance != null)
            return;

        if (!ShouldUseZombieVisualOverride())
            return;

        GameObject visualPrefab = Resources.Load<GameObject>(DefaultNightZombieVisualPath);
        if (visualPrefab == null)
            return;

        DisableExistingChildVisuals();

        visualOverrideInstance = Instantiate(visualPrefab, transform);
        visualOverrideInstance.name = "MiniKrugZombieVisual";
        visualOverrideInstance.transform.localPosition = Vector3.zero;
        visualOverrideInstance.transform.localRotation = Quaternion.identity;
        visualOverrideInstance.transform.localScale = Vector3.one * zombieVisualScale;

        ResolveAnimationDrivers();
        PlayIdleAnimation();
    }

    bool ShouldUseZombieVisualOverride()
    {
        return string.Equals(rewardDisplayName, "MiniKrug", System.StringComparison.OrdinalIgnoreCase);
    }

    void DisableExistingChildVisuals()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform == transform)
                continue;

            renderer.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.transform == transform)
                continue;

            collider.enabled = false;
        }
    }

    void PlayIdleAnimation()
    {
        ResolveAnimationDrivers();

        if (zombieAnimationDriver != null)
        {
            zombieAnimationDriver.PlayIdle();
            return;
        }

        animationDriver?.PlayIdle();
    }

    void PlayAttackAnimation()
    {
        ResolveAnimationDrivers();

        if (zombieAnimationDriver != null)
        {
            zombieAnimationDriver.PlayAttack();
            return;
        }

        animationDriver?.PlayAttack();
    }

    void PlayDamageAnimation()
    {
        ResolveAnimationDrivers();

        if (zombieAnimationDriver != null)
        {
            zombieAnimationDriver.PlayDamage();
            return;
        }

        animationDriver?.PlayDamage();
    }

    void NotifySpawnOwners()
    {
        if (spawnPoint != null)
            spawnPoint.NotifyMiniKrugDeath(this);

        deathCallback?.Invoke(this);
        deathCallback = null;
    }

    void BeginDeathSequence()
    {
        if (isPendingDestroy)
            return;

        isPendingDestroy = true;
        currentHealth = 0;
        nextAttackTime = float.MaxValue;
        targetTransform = null;
        targetPlayerId = null;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        UpdateHealthUI(false);
        ResolveAnimationDrivers();

        float destroyDelay = 0.05f;
        if (zombieAnimationDriver != null)
        {
            zombieAnimationDriver.PlayDeath();
            destroyDelay = Mathf.Max(destroyDelay, zombieAnimationDriver.DeathDuration);
        }
        else if (animationDriver != null)
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
