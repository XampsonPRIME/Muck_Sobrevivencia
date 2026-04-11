using UnityEngine;

[RequireComponent(typeof(Animation))]
public class MushroomMonsterEnemy : MonoBehaviour
{
    const string IdleAnimation = "Idle";
    const string RunAnimation = "Run";
    const string AttackAnimation = "Attack";
    const string DamageAnimation = "Damage";
    const string DeathAnimation = "Death";

    [Header("Vida")]
    public int maxHealth = 18;

    [Header("Combate")]
    public float detectionRange = 18f;
    public float disengageRange = 24f;
    public float attackRange = 1.8f;
    public float attackCooldown = 1.25f;
    public float contactDamage = 14f;
    public float moveSpeed = 2.6f;
    public float rotationSpeed = 7f;
    public float targetRefreshInterval = 0.4f;

    [Header("Recompensa")]
    public int minGoldDrop = 6;
    public int maxGoldDrop = 12;
    public int xpReward = 28;

    [Header("Chao")]
    public float groundRayHeight = 12f;
    public float maxGroundRayDistance = 40f;
    public LayerMask groundMask = ~0;

    int currentHealth;
    float nextAttackTime;
    float nextTargetRefreshTime;
    Transform targetTransform;
    string targetPlayerId;
    PlayerMovement lastAttacker;
    Animation legacyAnimation;
    Vector3 lastPosition;
    MushroomMonsterForestSpawner spawnOwner;

    public int CurrentHealth => currentHealth;
    public int EnemyLevel => 1;

    void Awake()
    {
        legacyAnimation = GetComponent<Animation>();
    }

    void Start()
    {
        currentHealth = maxHealth;
        EnsureMainCollider();
        EnsureStablePhysics();
        SnapToGround();
        PlayAnimation(IdleAnimation, 0.15f);
        lastPosition = transform.position;
    }

    void Update()
    {
        if (ShouldUseNetworkAuthority())
        {
            UpdateAnimationFromMovement();
            return;
        }

        if (Time.time >= nextTargetRefreshTime)
            RefreshTarget();

        if (targetTransform == null)
        {
            PlayAnimation(IdleAnimation, 0.2f);
            return;
        }

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > disengageRange)
        {
            targetTransform = null;
            targetPlayerId = null;
            PlayAnimation(IdleAnimation, 0.2f);
            return;
        }

        if (distance > attackRange)
        {
            FollowTarget(toTarget, distance);
            PlayAnimation(RunAnimation, 0.15f);
        }
        else
        {
            FaceTarget(toTarget);
            TryAttackTarget();
        }

