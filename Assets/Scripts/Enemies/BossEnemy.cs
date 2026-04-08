using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossEnemy : MonoBehaviour
{
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

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 2.4f, 0f);
    public float healthUiVisibleDuration = 3f;
    public float damagePopupLifetime = 0.9f;
    public float damagePopupRiseSpeed = 1.8f;

    int currentHealth;
    float nextAttackTime;
    float nextTargetRefreshTime;
    float nextPatrolRefreshTime;
    float healthUiHideTime;
    public int CurrentHealth => currentHealth;

    Vector3 spawnPosition;
    Vector3 patrolTarget;

    PlayerMovement targetPlayer;
    PlayerMovement lastAttacker;
    Canvas worldCanvas;
    Image healthFillImage;
    TextMeshProUGUI healthText;

    void Start()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        patrolTarget = spawnPosition;
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
    }

    void Update()
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        if (Time.time >= nextTargetRefreshTime || targetPlayer == null)
            RefreshTarget();

        UpdateMovement();
        TryAttackPlayer();
        UpdateUIFacing();
    }

    public void Hit(int damage, PlayerMovement attacker)
    {
        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        lastAttacker = attacker;
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        if (currentHealth <= 0)
            Die();
    }

    public void ApplyNetworkHit(int damage, out int goldAmount, out int xpAmount, out bool unlockMagic, out int remainingHealth, out bool destroyed)
    {
        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        destroyed = currentHealth <= 0;
        goldAmount = destroyed ? Random.Range(minGoldDrop, maxGoldDrop + 1) : 0;
        xpAmount = destroyed ? xpReward : 0;
        unlockMagic = destroyed;
        remainingHealth = Mathf.Max(0, currentHealth);

        if (destroyed)
            Destroy(gameObject);
    }

    public void ApplyNetworkState(int networkHealth, bool destroyed)
    {
        currentHealth = Mathf.Max(0, networkHealth);
        UpdateHealthUI(!destroyed);

        if (destroyed)
            Destroy(gameObject);
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

        if (targetPlayer == null)
            return false;

        Vector3 toPlayerFromSpawn = targetPlayer.transform.position - spawnPosition;
        toPlayerFromSpawn.y = 0f;

        Vector3 toPlayerFromBoss = targetPlayer.transform.position - transform.position;
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

    void TryAttackPlayer()
    {
        if (targetPlayer == null || Time.time < nextAttackTime)
            return;

        Vector3 toPlayer = targetPlayer.transform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > attackRange)
            return;

        targetPlayer.TakeDamage(contactDamage);
        nextAttackTime = Time.time + attackCooldown;
    }

    void RefreshTarget()
    {
        nextTargetRefreshTime = Time.time + targetRefreshInterval;

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

        targetPlayer = closestPlayer;
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
        DropMagicPickup();
        AwardExperienceIfKilledByPlayer();
        DropGoldIfKilledByPlayer();
        Destroy(gameObject);
    }

    void AwardExperienceIfKilledByPlayer()
    {
        if (lastAttacker == null)
            return;

        PlayerProgression progression = lastAttacker.GetComponent<PlayerProgression>();
        if (progression != null)
            progression.AddExperience(xpReward, "Boss");
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
        if (col == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.2f, 0f);
            capsule.radius = 0.85f;
            capsule.height = 2.4f;
        }
    }

    void EnsureStablePhysics()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }
}
