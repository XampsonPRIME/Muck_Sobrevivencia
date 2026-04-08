using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniKrug : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 2;

    [Header("Combate")]
    public float contactDamage = 8f;
    public float attackRange = 1.25f;
    public float attackCooldown = 1f;
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;
    public float targetRefreshInterval = 0.4f;

    [Header("Drop")]
    public int minGoldDrop = 5;
    public int maxGoldDrop = 10;
    public int xpReward = 20;

    [Header("Chao")]
    public float groundRayHeight = 10f;
    public float maxGroundRayDistance = 25f;
    public LayerMask groundMask = ~0;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 1.4f, 0f);
    public float healthUiVisibleDuration = 2f;
    public float damagePopupLifetime = 0.8f;
    public float damagePopupRiseSpeed = 1.4f;

    int currentHealth;
    float nextAttackTime;
    float nextTargetRefreshTime;
    float healthUiHideTime;

    PlayerMovement targetPlayer;
    PlayerMovement lastAttacker;
    MiniKrugSpawnPoint spawnPoint;
    Canvas worldCanvas;
    Image healthFillImage;
    TextMeshProUGUI healthText;

    void Start()
    {
        currentHealth = maxHealth;
        EnsureMainCollider();
        SnapToGround();
        EnsureCombatUI();
        UpdateHealthUI(false);
        RefreshTarget();
    }

    void Update()
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        if (Time.time >= nextTargetRefreshTime || targetPlayer == null)
            RefreshTarget();

        FollowTarget();
        TryAttackPlayer();
        UpdateUIFacing();
    }

    public void SetSpawnData(MiniKrugSpawnPoint owner)
    {
        spawnPoint = owner;
    }

    public void Hit(int damage, PlayerMovement attacker)
    {
        if (worldCanvas == null || healthFillImage == null || healthText == null)
            EnsureCombatUI();

        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        lastAttacker = attacker;
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);

        if (currentHealth <= 0)
            Die();
    }

    void FollowTarget()
    {
        if (targetPlayer == null)
            return;

        Vector3 toTarget = targetPlayer.transform.position - transform.position;
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
            if (candidate == null)
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
        if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            transform.position = groundedPosition;
    }

    void Die()
    {
        AwardExperienceIfKilledByPlayer();
        DropGoldIfKilledByPlayer();

        if (spawnPoint != null)
            spawnPoint.NotifyMiniKrugDeath(this);

        Destroy(gameObject);
    }

    void AwardExperienceIfKilledByPlayer()
    {
        if (lastAttacker == null)
            return;

        PlayerProgression progression = lastAttacker.GetComponent<PlayerProgression>();
        if (progression != null)
            progression.AddExperience(xpReward, "MiniKrug");
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
        if (col == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.42f, 0f);
            capsule.radius = 0.38f;
            capsule.height = 0.9f;
        }
    }
}