        lastPosition = transform.position;
    }

    public void SetSpawnOwner(MushroomMonsterForestSpawner owner)
    {
        spawnOwner = owner;
    }

    public void Hit(int damage, PlayerMovement attacker)
    {
        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        lastAttacker = attacker;
        PlayAnimation(DamageAnimation, 0.05f);

        if (currentHealth <= 0)
            Die();
    }

    public void ApplyNetworkHit(int damage, out int goldAmount, out int xpAmount, out int remainingHealth, out bool destroyed)
    {
        int finalDamage = Mathf.Max(1, damage);
        currentHealth -= finalDamage;
        PlayAnimation(DamageAnimation, 0.05f);

        destroyed = currentHealth <= 0;
        goldAmount = destroyed ? Random.Range(minGoldDrop, maxGoldDrop + 1) : 0;
        xpAmount = destroyed ? xpReward : 0;
        remainingHealth = Mathf.Max(0, currentHealth);

        if (!destroyed)
            return;

        spawnOwner?.NotifyMonsterDestroyed(this);
        PlayAnimation(DeathAnimation, 0.05f);
        Destroy(gameObject, 0.15f);
    }

    public void ApplyNetworkState(Vector3 networkPosition, Quaternion networkRotation, int networkLevel, int networkHealth, bool destroyed)
    {
        Vector3 previousPosition = transform.position;
        transform.SetPositionAndRotation(networkPosition, networkRotation);
        currentHealth = Mathf.Max(0, networkHealth);

        if (destroyed)
        {
            PlayAnimation(DeathAnimation, 0.05f);
            Destroy(gameObject, 0.15f);
            return;
        }

        if ((networkPosition - previousPosition).sqrMagnitude > 0.0025f)
            PlayAnimation(RunAnimation, 0.1f);
        else
            PlayAnimation(IdleAnimation, 0.15f);
    }

    public void PlayLocalHitFeedback(int damage)
    {
        PlayAnimation(DamageAnimation, 0.05f);
    }

    void RefreshTarget()
    {
        nextTargetRefreshTime = Time.time + targetRefreshInterval;

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.IsMultiplayerActive &&
            LanMultiplayerManager.Instance.IsServerAuthority &&
            LanMultiplayerManager.Instance.TryFindClosestEnemyTarget(transform.position, out Transform networkTarget, out string playerId))
        {
            float networkDistance = Vector3.Distance(transform.position, networkTarget.position);
            if (networkDistance <= detectionRange)
            {
                targetTransform = networkTarget;
                targetPlayerId = playerId;
                return;
            }
        }

        PlayerMovement[] players = LanMultiplayerManager.GetGameplayPlayers();
        float bestDistance = float.MaxValue;
        PlayerMovement closestPlayer = null;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement candidate = players[i];
            if (candidate == null)
                continue;

            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closestPlayer = candidate;
            }
        }

        if (closestPlayer != null && bestDistance <= detectionRange * detectionRange)
        {
            targetTransform = closestPlayer.transform;
            targetPlayerId = null;
            return;
        }

        targetTransform = null;
        targetPlayerId = null;
    }

    void FollowTarget(Vector3 toTarget, float distance)
    {
        if (distance <= 0.001f)
            return;

        Vector3 direction = toTarget / Mathf.Max(0.001f, distance);
        Vector3 nextPosition = transform.position + direction * moveSpeed * Time.deltaTime;

        if (TryGetGroundPosition(nextPosition, out Vector3 groundedPosition))
            nextPosition = groundedPosition;

        transform.position = nextPosition;
        FaceTarget(toTarget);
    }

    void FaceTarget(Vector3 toTarget)
    {
        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void TryAttackTarget()
    {
        if (Time.time < nextAttackTime)
            return;

        PlayAnimation(AttackAnimation, 0.05f);

        if (LanMultiplayerManager.Instance != null && LanMultiplayerManager.Instance.IsMultiplayerActive)
            LanMultiplayerManager.Instance.ApplyEnemyDamage(targetPlayerId, contactDamage);
        else
            targetTransform.GetComponent<PlayerMovement>()?.TakeDamage(contactDamage);

        nextAttackTime = Time.time + attackCooldown;
    }

    void Die()
    {
        AwardPlayerRewards();
        spawnOwner?.NotifyMonsterDestroyed(this);
        PlayAnimation(DeathAnimation, 0.05f);
        Destroy(gameObject, 0.2f);
    }

    void AwardPlayerRewards()
    {
        if (lastAttacker == null)
            return;

        PlayerProgression progression = lastAttacker.GetComponent<PlayerProgression>();
        if (progression != null)
            progression.AddExperience(xpReward, "Mushroom Monster");

        Inventory inventory = lastAttacker.GetComponent<Inventory>();
        if (inventory != null)
        {
            int amount = Random.Range(minGoldDrop, maxGoldDrop + 1);
            inventory.AddItem("Gold", amount, GoldItemRegistry.GetOrCreate());
            MessageSystem.Instance?.ShowMessage($"+{amount} gold");
        }
    }

    void PlayAnimation(string clipName, float fadeDuration)
    {
        if (legacyAnimation == null || legacyAnimation.GetClip(clipName) == null)
            return;

        if (legacyAnimation.IsPlaying(clipName))
            return;

        legacyAnimation.CrossFade(clipName, fadeDuration);
    }

    bool TryGetGroundPosition(Vector3 position, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = position + Vector3.up * groundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxGroundRayDistance, groundMask, QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        groundedPosition = position;
        bool foundGround = false;

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

    void SnapToGround()
    {
        if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            transform.position = groundedPosition;
    }

    bool IsValidGroundHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null || hit.normal.y < 0.35f)
            return false;

        Transform hitTransform = collider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return false;

        if (collider.GetComponentInParent<Cow>() != null)
            return false;

        if (collider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (collider.GetComponentInParent<MushroomMonsterEnemy>() != null)
            return false;

        if (collider.GetComponentInParent<BossEnemy>() != null)
            return false;

        if (collider.GetComponentInParent<PlayerMovement>() != null)
            return false;

        if (collider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        return true;
    }

    void EnsureMainCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.95f, 0f);
        capsule.radius = 0.55f;
        capsule.height = 2f;
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

    void UpdateAnimationFromMovement()
    {
        float movement = (transform.position - lastPosition).sqrMagnitude;
        if (movement > 0.0015f)
            PlayAnimation(RunAnimation, 0.1f);
        else
            PlayAnimation(IdleAnimation, 0.15f);

        lastPosition = transform.position;
    }

    bool ShouldUseNetworkAuthority()
    {
        return LanMultiplayerManager.Instance != null &&
               LanMultiplayerManager.Instance.IsMultiplayerActive &&
               LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Client;
    }
}
