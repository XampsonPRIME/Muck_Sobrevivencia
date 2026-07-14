using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EarthGolem : MonoBehaviour
{
    enum GolemState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        ReturnHome,
        GroundSlam,
        HeavyPunch,
        Recover,
        Hit,
        Dead
    }

    [Header("Configuracao")]
    public EarthGolemConfig config;
    public int suggestedLevel = 20;

    [Header("Vida")]
    public int maxHealth = 1200;
    public int defense = 80;
    public int resistance = 90;
    [Range(0f, 1f)] public float physicalDamageReduction = 0.25f;
    public float visualScale = 1.9f;

    [Header("IA")]
    public float detectionDistance = 15f;
    public float provocationDistance = 4.5f;
    public float loseTargetDistance = 26f;
    public float attackDistance = 6f;
    public float patrolSpeed = 1.15f;
    public float chaseSpeed = 2.15f;
    public float rotationSpeed = 5.2f;
    public float patrolRadius = 8f;
    public float reachDistance = 0.8f;
    public Vector2 idleTimeRange = new Vector2(2.2f, 5f);
    public float returnHealPerSecond = 18f;

    [Header("Golpe no Chao")]
    public int groundSlamDamage = 85;
    public float groundSlamRadius = 6.8f;
    public float groundSlamCooldown = 4.25f;
    public float groundSlamWindupDuration = 0.58f;
    public float groundSlamRecoveryDuration = 0.42f;
    public float groundSlamStunDuration = 1.1f;
    public float groundSlamKnockbackForce = 8.5f;

    [Header("Soco Pesado")]
    public int heavyPunchDamage = 70;
    public float heavyPunchRange = 5f;
    public float heavyPunchCooldown = 3.1f;
    public float heavyPunchWindupDuration = 0.32f;
    public float heavyPunchRecoveryDuration = 0.35f;
    public float heavyPunchKnockbackForce = 8f;
    public float heavyPunchKnockbackDuration = 0.28f;

    [Header("Chao")]
    public bool useNavMeshWhenAvailable = true;
    public float navMeshSampleDistance = 3f;
    public float groundRayHeight = 24f;
    public float maxGroundRayDistance = 64f;
    public LayerMask groundMask = ~0;

    [Header("Drops")]
    [Range(0f, 1f)] public float stoneFragmentChance = 0.7f;
    public int minStoneFragments = 3;
    public int maxStoneFragments = 8;
    [Range(0f, 1f)] public float resilientMossChance = 0.4f;
    public int minResilientMoss = 1;
    public int maxResilientMoss = 3;
    [Range(0f, 1f)] public float ironOreChance = 0.1f;
    public int minIronOre = 1;
    public int maxIronOre = 2;
    [Range(0f, 1f)] public float earthCoreChance = 0.2f;
    public float dropRadius = 1.2f;
    public Item stoneFragmentItemData;
    public Item resilientMossItemData;
    public Item ironOreItemData;
    public Item earthCoreItemData;

    [Header("Visual")]
    public bool buildBodyOnStart = true;
    public bool rebuildVisualOnStart = true;
    public float deathAnimationDuration = 1.2f;
    public Material stoneMaterial;
    public Material darkStoneMaterial;
    public Material mossMaterial;
    public Material eyeMaterial;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 4.65f, 0f);
    public float healthUiVisibleDuration = 3f;
    public float damagePopupLifetime = 0.9f;
    public float damagePopupRiseSpeed = 1.1f;

    int currentHealth;
    Vector3 homePosition;
    Vector3 patrolTarget;
    float stateTimer;
    float attackElapsed;
    float nextGroundSlamTime;
    float nextPunchTime;
    float hitFlashTimer;
    bool hasPatrolTarget;
    bool provoked;
    bool attackApplied;
    string targetPlayerId;

    GolemState state = GolemState.Idle;
    Transform player;
    PlayerMovement playerMovement;
    NavMeshAgent agent;
    EarthGolemSpawnPoint spawnPoint;
    Transform visualRoot;
    Transform torsoPart;
    Transform headPart;
    Transform leftUpperArm;
    Transform leftLowerArm;
    Transform rightUpperArm;
    Transform rightLowerArm;
    Transform leftLeg;
    Transform rightLeg;
    Renderer[] renderers;
    Color[] baseRenderColors;
    MobHealthBar healthBar;

    public int CurrentHealth => currentHealth;
    public bool IsDead => state == GolemState.Dead;
    public bool IsPendingDestroy => state == GolemState.Dead;
    public int GolemLevel => suggestedLevel;

    void Start()
    {
        ApplyConfig();
        currentHealth = maxHealth;
        homePosition = transform.position;
        EnsureItemData();
        EnsureMaterials();
        EnsureBestiaryIdentity();

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

    void Update()
    {
        if (state == GolemState.Dead)
            return;

        ResolvePlayer();
        UpdateState();
        AnimateVisuals();
    }

    public void SetSpawnData(EarthGolemSpawnPoint owner, Vector3 spawnHome)
    {
        spawnPoint = owner;
        homePosition = spawnHome;
        patrolTarget = spawnHome;
    }

    public void ForceTotemTarget(PlayerMovement target, bool enterChaseImmediately = true)
    {
        if (target == null || state == GolemState.Dead)
            return;

        playerMovement = target;
        player = target.transform;
        targetPlayerId = null;
        provoked = true;
        detectionDistance = Mathf.Max(detectionDistance, 260f);
        loseTargetDistance = Mathf.Max(loseTargetDistance, 380f);
        provocationDistance = Mathf.Max(provocationDistance, 260f);
        patrolRadius = Mathf.Max(patrolRadius, 50f);
        returnHealPerSecond = 0f;

        if (!enterChaseImmediately)
            return;

        if (state == GolemState.GroundSlam || state == GolemState.HeavyPunch || state == GolemState.Recover || state == GolemState.Hit)
            return;

        EnterChase();
    }

    public void Hit(int damage, PlayerMovement attacker = null)
    {
        if (state == GolemState.Dead)
            return;

        if (attacker != null)
        {
            playerMovement = attacker;
            player = attacker.transform;
            targetPlayerId = null;
        }

        provoked = true;
        BestiaryService.Instance?.Discover(BestiaryDatabase.EarthGolemId);
        ApplyDamage(damage, true, out _);
    }

    public void ApplyNetworkHit(
        int damage,
        out int stoneAmount,
        out int mossAmount,
        out int ironAmount,
        out int coreAmount,
        out int remainingHealth,
        out bool destroyed)
    {
        stoneAmount = 0;
        mossAmount = 0;
        ironAmount = 0;
        coreAmount = 0;

        if (state == GolemState.Dead)
        {
            remainingHealth = 0;
            destroyed = true;
            return;
        }

        provoked = true;
        ApplyDamage(damage, false, out destroyed);
        remainingHealth = Mathf.Max(0, currentHealth);

        if (destroyed)
            RollLootAmounts(out stoneAmount, out mossAmount, out ironAmount, out coreAmount);
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
        if (state == GolemState.Dead)
            EnterIdle();
    }

    public void PlayLocalHitFeedback(int damage)
    {
        EnsureCombatUI();
        ShowDamagePopup(Mathf.Max(1, Mathf.RoundToInt(damage * (1f - physicalDamageReduction))));
        ShowHealthUITemporarily();
        UpdateHealthUI(true);
        hitFlashTimer = 0.24f;
        EarthGolemAudio.PlayHit(transform.position);
        provoked = true;
        if (player != null)
            EnterChase();
    }

    void ApplyConfig()
    {
        if (config == null)
            return;

        maxHealth = Mathf.Max(1, config.maxHealth);
        defense = Mathf.Max(0, config.defense);
        resistance = Mathf.Max(0, config.resistance);
        physicalDamageReduction = Mathf.Clamp01(config.physicalDamageReduction);
        visualScale = Mathf.Max(1f, config.visualScale);
        detectionDistance = Mathf.Max(1f, config.detectionDistance);
        provocationDistance = Mathf.Max(1f, config.provocationDistance);
        loseTargetDistance = Mathf.Max(detectionDistance, config.loseTargetDistance);
        attackDistance = Mathf.Max(1f, config.attackDistance);
        patrolSpeed = Mathf.Max(0.1f, config.patrolSpeed);
        chaseSpeed = Mathf.Max(patrolSpeed, config.chaseSpeed);
        patrolRadius = Mathf.Max(1f, config.patrolRadius);
        returnHealPerSecond = Mathf.Max(0f, config.returnHealPerSecond);
        groundSlamDamage = Mathf.Max(1, config.groundSlamDamage);
        groundSlamRadius = Mathf.Max(1f, config.groundSlamRadius);
        groundSlamCooldown = Mathf.Max(0.1f, config.groundSlamCooldown);
        groundSlamWindupDuration = Mathf.Max(0.1f, config.groundSlamWindupDuration);
        groundSlamRecoveryDuration = Mathf.Max(0.05f, config.groundSlamRecoveryDuration);
        groundSlamStunDuration = Mathf.Max(0f, config.groundSlamStunDuration);
        heavyPunchDamage = Mathf.Max(1, config.heavyPunchDamage);
        heavyPunchRange = Mathf.Max(1f, config.heavyPunchRange);
        heavyPunchCooldown = Mathf.Max(0.1f, config.heavyPunchCooldown);
        heavyPunchWindupDuration = Mathf.Max(0.08f, config.heavyPunchWindupDuration);
        heavyPunchRecoveryDuration = Mathf.Max(0.05f, config.heavyPunchRecoveryDuration);
        heavyPunchKnockbackForce = Mathf.Max(0f, config.heavyPunchKnockbackForce);
        stoneFragmentChance = Mathf.Clamp01(config.stoneFragmentChance);
        minStoneFragments = Mathf.Max(0, config.minStoneFragments);
        maxStoneFragments = Mathf.Max(minStoneFragments, config.maxStoneFragments);
        resilientMossChance = Mathf.Clamp01(config.resilientMossChance);
        minResilientMoss = Mathf.Max(0, config.minResilientMoss);
        maxResilientMoss = Mathf.Max(minResilientMoss, config.maxResilientMoss);
        ironOreChance = Mathf.Clamp01(config.ironOreChance);
        minIronOre = Mathf.Max(0, config.minIronOre);
        maxIronOre = Mathf.Max(minIronOre, config.maxIronOre);
        earthCoreChance = Mathf.Clamp01(config.earthCoreChance);
    }

    void UpdateState()
    {
        switch (state)
        {
            case GolemState.Idle:
                stateTimer -= Time.deltaTime;
                if (HandlePassiveDetection())
                    return;

                if (stateTimer <= 0f)
                    EnterPatrol();
                break;

            case GolemState.Patrol:
                if (HandlePassiveDetection())
                    return;

                Patrol();
                break;

            case GolemState.Alert:
                stateTimer -= Time.deltaTime;
                StopAgent();
                FaceTarget();

                if (CanSeePlayer())
                {
                    if (DistanceToPlayer() <= provocationDistance || provoked)
                    {
                        provoked = true;
                        EnterChase();
                        return;
                    }
                }

                if (stateTimer <= 0f)
                    EnterIdle();
                break;

            case GolemState.Chase:
                Chase();
                break;

            case GolemState.ReturnHome:
                ReturnHome();
                break;

            case GolemState.GroundSlam:
                UpdateGroundSlam();
                break;

            case GolemState.HeavyPunch:
                UpdateHeavyPunch();
                break;

            case GolemState.Recover:
            case GolemState.Hit:
                stateTimer -= Time.deltaTime;
                StopAgent();
                if (stateTimer <= 0f)
                    EnterChaseOrReturn();
                break;
        }
    }

    bool HandlePassiveDetection()
    {
        if (!CanSeePlayer())
            return false;

        BestiaryService.Instance?.Discover(BestiaryDatabase.EarthGolemId);
        EnterAlert();
        return true;
    }

    bool CanSeePlayer()
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= detectionDistance;
    }

    float DistanceToPlayer()
    {
        return player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;
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
            EnterReturnHome();
            return;
        }

        float distance = DistanceToPlayer();
        if (distance > loseTargetDistance)
        {
            EnterReturnHome();
            return;
        }

        if (distance <= groundSlamRadius && Time.time >= nextGroundSlamTime)
        {
            EnterGroundSlam();
            return;
        }

        if (distance <= heavyPunchRange && Time.time >= nextPunchTime)
        {
            EnterHeavyPunch();
            return;
        }

        MoveTowards(player.position, chaseSpeed);
    }

    void ReturnHome()
    {
        if (CanSeePlayer() && provoked)
        {
            EnterChase();
            return;
        }

        if (currentHealth < maxHealth && returnHealPerSecond > 0f)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.CeilToInt(returnHealPerSecond * Time.deltaTime));
            UpdateHealthUI(healthBar != null && healthBar.IsVisible);
        }

        Vector3 toHome = homePosition - transform.position;
        toHome.y = 0f;

        if (toHome.magnitude <= reachDistance)
        {
            provoked = false;
            EnterIdle();
            return;
        }

        MoveTowards(homePosition, patrolSpeed);
    }

    void UpdateGroundSlam()
    {
        stateTimer -= Time.deltaTime;
        attackElapsed += Time.deltaTime;
        StopAgent();

        if (headPart != null)
            FaceTarget();

        if (!attackApplied && attackElapsed >= groundSlamWindupDuration)
        {
            attackApplied = true;
            ApplyGroundSlamDamage();
        }

        if (stateTimer <= 0f)
            EnterRecover();
    }

    void UpdateHeavyPunch()
    {
        stateTimer -= Time.deltaTime;
        attackElapsed += Time.deltaTime;
        StopAgent();
        FaceTarget();

        if (!attackApplied && attackElapsed >= heavyPunchWindupDuration)
        {
            attackApplied = true;
            ApplyHeavyPunchDamage();
        }

        if (stateTimer <= 0f)
            EnterRecover();
    }

    void ApplyGroundSlamDamage()
    {
        Vector3 attackCenter = transform.position + transform.forward * 0.65f;
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        bool networkAreaDamage = manager != null && manager.IsServerAuthority && manager.IsMultiplayerActive;

        if (networkAreaDamage)
            manager.ApplyEnemyAreaDamage(attackCenter, groundSlamRadius, groundSlamDamage);

        Collider[] hits = Physics.OverlapSphere(attackCenter, groundSlamRadius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<PlayerMovement> localPlayers = new HashSet<PlayerMovement>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            PlayerMovement target = hit.GetComponent<PlayerMovement>() ?? hit.GetComponentInParent<PlayerMovement>();
            if (target == null || !localPlayers.Add(target))
                continue;

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;

            if (!networkAreaDamage)
                target.TakeDamage(groundSlamDamage);

            target.ApplyKnockback(direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward, groundSlamKnockbackForce, 0.32f);
            target.ApplyStun(groundSlamStunDuration);
        }

        EarthGolemAudio.PlaySlam(transform.position);
        EarthGolemScreenShake.Shake(RuntimeCameraCache.Main, 0.22f, 0.14f);
        CreateGroundSlamFx(attackCenter);
    }

    void ApplyHeavyPunchDamage()
    {
        if (player == null)
            return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > heavyPunchRange + 0.45f)
            return;

        float facing = Vector3.Dot(transform.forward, toPlayer.normalized);
        if (facing < 0.25f)
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.IsServerAuthority && manager.IsMultiplayerActive && !string.IsNullOrWhiteSpace(targetPlayerId))
        {
            manager.ApplyEnemyDamage(targetPlayerId, heavyPunchDamage);
        }
        else if (playerMovement != null)
        {
            playerMovement.TakeDamage(heavyPunchDamage);
        }

        if (playerMovement != null)
            playerMovement.ApplyKnockback(toPlayer.normalized, heavyPunchKnockbackForce, heavyPunchKnockbackDuration);

        EarthGolemAudio.PlayPunch(transform.position);
        CreatePunchFx(transform.position + transform.forward * 1.5f + Vector3.up * 1.15f);
    }

    void EnterIdle()
    {
        state = GolemState.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        hasPatrolTarget = false;
        StopAgent();
    }

    void EnterPatrol()
    {
        state = GolemState.Patrol;
        PickPatrolTarget();
    }

    void EnterAlert()
    {
        state = GolemState.Alert;
        stateTimer = 1.6f;
        StopAgent();
        EarthGolemAudio.PlayAlert(transform.position);
    }

    void EnterChase()
    {
        state = GolemState.Chase;
        provoked = true;
        nextGroundSlamTime = Mathf.Max(nextGroundSlamTime, Time.time + 0.35f);
        nextPunchTime = Mathf.Max(nextPunchTime, Time.time + 0.25f);
    }

    void EnterReturnHome()
    {
        state = GolemState.ReturnHome;
        targetPlayerId = null;
        playerMovement = null;
    }

    void EnterGroundSlam()
    {
        state = GolemState.GroundSlam;
        stateTimer = groundSlamWindupDuration + groundSlamRecoveryDuration;
        attackElapsed = 0f;
        attackApplied = false;
        nextGroundSlamTime = Time.time + groundSlamCooldown;
        StopAgent();
    }

    void EnterHeavyPunch()
    {
        state = GolemState.HeavyPunch;
        stateTimer = heavyPunchWindupDuration + heavyPunchRecoveryDuration;
        attackElapsed = 0f;
        attackApplied = false;
        nextPunchTime = Time.time + heavyPunchCooldown;
        StopAgent();
    }

    void EnterRecover()
    {
        state = GolemState.Recover;
        stateTimer = 0.28f;
        StopAgent();
    }

    void EnterHit()
    {
        state = GolemState.Hit;
        stateTimer = 0.22f;
        StopAgent();
    }

    void EnterChaseOrReturn()
    {
        if (provoked && CanSeePlayer())
            EnterChase();
        else
            EnterReturnHome();
    }

    void PickPatrolTarget()
    {
        for (int i = 0; i < 10; i++)
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

    void FaceTarget()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
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

    void ApplyDamage(int damage, bool dropLootOnDeath, out bool destroyed)
    {
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - physicalDamageReduction)));
        currentHealth -= finalDamage;
        hitFlashTimer = 0.24f;

        EnsureCombatUI();
        ShowDamagePopup(finalDamage);
        ShowHealthUITemporarily();
        UpdateHealthUI(true);
        EarthGolemAudio.PlayHit(transform.position);

        destroyed = currentHealth <= 0;
        if (!destroyed)
        {
            EnterHit();
            return;
        }

        currentHealth = 0;
        if (dropLootOnDeath)
        {
            BestiaryService.Instance?.RecordDefeat(BestiaryDatabase.EarthGolemId);
            DropLoot();
        }

        StartDeath(true);
    }

    void StartDeath(bool notifySpawnPoint)
    {
        if (state == GolemState.Dead)
            return;

        state = GolemState.Dead;
        StopAgent();
        UpdateHealthUI(false);
        EarthGolemAudio.PlayDeath(transform.position);
        CreateDeathRubble();

        if (agent != null)
            agent.enabled = false;

        if (notifySpawnPoint && spawnPoint != null)
            spawnPoint.NotifyGolemDeath(this);

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
        Quaternion endRotation = Quaternion.Euler(0f, 0f, 18f);
        Vector3 startPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        Vector3 endPosition = startPosition + Vector3.down * 0.45f;
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

            SetRenderAlpha(Mathf.Lerp(1f, 0.05f, t));
            yield return null;
        }

        Destroy(gameObject);
    }

    void DropLoot()
    {
        RollLootAmounts(out int stoneAmount, out int mossAmount, out int ironAmount, out int coreAmount);

        for (int i = 0; i < stoneAmount; i++)
            CreateDrop(stoneFragmentItemData, "Fragmento de Pedra Drop", new Color(0.5f, 0.43f, 0.34f, 1f), new Vector3(0.24f, 0.2f, 0.24f));

        for (int i = 0; i < mossAmount; i++)
            CreateDrop(resilientMossItemData, "Musgo Resiliente Drop", new Color(0.27f, 0.48f, 0.12f, 1f), new Vector3(0.2f, 0.1f, 0.24f));

        for (int i = 0; i < ironAmount; i++)
            CreateDrop(ironOreItemData, "Ferro Bruto Drop", new Color(0.44f, 0.43f, 0.38f, 1f), new Vector3(0.22f, 0.16f, 0.2f));

        for (int i = 0; i < coreAmount; i++)
            CreateDrop(earthCoreItemData, "Nucleo de Terra Drop", new Color(0.9f, 0.72f, 0.22f, 1f), new Vector3(0.22f, 0.22f, 0.22f));
    }

    void RollLootAmounts(out int stoneAmount, out int mossAmount, out int ironAmount, out int coreAmount)
    {
        stoneAmount = Random.value <= stoneFragmentChance ? Random.Range(minStoneFragments, maxStoneFragments + 1) : 0;
        mossAmount = Random.value <= resilientMossChance ? Random.Range(minResilientMoss, maxResilientMoss + 1) : 0;
        ironAmount = Random.value <= ironOreChance ? Random.Range(minIronOre, maxIronOre + 1) : 0;
        coreAmount = Random.value <= earthCoreChance ? 1 : 0;
    }

    void CreateDrop(Item itemData, string dropName, Color color, Vector3 scale)
    {
        if (itemData == null)
            return;

        Vector2 circle = Random.insideUnitCircle * dropRadius;
        Vector3 spawnPos = transform.position + new Vector3(circle.x, 1.1f, circle.y);

        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = dropName;
        drop.transform.position = spawnPos;
        drop.transform.rotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-18f, 18f));
        drop.transform.localScale = scale;

        Renderer renderer = drop.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial($"{dropName}Material", color);

        Item item = drop.AddComponent<Item>();
        CopyItemData(itemData, item);

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.mass = 0.14f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce((Vector3.up * 1.55f) + new Vector3(circle.x, 0f, circle.y) * 0.65f, ForceMode.Impulse);

        FloatingPickup floatingPickup = drop.AddComponent<FloatingPickup>();
        floatingPickup.groundMask = groundMask;
        floatingPickup.hoverHeight = 0.62f;
        floatingPickup.collectRadius = 1.45f;
    }

    void CopyItemData(Item source, Item target)
    {
        target.itemName = source.itemName;
        target.icon = source.icon;
        target.itemType = source.itemType;
        target.category = source.category;
        target.rarity = source.rarity;
        target.description = source.description;
        target.weight = source.weight;
        target.maxStack = source.maxStack;
        target.toolType = source.toolType;
        target.toolDamage = source.toolDamage;
        target.buyPrice = source.buyPrice;
        target.sellPrice = source.sellPrice;
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
        visualRoot.localScale = Vector3.one * Mathf.Max(1f, visualScale);

        CreateRockPart("Pelvis", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 0.92f, 0f), new Vector3(0.9f, 0.55f, 0.85f), Quaternion.Euler(0f, 15f, -4f), darkStoneMaterial);
        torsoPart = CreateRockPart("Torso", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 1.65f, 0f), new Vector3(1.35f, 1f, 1.05f), Quaternion.Euler(-3f, 0f, 2f), stoneMaterial).transform;
        CreateRockPart("ChestPlate", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.72f, 0.42f), new Vector3(1.05f, 0.72f, 0.36f), Quaternion.Euler(-8f, 12f, 3f), stoneMaterial);
        headPart = CreateRockPart("Head", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 2.45f, 0.16f), new Vector3(0.74f, 0.55f, 0.65f), Quaternion.Euler(-5f, 0f, 0f), darkStoneMaterial).transform;

        CreateRockPart("Brow", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.52f, 0.55f), new Vector3(0.62f, 0.12f, 0.16f), Quaternion.Euler(-6f, 0f, 0f), darkStoneMaterial);
        CreateEye("EyeL", new Vector3(-0.22f, 2.46f, 0.58f));
        CreateEye("EyeR", new Vector3(0.22f, 2.46f, 0.58f));

        leftUpperArm = CreateRockPart("ArmUpperL", PrimitiveType.Sphere, visualRoot, new Vector3(-0.96f, 1.85f, 0f), new Vector3(0.55f, 0.72f, 0.48f), Quaternion.Euler(0f, 0f, -17f), stoneMaterial).transform;
        leftLowerArm = CreateRockPart("ArmLowerL", PrimitiveType.Sphere, visualRoot, new Vector3(-1.22f, 1.08f, 0.04f), new Vector3(0.52f, 0.82f, 0.46f), Quaternion.Euler(0f, 0f, 8f), darkStoneMaterial).transform;
        CreateRockPart("HandL", PrimitiveType.Sphere, visualRoot, new Vector3(-1.15f, 0.42f, 0.16f), new Vector3(0.48f, 0.34f, 0.52f), Quaternion.Euler(0f, 0f, 12f), stoneMaterial);

        rightUpperArm = CreateRockPart("ArmUpperR", PrimitiveType.Sphere, visualRoot, new Vector3(0.96f, 1.85f, 0f), new Vector3(0.55f, 0.72f, 0.48f), Quaternion.Euler(0f, 0f, 17f), stoneMaterial).transform;
        rightLowerArm = CreateRockPart("ArmLowerR", PrimitiveType.Sphere, visualRoot, new Vector3(1.22f, 1.08f, 0.04f), new Vector3(0.52f, 0.82f, 0.46f), Quaternion.Euler(0f, 0f, -8f), darkStoneMaterial).transform;
        CreateRockPart("HandR", PrimitiveType.Sphere, visualRoot, new Vector3(1.15f, 0.42f, 0.16f), new Vector3(0.48f, 0.34f, 0.52f), Quaternion.Euler(0f, 0f, -12f), stoneMaterial);

        leftLeg = CreateRockPart("LegL", PrimitiveType.Sphere, visualRoot, new Vector3(-0.42f, 0.36f, 0f), new Vector3(0.42f, 0.74f, 0.4f), Quaternion.Euler(0f, 0f, -4f), darkStoneMaterial).transform;
        rightLeg = CreateRockPart("LegR", PrimitiveType.Sphere, visualRoot, new Vector3(0.42f, 0.36f, 0f), new Vector3(0.42f, 0.74f, 0.4f), Quaternion.Euler(0f, 0f, 4f), darkStoneMaterial).transform;
        CreateRockPart("FootL", PrimitiveType.Cube, visualRoot, new Vector3(-0.42f, 0.06f, 0.22f), new Vector3(0.48f, 0.18f, 0.62f), Quaternion.Euler(0f, -8f, 0f), stoneMaterial);
        CreateRockPart("FootR", PrimitiveType.Cube, visualRoot, new Vector3(0.42f, 0.06f, 0.22f), new Vector3(0.48f, 0.18f, 0.62f), Quaternion.Euler(0f, 8f, 0f), stoneMaterial);

        CreateMossPatch("MossShoulderL", new Vector3(-0.62f, 2.28f, -0.1f), new Vector3(0.42f, 0.07f, 0.26f), Quaternion.Euler(12f, 0f, -16f));
        CreateMossPatch("MossChest", new Vector3(0.2f, 1.93f, 0.55f), new Vector3(0.34f, 0.06f, 0.22f), Quaternion.Euler(80f, 0f, 8f));
        CreateMossPatch("MossArmR", new Vector3(1.25f, 1.38f, -0.12f), new Vector3(0.32f, 0.06f, 0.24f), Quaternion.Euler(12f, 0f, -20f));

        CacheRenderers();
    }

    GameObject CreateRockPart(string partName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
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

    void CreateEye(string eyeName, Vector3 localPosition)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = eyeName;
        eye.transform.SetParent(visualRoot, false);
        eye.transform.localPosition = localPosition;
        eye.transform.localScale = new Vector3(0.09f, 0.055f, 0.04f);

        Collider collider = eye.GetComponent<Collider>();
        if (collider != null)
            DestroyRuntimeObject(collider);

        Renderer renderer = eye.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = eyeMaterial;
    }

    void CreateMossPatch(string patchName, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
    {
        GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        patch.name = patchName;
        patch.transform.SetParent(visualRoot, false);
        patch.transform.localPosition = localPosition;
        patch.transform.localRotation = localRotation;
        patch.transform.localScale = localScale;

        Collider collider = patch.GetComponent<Collider>();
        if (collider != null)
            DestroyRuntimeObject(collider);

        Renderer renderer = patch.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = mossMaterial;
    }

    void AnimateVisuals()
    {
        if (visualRoot == null)
            return;

        float moving = state == GolemState.Chase || state == GolemState.ReturnHome ? 0.65f : state == GolemState.Patrol ? 0.32f : 0f;
        float phase = Time.time * Mathf.Lerp(1.5f, 3.6f, moving);
        float breath = Mathf.Sin(Time.time * 1.4f) * 0.025f;
        visualRoot.localPosition = new Vector3(0f, breath + Mathf.Sin(phase) * 0.035f * moving, 0f);

        if (torsoPart != null)
            torsoPart.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.2f) * 1.5f, 0f, Mathf.Sin(Time.time * 0.9f) * 1.5f);

        if (headPart != null)
            headPart.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.6f) * 2f, 0f, 0f);

        AnimateLimb(leftLeg, phase, moving, 1f, 7f);
        AnimateLimb(rightLeg, phase, moving, -1f, 7f);

        float slamLift = state == GolemState.GroundSlam && !attackApplied ? Mathf.Sin(Mathf.Clamp01(attackElapsed / Mathf.Max(0.01f, groundSlamWindupDuration)) * Mathf.PI) : 0f;
        float punchLift = state == GolemState.HeavyPunch && !attackApplied ? Mathf.Sin(Mathf.Clamp01(attackElapsed / Mathf.Max(0.01f, heavyPunchWindupDuration)) * Mathf.PI) : 0f;

        if (leftUpperArm != null)
            leftUpperArm.localRotation = Quaternion.Euler(-48f * slamLift + Mathf.Sin(phase) * 9f * moving, 0f, -17f);
        if (leftLowerArm != null)
            leftLowerArm.localRotation = Quaternion.Euler(-36f * slamLift, 0f, 8f);
        if (rightUpperArm != null)
            rightUpperArm.localRotation = Quaternion.Euler((-48f * slamLift) + (-62f * punchLift) + Mathf.Sin(phase + Mathf.PI) * 9f * moving, 0f, 17f);
        if (rightLowerArm != null)
            rightLowerArm.localRotation = Quaternion.Euler((-36f * slamLift) + (-46f * punchLift), 0f, -8f);

        UpdateHitFlash();
    }

    void AnimateLimb(Transform limb, float phase, float speedFactor, float side, float maxAngle)
    {
        if (limb == null)
            return;

        limb.localRotation = Quaternion.Euler(Mathf.Sin(phase) * maxAngle * speedFactor * side, 0f, 0f);
    }

    void CreateGroundSlamFx(Vector3 center)
    {
        for (int i = 0; i < 14; i++)
        {
            float angle = (Mathf.PI * 2f / 14f) * i;
            Vector3 position = center + new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle)) * Random.Range(0.7f, groundSlamRadius);
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.name = "GolemGroundDust";
            dust.transform.position = position;
            dust.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);

            Collider collider = dust.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = dust.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateRuntimeMaterial("GolemDustMaterial", new Color(0.5f, 0.42f, 0.3f, 0.72f));

            Destroy(dust, Random.Range(0.35f, 0.65f));
        }
    }

    void CreatePunchFx(Vector3 center)
    {
        GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        impact.name = "GolemPunchImpact";
        impact.transform.position = center;
        impact.transform.localScale = Vector3.one * 0.34f;

        Collider collider = impact.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = impact.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial("GolemPunchImpactMaterial", new Color(0.8f, 0.62f, 0.28f, 0.85f));

        Destroy(impact, 0.32f);
    }

    void CreateDeathRubble()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 circle = Random.insideUnitCircle * 1.1f;
            GameObject rock = GameObject.CreatePrimitive(Random.value > 0.5f ? PrimitiveType.Cube : PrimitiveType.Sphere);
            rock.name = "GolemDeathRubble";
            rock.transform.position = transform.position + new Vector3(circle.x, Random.Range(0.4f, 2.4f), circle.y);
            rock.transform.localScale = Vector3.one * Random.Range(0.16f, 0.36f);
            rock.transform.rotation = Random.rotation;

            Renderer renderer = rock.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = Random.value > 0.35f ? stoneMaterial : darkStoneMaterial;

            Collider collider = rock.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            Rigidbody rb = rock.AddComponent<Rigidbody>();
            rb.mass = 0.18f;
            rb.AddForce((Vector3.up * Random.Range(0.6f, 1.2f)) + new Vector3(circle.x, 0f, circle.y), ForceMode.Impulse);
            Destroy(rock, Random.Range(1.1f, 1.8f));
        }
    }

    bool TryGetGroundPosition(Vector3 position, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = position + Vector3.up * groundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxGroundRayDistance, groundMask, QueryTriggerInteraction.Ignore);

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

        if (hitCollider.GetComponentInParent<EarthGolem>() != null)
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

    void EnsureCombatUI()
    {
        if (healthBar != null)
            return;

        if (healthBar == null)
            healthBar = GetComponentInChildren<MobHealthBar>(true);

        if (healthBar == null)
            healthBar = gameObject.AddComponent<MobHealthBar>();

        healthBar.Configure(uiWorldOffset, healthUiVisibleDuration, damagePopupLifetime, damagePopupRiseSpeed, new Vector2(2.35f, 0.82f), 0.01f);
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

    void ShowDamagePopup(int damage)
    {
        EnsureCombatUI();
        healthBar.ShowDamage(currentHealth, maxHealth, damage);
    }

    void EnsureMainCollider()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.center = new Vector3(0f, 1.35f * Mathf.Max(1f, visualScale), 0.08f);
        capsule.radius = 0.86f * Mathf.Max(1f, visualScale);
        capsule.height = 2.75f * Mathf.Max(1f, visualScale);
        capsule.direction = 1;
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

        agent.radius = 0.82f * Mathf.Max(1f, visualScale);
        agent.height = 2.75f * Mathf.Max(1f, visualScale);
        agent.acceleration = 8f;
        agent.angularSpeed = 180f;
        agent.stoppingDistance = 0.35f;
        agent.updateRotation = false;
        agent.enabled = true;
        agent.Warp(hit.position);
    }

    void ResolvePlayer()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (manager != null && manager.IsServerAuthority && manager.IsMultiplayerActive &&
            manager.TryFindClosestEnemyTarget(transform.position, out Transform networkTarget, out string playerId))
        {
            player = networkTarget;
            targetPlayerId = playerId;
            playerMovement = networkTarget != null ? networkTarget.GetComponent<PlayerMovement>() ?? networkTarget.GetComponentInParent<PlayerMovement>() : null;
            return;
        }

        if (player != null && player.gameObject.activeInHierarchy)
            return;

        player = LanMultiplayerManager.FindWorldFocusTransform();
        targetPlayerId = null;
        playerMovement = player != null ? player.GetComponent<PlayerMovement>() ?? player.GetComponentInParent<PlayerMovement>() : null;
    }

    void EnsureItemData()
    {
        stoneFragmentItemData ??= StoneFragmentItemRegistry.GetOrCreate();
        resilientMossItemData ??= ResilientMossItemRegistry.GetOrCreate();
        ironOreItemData ??= IronItemRegistry.GetOrCreate();
        earthCoreItemData ??= EarthCoreItemRegistry.GetOrCreate();
    }

    void EnsureMaterials()
    {
        stoneMaterial ??= CreateRuntimeMaterial("EarthGolemStoneRuntime", new Color(0.46f, 0.36f, 0.24f, 1f));
        darkStoneMaterial ??= CreateRuntimeMaterial("EarthGolemDarkStoneRuntime", new Color(0.28f, 0.22f, 0.16f, 1f));
        mossMaterial ??= CreateRuntimeMaterial("EarthGolemMossRuntime", new Color(0.24f, 0.42f, 0.12f, 1f));
        eyeMaterial ??= CreateRuntimeMaterial("EarthGolemEyeRuntime", new Color(0.95f, 0.78f, 0.22f, 1f));
    }

    void EnsureBestiaryIdentity()
    {
        BestiaryCreatureIdentity identity = GetComponent<BestiaryCreatureIdentity>();
        if (identity == null)
            identity = gameObject.AddComponent<BestiaryCreatureIdentity>();

        identity.creatureId = BestiaryDatabase.EarthGolemId;
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
            Color flashColor = Color.Lerp(baseColor, new Color(1f, 0.72f, 0.38f, baseColor.a), 0.58f);
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
            baseRenderColors[i] = renderers[i] != null && renderers[i].sharedMaterial != null ? renderers[i].sharedMaterial.color : Color.white;
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

    void SnapToGround()
    {
        if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition))
            transform.position = groundedPosition;

        if (agent != null && agent.enabled && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            agent.Warp(hit.position);
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

public static class EarthGolemAudio
{
    static AudioClip alertClip;
    static AudioClip hitClip;
    static AudioClip punchClip;
    static AudioClip slamClip;
    static AudioClip deathClip;

    public static void PlayAlert(Vector3 position)
    {
        if (alertClip == null)
            alertClip = CreateClip("EarthGolemAlertRuntime", 0.35f, 70f, 0.22f, 0.09f);

        AudioSource.PlayClipAtPoint(alertClip, position, 0.3f);
    }

    public static void PlayHit(Vector3 position)
    {
        if (hitClip == null)
            hitClip = CreateClip("EarthGolemHitRuntime", 0.16f, 130f, 0.12f, 0.18f);

        AudioSource.PlayClipAtPoint(hitClip, position, 0.28f);
    }

    public static void PlayPunch(Vector3 position)
    {
        if (punchClip == null)
            punchClip = CreateClip("EarthGolemPunchRuntime", 0.22f, 95f, 0.2f, 0.14f);

        AudioSource.PlayClipAtPoint(punchClip, position, 0.36f);
    }

    public static void PlaySlam(Vector3 position)
    {
        if (slamClip == null)
            slamClip = CreateClip("EarthGolemSlamRuntime", 0.32f, 55f, 0.26f, 0.2f);

        AudioSource.PlayClipAtPoint(slamClip, position, 0.46f);
    }

    public static void PlayDeath(Vector3 position)
    {
        if (deathClip == null)
            deathClip = CreateClip("EarthGolemDeathRuntime", 0.55f, 42f, 0.24f, 0.22f);

        AudioSource.PlayClipAtPoint(deathClip, position, 0.42f);
    }

    static AudioClip CreateClip(string clipName, float duration, float baseFrequency, float tonalAmount, float noiseAmount)
    {
        int sampleRate = 22050;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalized = i / (float)sampleCount;
            float envelope = Mathf.Exp(-normalized * 5.5f);
            float tone = Mathf.Sin(t * baseFrequency * Mathf.PI * 2f) * tonalAmount;
            float sub = Mathf.Sin(t * baseFrequency * 0.5f * Mathf.PI * 2f) * tonalAmount * 0.65f;
            float noise = Mathf.Sin((i * 12.9898f + 78.233f) * 0.13f) * noiseAmount;
            samples[i] = (tone + sub + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

public class EarthGolemScreenShake : MonoBehaviour
{
    float remaining;
    float intensity;
    Vector3 originalLocalPosition;

    public static void Shake(Camera camera, float duration, float strength)
    {
        if (camera == null)
            return;

        EarthGolemScreenShake shake = camera.GetComponent<EarthGolemScreenShake>();
        if (shake == null)
            shake = camera.gameObject.AddComponent<EarthGolemScreenShake>();

        shake.Begin(duration, strength);
    }

    void Begin(float duration, float strength)
    {
        if (remaining <= 0f)
            originalLocalPosition = transform.localPosition;

        remaining = Mathf.Max(remaining, duration);
        intensity = Mathf.Max(intensity, strength);
    }

    void LateUpdate()
    {
        if (remaining <= 0f)
            return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            transform.localPosition = originalLocalPosition;
            intensity = 0f;
            return;
        }

        transform.localPosition = originalLocalPosition + Random.insideUnitSphere * intensity;
        intensity = Mathf.MoveTowards(intensity, 0f, Time.deltaTime * 0.9f);
    }
}
