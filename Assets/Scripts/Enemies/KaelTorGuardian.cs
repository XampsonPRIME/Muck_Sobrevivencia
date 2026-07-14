using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KaelTorGuardian : MonoBehaviour
{
    public const string DisplayName = "Kael'Tor, o Vigia dos Artefatos";

    [Header("Vida")]
    public int maxHealth = 4200;
    public int currentHealth;
    public float visualScale = 1f;

    [Header("Movimento")]
    public float detectionRange = 46f;
    public float arenaRadius = 34f;
    public float chaseSpeed = 3.35f;
    public float phaseTwoSpeedMultiplier = 1.12f;
    public float phaseThreeSpeedMultiplier = 1.25f;
    public float rotationSpeed = 4.8f;
    public LayerMask groundMask = ~0;

    [Header("Ataques")]
    public int heavyPunchDamage = 90;
    public int stompDamage = 85;
    public int rockThrowDamage = 75;
    public int seismicWaveDamage = 95;
    public int rockRainDamage = 80;
    public int chargeDamage = 120;
    public int finalSlamDamage = 165;

    public float heavyPunchCooldown = 2.9f;
    public float stompCooldown = 4.4f;
    public float rockThrowCooldown = 3.7f;
    public float seismicWaveCooldown = 8.5f;
    public float summonCooldown = 18f;
    public float rockRainCooldown = 11f;
    public float chargeCooldown = 8f;
    public float finalSlamCooldown = 14f;

    [Header("Drops")]
    public float dropRadius = 3.1f;

    [Header("UI")]
    public Vector3 healthBarOffset = new Vector3(0f, 9.8f, 0f);
    public float healthBarVisibleDuration = 7f;

    Transform player;
    PlayerMovement playerMovement;
    Vector3 arenaCenter;
    Vector3 lastKnownPlayerPosition;
    bool awakened;
    bool attacking;
    bool dead;
    bool phaseTwoSummoned;
    int phase = 1;

    float nextPunchTime;
    float nextStompTime;
    float nextRockThrowTime;
    float nextSeismicTime;
    float nextSummonTime;
    float nextRockRainTime;
    float nextChargeTime;
    float nextFinalSlamTime;
    float footstepTimer;
    float hitFlashTimer;

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Transform> floatingShards = new List<Transform>();
    readonly List<Transform> rageRunes = new List<Transform>();
    readonly List<EarthGolem> summonedGolems = new List<EarthGolem>();

    Transform visualRoot;
    Transform headPart;
    Transform chestPart;
    Transform leftArm;
    Transform rightArm;
    Transform leftHand;
    Transform rightHand;
    Transform leftLeg;
    Transform rightLeg;
    Material stoneMaterial;
    Material darkStoneMaterial;
    Material mossMaterial;
    Material eyeMaterial;
    Material runeMaterial;
    Material rageMaterial;
    Color runeBaseColor;
    MobHealthBar healthBar;
    Rigidbody body;

    public int CurrentHealth => currentHealth;
    public bool IsDead => dead;

    void Start()
    {
        arenaCenter = transform.position;
        currentHealth = Mathf.Max(1, maxHealth);
        lastKnownPlayerPosition = transform.position + transform.forward * 10f;
        EnsureBestiaryIdentity();
        EnsureMaterials();
        BuildProceduralModel();
        EnsurePhysics();
        SnapToGround();
        EnsureHealthBar();
        EnsureWorldMapMarker();
        LanNetworkEntity.Ensure(this, "KaelTorGuardian|Demo");
    }

    void Update()
    {
        if (dead)
            return;

        ResolvePlayer();
        AnimateVisuals();
        UpdateHitFlash();

        if (player == null)
            return;

        float distanceToPlayer = HorizontalDistance(transform.position, player.position);
        if (!awakened)
        {
            if (distanceToPlayer > detectionRange)
                return;

            Awaken();
        }

        if (attacking)
            return;

        UpdatePhase();
        FaceTarget(player.position);
        TryStartAttack(distanceToPlayer);
    }

    public void Hit(int damage, PlayerMovement attacker = null)
    {
        if (dead)
            return;

        if (attacker != null)
        {
            playerMovement = attacker;
            player = attacker.transform;
            lastKnownPlayerPosition = player.position;
        }

        if (!awakened)
            Awaken();

        int finalDamage = Mathf.Max(1, damage);
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        hitFlashTimer = 0.12f;

        BestiaryService.Instance?.Discover(BestiaryDatabase.KaelTorGuardianId);
        healthBar?.ShowDamage(currentHealth, maxHealth, finalDamage);
        BowCombatAudio.PlayImpact(transform.position + Vector3.up * 3.8f, true);

        if (currentHealth <= 0)
            StartCoroutine(DeathRoutine());
        else
            UpdatePhase();
    }

    public bool CanBeChallengedBy(PlayerMovement challenger)
    {
        return true;
    }

    public string BuildMinimumLevelMessage()
    {
        return string.Empty;
    }

    void Awaken()
    {
        awakened = true;
        BestiaryService.Instance?.Discover(BestiaryDatabase.KaelTorGuardianId);
        MessageSystem.Instance?.ShowMessage("Kael'Tor despertou nas ruinas antigas.");
        DebugCommandChat.AddSystemMessage("Kael'Tor: Os artefatos nunca deveriam retornar...");
        healthBar?.SetHealth(currentHealth, maxHealth, true);
        nextPunchTime = Time.time + 1.2f;
        nextRockThrowTime = Time.time + 1.8f;
        nextStompTime = Time.time + 2.4f;
    }

    void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
        {
            lastKnownPlayerPosition = player.position;
            return;
        }

        playerMovement = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();
        player = playerMovement != null ? playerMovement.transform : LanMultiplayerManager.FindWorldFocusTransform();
        if (player != null)
            lastKnownPlayerPosition = player.position;
    }

    void UpdatePhase()
    {
        float normalizedHealth = maxHealth > 0 ? currentHealth / (float)maxHealth : 0f;
        int newPhase = normalizedHealth <= 0.3f ? 3 : normalizedHealth <= 0.7f ? 2 : 1;
        if (newPhase <= phase)
            return;

        phase = newPhase;
        if (phase == 2)
        {
            MessageSystem.Instance?.ShowMessage("Kael'Tor entra em furia.");
            DebugCommandChat.AddSystemMessage("Kael'Tor: Voces nao entendem...");
            nextSeismicTime = Time.time + 1.2f;
            nextSummonTime = Time.time + 2.2f;
            SetRuneIntensity(true);
        }
        else if (phase == 3)
        {
            MessageSystem.Instance?.ShowMessage("A arena treme com energia corrompida.");
            DebugCommandChat.AddSystemMessage("Kael'Tor: Ele esta despertando...");
            nextRockRainTime = Time.time + 1.1f;
            nextChargeTime = Time.time + 2.5f;
            nextFinalSlamTime = Time.time + 5.5f;
            SetRageVisual(true);
        }
    }

    void TryStartAttack(float distanceToPlayer)
    {
        if (phase >= 3)
        {
            if (Time.time >= nextFinalSlamTime && distanceToPlayer <= 18f)
            {
                StartCoroutine(FinalSlamRoutine());
                return;
            }

            if (Time.time >= nextRockRainTime)
            {
                StartCoroutine(RockRainRoutine());
                return;
            }

            if (Time.time >= nextChargeTime && distanceToPlayer > 7f)
            {
                StartCoroutine(ChargeRoutine());
                return;
            }
        }

        if (phase >= 2)
        {
            if (!phaseTwoSummoned && Time.time >= nextSummonTime)
            {
                StartCoroutine(SummonRoutine());
                return;
            }

            if (Time.time >= nextSeismicTime && distanceToPlayer <= 15f)
            {
                StartCoroutine(SeismicWaveRoutine());
                return;
            }
        }

        if (distanceToPlayer <= 5.8f && Time.time >= nextPunchTime)
        {
            StartCoroutine(HeavyPunchRoutine());
            return;
        }

        if (distanceToPlayer <= 9.2f && Time.time >= nextStompTime)
        {
            StartCoroutine(StompRoutine());
            return;
        }

        if (distanceToPlayer > 7f && Time.time >= nextRockThrowTime)
        {
            StartCoroutine(RockThrowRoutine());
            return;
        }

        MoveTowardPlayer();
    }

    IEnumerator HeavyPunchRoutine()
    {
        attacking = true;
        nextPunchTime = Time.time + heavyPunchCooldown;
        Vector3 start = rightArm != null ? rightArm.localRotation.eulerAngles : Vector3.zero;

        yield return AnimateLimb(rightArm, Quaternion.Euler(22f, 0f, -38f), 0.28f);
        CreateTelegraph(transform.position + transform.forward * 3f, 3.4f, new Color(1f, 0.42f, 0.08f, 0.72f), 0.22f);
        yield return new WaitForSeconds(0.18f);

        if (rightHand != null)
            rightHand.localPosition += Vector3.forward * 0.45f;

        DamagePlayerCone(transform.position + Vector3.up * 2.7f, transform.forward, 6.2f, 0.35f, heavyPunchDamage, 11f, 0.32f);
        SpawnImpactBurst(transform.position + transform.forward * 3.2f + Vector3.up * 0.2f, 1.1f, new Color(0.82f, 0.54f, 0.22f, 1f));
        yield return new WaitForSeconds(0.32f);

        if (rightArm != null)
            rightArm.localRotation = Quaternion.Euler(start);

        if (rightHand != null)
            rightHand.localPosition -= Vector3.forward * 0.45f;

        yield return new WaitForSeconds(0.18f);
        attacking = false;
    }

    IEnumerator StompRoutine()
    {
        attacking = true;
        nextStompTime = Time.time + stompCooldown;
        CreateTelegraph(transform.position, 8.8f, new Color(0.95f, 0.32f, 0.08f, 0.62f), 0.7f);
        yield return AnimateLimb(leftLeg, Quaternion.Euler(-22f, 0f, 8f), 0.38f);
        yield return new WaitForSeconds(0.32f);

        DamagePlayerArea(transform.position, 8.8f, stompDamage, 10f, 0.34f);
        SpawnImpactBurst(transform.position, 1.8f, new Color(0.65f, 0.42f, 0.2f, 1f));
        ShakeVisual(0.3f, 0.16f);

        if (leftLeg != null)
            leftLeg.localRotation = Quaternion.identity;

        yield return new WaitForSeconds(0.45f);
        attacking = false;
    }

    IEnumerator RockThrowRoutine()
    {
        attacking = true;
        nextRockThrowTime = Time.time + rockThrowCooldown;
        Vector3 target = player != null ? player.position + Vector3.up * 1.1f : lastKnownPlayerPosition;

        yield return AnimateLimb(leftArm, Quaternion.Euler(-28f, 0f, 38f), 0.34f);
        yield return new WaitForSeconds(0.24f);

        Vector3 spawn = transform.position + transform.forward * 2.4f + Vector3.up * 5.6f;
        KaelTorRockProjectile.Create(spawn, target, rockThrowDamage, playerMovement);
        if (leftArm != null)
            leftArm.localRotation = Quaternion.identity;

        yield return new WaitForSeconds(0.38f);
        attacking = false;
    }

    IEnumerator SeismicWaveRoutine()
    {
        attacking = true;
        nextSeismicTime = Time.time + seismicWaveCooldown;
        CreateTelegraph(transform.position, 14f, new Color(0.9f, 0.16f, 0.06f, 0.55f), 1.05f);
        yield return new WaitForSeconds(0.72f);

        for (int i = 0; i < 3; i++)
        {
            float radius = 6.5f + i * 3.8f;
            SpawnImpactBurst(transform.position, radius * 0.17f, new Color(0.75f, 0.45f, 0.2f, 1f));
            DamagePlayerArea(transform.position, radius, seismicWaveDamage, 12f, 0.32f);
            yield return new WaitForSeconds(0.18f);
        }

        yield return new WaitForSeconds(0.4f);
        attacking = false;
    }

    IEnumerator SummonRoutine()
    {
        attacking = true;
        phaseTwoSummoned = true;
        nextSummonTime = Time.time + summonCooldown;
        CreateTelegraph(transform.position, 11f, new Color(0.24f, 0.78f, 1f, 0.45f), 1.1f);
        yield return new WaitForSeconds(0.85f);

        for (int i = 0; i < 2; i++)
        {
            float angle = i == 0 ? -35f : 35f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * transform.forward * 9f;
            SpawnMinorGolem(ResolveGround(transform.position + offset));
        }

        MessageSystem.Instance?.ShowMessage("Kael'Tor invocou guardioes menores.");
        yield return new WaitForSeconds(0.45f);
        attacking = false;
    }

    IEnumerator RockRainRoutine()
    {
        attacking = true;
        nextRockRainTime = Time.time + rockRainCooldown;
        Vector3 center = player != null ? player.position : lastKnownPlayerPosition;
        const int rockCount = 7;

        for (int i = 0; i < rockCount; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * 8f;
            Vector3 impact = ResolveGround(center + new Vector3(circle.x, 0f, circle.y));
            CreateTelegraph(impact, 2.2f, new Color(1f, 0.18f, 0.08f, 0.6f), 0.85f);
            StartCoroutine(FallingRockRoutine(impact, 0.85f + i * 0.05f));
            yield return new WaitForSeconds(0.08f);
        }

        yield return new WaitForSeconds(1.35f);
        attacking = false;
    }

    IEnumerator ChargeRoutine()
    {
        attacking = true;
        nextChargeTime = Time.time + chargeCooldown;
        Vector3 target = player != null ? player.position : lastKnownPlayerPosition;
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            direction = transform.forward;

        direction.Normalize();
        CreateTelegraph(transform.position + direction * 8f, 5f, new Color(1f, 0.22f, 0.06f, 0.48f), 0.72f);
        yield return new WaitForSeconds(0.62f);

        float endTime = Time.time + 1.25f;
        bool applied = false;
        while (Time.time < endTime)
        {
            transform.position = ResolveGround(transform.position + direction * (8.5f * Time.deltaTime));
            FaceTarget(transform.position + direction);
            if (!applied && player != null && HorizontalDistance(transform.position, player.position) <= 5.2f)
            {
                DamagePlayerArea(transform.position + direction * 1.8f, 5.8f, chargeDamage, 15f, 0.35f);
                applied = true;
            }

            yield return null;
        }

        SpawnImpactBurst(transform.position + direction * 2f, 1.4f, new Color(0.72f, 0.35f, 0.16f, 1f));
        yield return new WaitForSeconds(0.55f);
        attacking = false;
    }

    IEnumerator FinalSlamRoutine()
    {
        attacking = true;
        nextFinalSlamTime = Time.time + finalSlamCooldown;
        MessageSystem.Instance?.ShowMessage("Kael'Tor prepara um golpe devastador.");
        CreateTelegraph(transform.position, 16f, new Color(1f, 0.06f, 0.02f, 0.7f), 1.45f);
        yield return AnimateLimb(leftArm, Quaternion.Euler(44f, 0f, 34f), 0.38f);
        yield return AnimateLimb(rightArm, Quaternion.Euler(44f, 0f, -34f), 0.38f);
        yield return new WaitForSeconds(0.55f);

        DamagePlayerArea(transform.position, 16f, finalSlamDamage, 18f, 0.45f);
        SpawnImpactBurst(transform.position, 2.6f, new Color(1f, 0.24f, 0.08f, 1f));
        ShakeVisual(0.55f, 0.24f);

        if (leftArm != null)
            leftArm.localRotation = Quaternion.identity;
        if (rightArm != null)
            rightArm.localRotation = Quaternion.identity;

        yield return new WaitForSeconds(0.75f);
        attacking = false;
    }

    IEnumerator FallingRockRoutine(Vector3 impactPoint, float delay)
    {
        yield return new WaitForSeconds(delay);

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "KaelTorFallingRock";
        rock.transform.position = impactPoint + Vector3.up * 18f;
        rock.transform.localScale = new Vector3(1.8f, 1.35f, 1.7f);
        Renderer renderer = rock.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = darkStoneMaterial;

        Collider collider = rock.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        float fallTime = 0.38f;
        float elapsed = 0f;
        Vector3 start = rock.transform.position;
        while (elapsed < fallTime)
        {
            elapsed += Time.deltaTime;
            rock.transform.position = Vector3.Lerp(start, impactPoint + Vector3.up * 0.65f, elapsed / fallTime);
            yield return null;
        }

        DamagePlayerArea(impactPoint, 2.5f, rockRainDamage, 9f, 0.25f);
        SpawnImpactBurst(impactPoint, 0.9f, new Color(0.68f, 0.44f, 0.22f, 1f));
        Destroy(rock);
    }

    IEnumerator AnimateLimb(Transform limb, Quaternion targetRotation, float duration)
    {
        if (limb == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Quaternion start = limb.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            limb.localRotation = Quaternion.Slerp(start, targetRotation, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    IEnumerator DeathRoutine()
    {
        if (dead)
            yield break;

        dead = true;
        attacking = false;
        BestiaryService.Instance?.RecordDefeat(BestiaryDatabase.KaelTorGuardianId);
        PlayerAnimationBridge.Trigger(playerMovement, PlayerAnimationBridge.VictoryTrigger);
        DebugCommandChat.AddSystemMessage("Kael'Tor: Voces nao entendem...");
        DebugCommandChat.AddSystemMessage("Kael'Tor: Os artefatos nunca deveriam retornar...");
        DebugCommandChat.AddSystemMessage("Kael'Tor: Ele esta despertando...");
        MessageSystem.Instance?.ShowMessage("Kael'Tor foi derrotado.");
        healthBar?.Hide();

        DropGuaranteedLoot();
        SpawnFirstArtifactPickup();
        SetRageVisual(true);

        float elapsed = 0f;
        Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        while (elapsed < 2.4f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 2.4f);
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.Lerp(startScale, startScale * 0.72f, t);
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -10f, t));
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.material == null)
                    continue;

                Color color = renderer.material.color;
                color.a = Mathf.Lerp(1f, 0.18f, t);
                renderer.material.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    void MoveTowardPlayer()
    {
        if (player == null)
            return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 18f)
            return;

        float speed = chaseSpeed;
        if (phase == 2)
            speed *= phaseTwoSpeedMultiplier;
        else if (phase >= 3)
            speed *= phaseThreeSpeedMultiplier;

        Vector3 next = transform.position + toPlayer.normalized * (speed * Time.deltaTime);
        transform.position = ResolveGround(next);

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            footstepTimer = 0.62f;
            SpawnImpactBurst(transform.position - transform.forward * 1.4f, 0.55f, new Color(0.38f, 0.28f, 0.18f, 1f));
        }
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationSpeed * Time.deltaTime);
    }

    void DamagePlayerArea(Vector3 center, float radius, int damage, float knockbackForce, float knockbackDuration)
    {
        if (playerMovement == null || GameState.IsPlayerDead)
            return;

        float distance = HorizontalDistance(center, playerMovement.transform.position);
        if (distance > radius)
            return;

        playerMovement.TakeDamage(damage);
        Vector3 direction = playerMovement.transform.position - center;
        if (direction.sqrMagnitude < 0.01f)
            direction = playerMovement.transform.position - transform.position;
        playerMovement.ApplyKnockback(direction, knockbackForce, knockbackDuration);
        playerMovement.RegisterBossOrMiniBossCombat(10f);
    }

    void DamagePlayerCone(Vector3 origin, Vector3 direction, float range, float dotThreshold, int damage, float knockbackForce, float knockbackDuration)
    {
        if (playerMovement == null || GameState.IsPlayerDead)
            return;

        Vector3 toPlayer = playerMovement.transform.position + Vector3.up * 1f - origin;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > range * range)
            return;

        if (Vector3.Dot(direction.normalized, toPlayer.normalized) < dotThreshold)
            return;

        playerMovement.TakeDamage(damage);
        playerMovement.ApplyKnockback(toPlayer, knockbackForce, knockbackDuration);
        playerMovement.RegisterBossOrMiniBossCombat(10f);
    }

    void SpawnMinorGolem(Vector3 position)
    {
        GameObject golemObject = new GameObject("Golem Menor de Kael'Tor");
        golemObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation((arenaCenter - position).normalized, Vector3.up));

        EarthGolem golem = golemObject.AddComponent<EarthGolem>();
        golem.maxHealth = 380;
        golem.defense = 20;
        golem.resistance = 25;
        golem.physicalDamageReduction = 0.08f;
        golem.visualScale = 1.25f;
        golem.detectionDistance = 220f;
        golem.loseTargetDistance = 320f;
        golem.provocationDistance = 220f;
        golem.attackDistance = 5f;
        golem.patrolSpeed = 1.8f;
        golem.chaseSpeed = 3.2f;
        golem.returnHealPerSecond = 0f;
        golem.stoneFragmentChance = 0.35f;
        golem.resilientMossChance = 0.2f;
        golem.ironOreChance = 0f;
        golem.earthCoreChance = 0f;
        golem.SetSpawnData(null, position);
        LanNetworkEntity.Ensure(golem, $"KaelTorMinorGolem|{Time.frameCount}|{summonedGolems.Count}");
        summonedGolems.Add(golem);
        StartCoroutine(ForceMinorGolemTargetNextFrame(golem));
    }

    IEnumerator ForceMinorGolemTargetNextFrame(EarthGolem golem)
    {
        yield return null;
        if (golem != null && playerMovement != null)
            golem.ForceTotemTarget(playerMovement, true);
    }

    void DropGuaranteedLoot()
    {
        CreateDrop(AncestralCoreItemRegistry.GetOrCreate(), "Nucleo Ancestral Drop", new Color(0.92f, 0.72f, 0.22f, 1f), new Vector3(0.42f, 0.42f, 0.42f));
        CreateDrop(FirstArtifactFragmentItemRegistry.GetOrCreate(), "Fragmento do Primeiro Artefato Drop", new Color(0.9f, 0.24f, 0.12f, 1f), new Vector3(0.36f, 0.5f, 0.36f));
        CreateDrop(KaelTorTrophyItemRegistry.GetOrCreate(), "Trofeu de Kael'Tor Drop", new Color(0.5f, 0.38f, 0.24f, 1f), new Vector3(0.48f, 0.34f, 0.48f));
    }

    void CreateDrop(Item itemData, string dropName, Color color, Vector3 scale)
    {
        if (itemData == null)
            return;

        Vector2 circle = UnityEngine.Random.insideUnitCircle * dropRadius;
        Vector3 spawnPos = ResolveGround(transform.position + new Vector3(circle.x, 0f, circle.y)) + Vector3.up * 1.35f;

        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = dropName;
        drop.transform.position = spawnPos;
        drop.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-18f, 18f));
        drop.transform.localScale = scale;

        Renderer renderer = drop.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial($"{dropName}Material", color);

        Item item = drop.AddComponent<Item>();
        CopyItemData(itemData, item);

        Rigidbody rb = drop.AddComponent<Rigidbody>();
        rb.mass = 0.16f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce((Vector3.up * 2.1f) + new Vector3(circle.x, 0f, circle.y) * 0.45f, ForceMode.Impulse);

        FloatingPickup pickup = drop.AddComponent<FloatingPickup>();
        pickup.groundMask = groundMask;
        pickup.hoverHeight = 0.86f;
        pickup.collectRadius = 1.65f;
    }

    void SpawnFirstArtifactPickup()
    {
        Vector3 spawnPosition = ResolveGround(arenaCenter + Vector3.up * 0.4f);
        GameObject artifactObject = new GameObject("Primeiro Artefato Demo Pickup");
        artifactObject.transform.position = spawnPosition + Vector3.up * 1.15f;
        artifactObject.AddComponent<FirstArtifactDemoPickup>();
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
        target.equipmentSlot = source.equipmentSlot;
        target.buyPrice = source.buyPrice;
        target.sellPrice = source.sellPrice;
    }

    void EnsureBestiaryIdentity()
    {
        BestiaryCreatureIdentity identity = GetComponent<BestiaryCreatureIdentity>();
        if (identity == null)
            identity = gameObject.AddComponent<BestiaryCreatureIdentity>();

        identity.creatureId = BestiaryDatabase.KaelTorGuardianId;
    }

    void EnsureWorldMapMarker()
    {
        WorldMapMarker marker = GetComponent<WorldMapMarker>();
        if (marker == null)
            marker = gameObject.AddComponent<WorldMapMarker>();

        marker.label = "Ruinas de Kael'Tor";
        marker.markerColor = new Color(1f, 0.66f, 0.12f, 0.95f);
        marker.markerSize = 15f;
    }

    void EnsureHealthBar()
    {
        healthBar = GetComponent<MobHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<MobHealthBar>();

        healthBar.canvasSize = new Vector2(3.8f, 0.95f);
        healthBar.canvasScale = 0.012f;
        healthBar.Configure(healthBarOffset, healthBarVisibleDuration);
        healthBar.SetHealth(currentHealth, maxHealth, false);
    }

    void EnsurePhysics()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.center = new Vector3(0f, 4.4f, 0.05f);
        capsule.radius = 2.55f;
        capsule.height = 8.9f;
        capsule.direction = 1;

        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void EnsureMaterials()
    {
        stoneMaterial = CreateRuntimeMaterial("KaelTorStoneRuntime", new Color(0.38f, 0.3f, 0.52f, 1f));
        darkStoneMaterial = CreateRuntimeMaterial("KaelTorDarkStoneRuntime", new Color(0.16f, 0.13f, 0.26f, 1f));
        mossMaterial = CreateRuntimeMaterial("KaelTorMossRuntime", new Color(0.22f, 0.55f, 0.22f, 1f));
        eyeMaterial = CreateRuntimeMaterial("KaelTorEyeRuntime", new Color(0.1f, 0.88f, 1f, 1f));
        runeMaterial = CreateRuntimeMaterial("KaelTorRuneRuntime", new Color(0.18f, 0.9f, 1f, 1f));
        rageMaterial = CreateRuntimeMaterial("KaelTorRageRuntime", new Color(1f, 0.12f, 0.04f, 1f));
        runeBaseColor = runeMaterial.color;
    }

    void BuildProceduralModel()
    {
        Transform existing = transform.Find("Visual");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject root = new GameObject("Visual");
        root.transform.SetParent(transform, false);
        visualRoot = root.transform;

        CreatePart("Pelvis", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 2.25f, 0f), new Vector3(2.5f, 1.2f, 2.05f), Quaternion.Euler(0f, 12f, -3f), darkStoneMaterial);
        chestPart = CreatePart("Chest", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 4.3f, 0.05f), new Vector3(3.35f, 2.35f, 2.55f), Quaternion.Euler(-3f, 0f, 2f), stoneMaterial).transform;
        CreatePart("AncientChestPlate", PrimitiveType.Cube, visualRoot, new Vector3(0f, 4.38f, 1.2f), new Vector3(2.2f, 1.25f, 0.44f), Quaternion.Euler(-8f, 4f, 0f), darkStoneMaterial);
        headPart = CreatePart("Head", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 6.45f, 0.42f), new Vector3(1.55f, 1.1f, 1.3f), Quaternion.Euler(-7f, 0f, 0f), darkStoneMaterial).transform;

        CreatePart("Brow", PrimitiveType.Cube, visualRoot, new Vector3(0f, 6.62f, 1.2f), new Vector3(1.25f, 0.22f, 0.22f), Quaternion.Euler(-6f, 0f, 0f), darkStoneMaterial);
        CreateEye("EyeL", new Vector3(-0.36f, 6.43f, 1.28f));
        CreateEye("EyeR", new Vector3(0.36f, 6.43f, 1.28f));

        leftArm = CreatePart("ArmUpperL", PrimitiveType.Sphere, visualRoot, new Vector3(-2.45f, 4.55f, 0.05f), new Vector3(1.05f, 1.55f, 0.92f), Quaternion.Euler(0f, 0f, -20f), stoneMaterial).transform;
        CreatePart("ArmLowerL", PrimitiveType.Sphere, visualRoot, new Vector3(-3.05f, 2.85f, 0.1f), new Vector3(0.95f, 1.62f, 0.88f), Quaternion.Euler(0f, 0f, 8f), darkStoneMaterial);
        leftHand = CreatePart("HandL", PrimitiveType.Sphere, visualRoot, new Vector3(-2.95f, 1.35f, 0.45f), new Vector3(1.0f, 0.72f, 1.04f), Quaternion.Euler(0f, 0f, 8f), stoneMaterial).transform;

        rightArm = CreatePart("ArmUpperR", PrimitiveType.Sphere, visualRoot, new Vector3(2.45f, 4.55f, 0.05f), new Vector3(1.05f, 1.55f, 0.92f), Quaternion.Euler(0f, 0f, 20f), stoneMaterial).transform;
        CreatePart("ArmLowerR", PrimitiveType.Sphere, visualRoot, new Vector3(3.05f, 2.85f, 0.1f), new Vector3(0.95f, 1.62f, 0.88f), Quaternion.Euler(0f, 0f, -8f), darkStoneMaterial);
        rightHand = CreatePart("HandR", PrimitiveType.Sphere, visualRoot, new Vector3(2.95f, 1.35f, 0.45f), new Vector3(1.0f, 0.72f, 1.04f), Quaternion.Euler(0f, 0f, -8f), stoneMaterial).transform;

        leftLeg = CreatePart("LegL", PrimitiveType.Sphere, visualRoot, new Vector3(-0.82f, 1.1f, 0f), new Vector3(0.9f, 1.65f, 0.85f), Quaternion.Euler(0f, 0f, -5f), darkStoneMaterial).transform;
        rightLeg = CreatePart("LegR", PrimitiveType.Sphere, visualRoot, new Vector3(0.82f, 1.1f, 0f), new Vector3(0.9f, 1.65f, 0.85f), Quaternion.Euler(0f, 0f, 5f), darkStoneMaterial).transform;
        CreatePart("FootL", PrimitiveType.Cube, visualRoot, new Vector3(-0.82f, 0.16f, 0.52f), new Vector3(1.15f, 0.34f, 1.25f), Quaternion.Euler(0f, -8f, 0f), stoneMaterial);
        CreatePart("FootR", PrimitiveType.Cube, visualRoot, new Vector3(0.82f, 0.16f, 0.52f), new Vector3(1.15f, 0.34f, 1.25f), Quaternion.Euler(0f, 8f, 0f), stoneMaterial);

        CreateMossPatch("MossHead", new Vector3(-0.2f, 7.08f, 0.28f), new Vector3(0.9f, 0.08f, 0.45f), Quaternion.Euler(16f, 0f, -6f));
        CreateMossPatch("MossShoulderL", new Vector3(-1.62f, 5.5f, -0.22f), new Vector3(0.82f, 0.08f, 0.45f), Quaternion.Euler(12f, 0f, -18f));
        CreateMossPatch("MossChest", new Vector3(0.45f, 5.15f, 1.12f), new Vector3(0.72f, 0.08f, 0.42f), Quaternion.Euler(82f, 0f, 8f));
        CreateRune("RuneChest", new Vector3(0f, 4.72f, 1.48f), new Vector3(0.12f, 0.62f, 0.04f));
        CreateRune("RuneHead", new Vector3(0f, 6.84f, 1.18f), new Vector3(0.08f, 0.34f, 0.04f));
        CreateFloatingShard("ShardL", new Vector3(-2.8f, 6.1f, -0.55f));
        CreateFloatingShard("ShardR", new Vector3(2.8f, 5.8f, -0.25f));
        CreateFloatingShard("ShardBack", new Vector3(0.45f, 6.8f, -1.35f));
    }

    GameObject CreatePart(string partName, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition * visualScale;
        part.transform.localScale = localScale * visualScale;
        part.transform.localRotation = localRotation;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
        }

        return part;
    }

    void CreateEye(string name, Vector3 localPosition)
    {
        GameObject eye = CreatePart(name, PrimitiveType.Sphere, visualRoot, localPosition, new Vector3(0.18f, 0.16f, 0.08f), Quaternion.identity, eyeMaterial);
        rageRunes.Add(eye.transform);
    }

    void CreateMossPatch(string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
    {
        CreatePart(name, PrimitiveType.Cube, visualRoot, localPosition, localScale, localRotation, mossMaterial);
    }

    void CreateRune(string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject rune = CreatePart(name, PrimitiveType.Cube, visualRoot, localPosition, localScale, Quaternion.Euler(0f, 0f, 35f), runeMaterial);
        rageRunes.Add(rune.transform);
    }

    void CreateFloatingShard(string name, Vector3 localPosition)
    {
        GameObject shard = CreatePart(name, PrimitiveType.Cube, visualRoot, localPosition, new Vector3(0.42f, 0.72f, 0.34f), Quaternion.Euler(18f, 25f, 12f), darkStoneMaterial);
        floatingShards.Add(shard.transform);
    }

    void AnimateVisuals()
    {
        float time = Time.time;
        if (headPart != null)
            headPart.localRotation = Quaternion.Euler(-7f + Mathf.Sin(time * 1.4f) * 1.7f, Mathf.Sin(time * 0.7f) * 2f, 0f);

        if (chestPart != null)
            chestPart.localPosition = new Vector3(0f, 4.3f + Mathf.Sin(time * 1.6f) * 0.035f, 0.05f) * visualScale;

        for (int i = 0; i < floatingShards.Count; i++)
        {
            Transform shard = floatingShards[i];
            if (shard == null)
                continue;

            shard.localPosition += Vector3.up * (Mathf.Sin(time * 2.1f + i) * 0.0035f);
            shard.Rotate(Vector3.up, (28f + i * 8f) * Time.deltaTime, Space.Self);
        }

        if (runeMaterial != null)
        {
            float pulse = Mathf.Lerp(0.72f, 1.18f, (Mathf.Sin(time * (phase >= 3 ? 7f : 3.5f)) + 1f) * 0.5f);
            runeMaterial.color = runeBaseColor * pulse;
        }
    }

    void UpdateHitFlash()
    {
        if (hitFlashTimer <= 0f)
            return;

        hitFlashTimer -= Time.deltaTime;
        Color flash = phase >= 3 ? new Color(1f, 0.18f, 0.08f, 1f) : new Color(1f, 0.78f, 0.25f, 1f);
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.material != null)
                renderer.material.color = Color.Lerp(renderer.material.color, flash, 0.32f);
        }
    }

    void SetRuneIntensity(bool intense)
    {
        if (runeMaterial == null)
            return;

        runeBaseColor = intense ? new Color(1f, 0.38f, 0.08f, 1f) : new Color(0.18f, 0.9f, 1f, 1f);
        runeMaterial.color = runeBaseColor;
    }

    void SetRageVisual(bool active)
    {
        if (!active)
            return;

        SetRuneIntensity(true);
        if (eyeMaterial != null)
            eyeMaterial.color = new Color(1f, 0.16f, 0.05f, 1f);

        for (int i = 0; i < rageRunes.Count; i++)
        {
            Transform rune = rageRunes[i];
            if (rune == null)
                continue;

            Renderer renderer = rune.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = rageMaterial;
        }
    }

    void ShakeVisual(float amplitude, float duration)
    {
        StartCoroutine(ShakeVisualRoutine(amplitude, duration));
    }

    IEnumerator ShakeVisualRoutine(float amplitude, float duration)
    {
        if (visualRoot == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            visualRoot.localPosition = UnityEngine.Random.insideUnitSphere * amplitude;
            visualRoot.localPosition = new Vector3(visualRoot.localPosition.x, 0f, visualRoot.localPosition.z);
            yield return null;
        }

        visualRoot.localPosition = Vector3.zero;
    }

    void CreateTelegraph(Vector3 position, float radius, Color color, float lifetime)
    {
        GameObject telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        telegraph.name = "KaelTorTelegraph";
        telegraph.transform.position = ResolveGround(position) + Vector3.up * 0.035f;
        telegraph.transform.localScale = new Vector3(radius * 2f, 0.035f, radius * 2f);

        Collider collider = telegraph.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = telegraph.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial("KaelTorTelegraphRuntime", color);

        Destroy(telegraph, Mathf.Max(0.1f, lifetime));
    }

    void SpawnImpactBurst(Vector3 position, float scale, Color color)
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.name = "KaelTorImpactBurst";
        burst.transform.position = ResolveGround(position) + Vector3.up * 0.18f;
        burst.transform.localScale = Vector3.one * Mathf.Max(0.15f, scale);

        Collider collider = burst.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = burst.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial("KaelTorImpactBurstRuntime", color);

        Destroy(burst, 0.35f);
    }

    void SnapToGround()
    {
        transform.position = ResolveGround(transform.position);
        arenaCenter = transform.position;
    }

    Vector3 ResolveGround(Vector3 position)
    {
        return KaelTorSpawnUtility.TryResolveGround(position, out Vector3 groundedPosition, groundMask)
            ? groundedPosition
            : position;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Material CreateRuntimeMaterial(string materialName, Color color)
    {
        return KaelTorSpawnUtility.CreateRuntimeMaterial(materialName, color);
    }
}

public class KaelTorRockProjectile : MonoBehaviour
{
    int damage;
    float speed;
    float expireTime;
    Vector3 direction;
    PlayerMovement targetPlayer;

    public static void Create(Vector3 spawnPosition, Vector3 targetPosition, int projectileDamage, PlayerMovement player)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "KaelTorThrownRock";
        rock.transform.position = spawnPosition;
        rock.transform.localScale = new Vector3(1.1f, 0.88f, 1f);

        Renderer renderer = rock.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = RuntimeMaterialUtility.Create("KaelTorThrownRockRuntime", new Color(0.28f, 0.22f, 0.16f, 1f));

        KaelTorRockProjectile projectile = rock.AddComponent<KaelTorRockProjectile>();
        projectile.damage = projectileDamage;
        projectile.speed = 18f;
        projectile.expireTime = Time.time + 4.2f;
        projectile.targetPlayer = player;
        Vector3 aim = targetPosition - spawnPosition;
        projectile.direction = aim.sqrMagnitude > 0.01f ? aim.normalized : Vector3.forward;

        Rigidbody rb = rock.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void Update()
    {
        transform.position += direction * (speed * Time.deltaTime);
        transform.Rotate(Vector3.one, 140f * Time.deltaTime, Space.Self);

        if (targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.transform.position + Vector3.up) <= 1.6f)
            HitPlayer();

        if (Time.time >= expireTime)
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        if (collision.collider.GetComponentInParent<KaelTorGuardian>() != null)
            return;

        PlayerMovement player = collision.collider.GetComponentInParent<PlayerMovement>();
        if (player != null)
        {
            targetPlayer = player;
            HitPlayer();
            return;
        }

        Destroy(gameObject);
    }

    void HitPlayer()
    {
        if (targetPlayer != null)
        {
            targetPlayer.TakeDamage(damage);
            targetPlayer.ApplyKnockback(direction, 10f, 0.28f);
            targetPlayer.RegisterBossOrMiniBossCombat(10f);
        }

        Destroy(gameObject);
    }
}

public class FirstArtifactDemoPickup : MonoBehaviour
{
    const float CollectRadius = 2.1f;

    Item itemData;
    Transform visualRoot;
    bool collected;

    void Start()
    {
        itemData = FirstArtifactItemRegistry.GetOrCreate();
        BuildVisual();
    }

    void Update()
    {
        if (collected)
            return;

        if (visualRoot != null)
        {
            visualRoot.Rotate(Vector3.up, 44f * Time.deltaTime, Space.World);
            visualRoot.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.6f) * 0.12f);
        }

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();
        if (player == null || Vector3.Distance(transform.position, player.transform.position) > CollectRadius)
            return;

        Collect(player);
    }

    void Collect(PlayerMovement player)
    {
        collected = true;
        Inventory inventory = player.GetComponent<Inventory>();
        Hotbar hotbar = player.GetComponent<Hotbar>();

        if (inventory != null && itemData != null && inventory.AddItem(itemData.itemName, 1, itemData))
        {
            hotbar?.TryAddInventoryItem(new InventoryItem(itemData.itemName, 1, itemData));
            SceneObjectCache.Find<InventoryUI>(gameObject.scene, true)?.Refresh();
            PickupMessageSystem.Show(itemData.itemName, 1, transform.position + Vector3.up * 0.8f, itemData.icon);
        }

        DebugCommandChat.AddSystemMessage("Primeiro Artefato obtido.");
        DemoEndingOverlay.Show();
        Destroy(gameObject);
    }

    void BuildVisual()
    {
        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);

        Material crystalMaterial = RuntimeMaterialUtility.Create("FirstArtifactRuntime", new Color(1f, 0.76f, 0.18f, 1f));
        Material coreMaterial = RuntimeMaterialUtility.Create("FirstArtifactCoreRuntime", new Color(1f, 0.16f, 0.08f, 1f));

        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "PrimeiroArtefatoCristal";
        crystal.transform.SetParent(visualRoot, false);
        crystal.transform.localScale = new Vector3(0.8f, 1.4f, 0.8f);
        Renderer crystalRenderer = crystal.GetComponent<Renderer>();
        if (crystalRenderer != null)
            crystalRenderer.sharedMaterial = crystalMaterial;
        Collider crystalCollider = crystal.GetComponent<Collider>();
        if (crystalCollider != null)
            Destroy(crystalCollider);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "PrimeiroArtefatoEnergia";
        core.transform.SetParent(visualRoot, false);
        core.transform.localScale = Vector3.one * 0.38f;
        Renderer coreRenderer = core.GetComponent<Renderer>();
        if (coreRenderer != null)
            coreRenderer.sharedMaterial = coreMaterial;
        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null)
            Destroy(coreCollider);
    }
}

public static class DemoEndingOverlay
{
    static Canvas canvas;

    public static void Show()
    {
        if (canvas != null)
            Object.Destroy(canvas.gameObject);

        UIEventSystemUtility.EnsureSingleEventSystem();

        GameObject canvasObject = new GameObject("DemoEndingOverlay");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        Image background = canvasObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.9f);

        GameObject textObject = new GameObject("EndingText");
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.14f, 0.34f);
        textRect.anchorMax = new Vector2(0.86f, 0.78f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.88f, 0.58f, 1f);
        label.text = "Ele esta despertando...\n\nContinua na versao completa.";

        GameObject buttonsObject = new GameObject("EndingActions");
        buttonsObject.transform.SetParent(canvasObject.transform, false);
        RectTransform buttonsRect = buttonsObject.AddComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.32f, 0.16f);
        buttonsRect.anchorMax = new Vector2(0.68f, 0.29f);
        buttonsRect.offsetMin = Vector2.zero;
        buttonsRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = buttonsObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateButton(buttonsObject.transform, "Continuar Jogando", ContinuePlaying, new Color(0.34f, 0.45f, 0.18f, 0.96f));
        CreateButton(buttonsObject.transform, "Sair da Demo", ExitGame, new Color(0.42f, 0.16f, 0.12f, 0.96f));

        Time.timeScale = 0f;
        GameState.IsPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction action, Color color)
    {
        GameObject buttonObject = new GameObject(text);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
        button.colors = colors;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 6f);
        labelRect.offsetMax = new Vector2(-16f, -6f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.93f, 0.72f, 1f);
        label.raycastTarget = false;

        return button;
    }

    static void ContinuePlaying()
    {
        Time.timeScale = 1f;
        GameState.IsPaused = false;
        GameState.LastUiCloseFrame = Time.frameCount;

        if (canvas != null)
            Object.Destroy(canvas.gameObject);

        canvas = null;

        if (!GameState.IsInventoryOpen && !GameState.IsBestiaryOpen && !GameState.IsQuestJournalOpen && !GameState.IsVendorOpen && !GameState.IsCraftingOpen && !GameState.IsDebugChatOpen && !GameState.IsInLobby && !GameState.IsPlayerDead)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    static void ExitGame()
    {
        Time.timeScale = 1f;
        GameState.IsPaused = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}

public static class KaelTorArenaBuilder
{
    const float CleanupRadius = 62f;
    const float ArenaVisualLift = 0.22f;

    public static GameObject Build(Vector3 center)
    {
        ClearArenaResources(center, CleanupRadius);

        GameObject root = new GameObject("Ruinas Antigas de Kael'Tor");
        root.transform.position = center;
        root.AddComponent<KaelTorArenaDecoration>();
        root.AddComponent<KaelTorArenaCleaner>().Configure(center, CleanupRadius);

        Material stone = RuntimeMaterialUtility.Create("KaelTorArenaStoneRuntime", new Color(0.34f, 0.29f, 0.48f, 1f));
        Material darkStone = RuntimeMaterialUtility.Create("KaelTorArenaDarkStoneRuntime", new Color(0.13f, 0.11f, 0.2f, 1f));
        Material crystal = RuntimeMaterialUtility.Create("KaelTorArenaCrystalRuntime", new Color(0.12f, 0.82f, 1f, 1f));
        Material rune = RuntimeMaterialUtility.Create("KaelTorArenaRuneRuntime", new Color(1f, 0.68f, 0.18f, 1f));
        Vector3 visualCenter = center + Vector3.up * ArenaVisualLift;

        CreateCylinder("ArenaRing", root.transform, visualCenter + Vector3.up * 0.02f, new Vector3(37f, 0.04f, 37f), stone);
        CreateCrystal("CristalCentral", root.transform, visualCenter + Vector3.up * 1.6f, crystal);
        AddArenaLight(root.transform, visualCenter + Vector3.up * 3.6f, new Color(0.35f, 0.85f, 1f, 1f));

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            CreateRuneTile($"RunaArena_{i}", root.transform, visualCenter + dir * 12.5f + Vector3.up * 0.09f, Quaternion.Euler(0f, angle, 0f), rune);
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 pos = visualCenter + dir * 17f;
            float height = i % 3 == 0 ? 4.8f : 3.2f;
            GameObject column = CreateCylinder($"ColunaQuebrada_{i}", root.transform, pos + Vector3.up * (height * 0.5f), new Vector3(1.35f, height, 1.35f), i % 2 == 0 ? stone : darkStone);
            column.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-5f, 7f), angle, UnityEngine.Random.Range(-4f, 4f));
        }

        for (int i = 0; i < 18; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(8f, 23f);
            Vector3 pos = visualCenter + new Vector3(circle.x, 0.18f, circle.y);
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = $"PedraRuina_{i}";
            rock.transform.SetParent(root.transform, true);
            rock.transform.position = pos;
            rock.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.55f, 1.45f);
            DisableCollider(rock);
            Renderer renderer = rock.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = i % 2 == 0 ? stone : darkStone;
        }

        WorldMapMarker marker = root.AddComponent<WorldMapMarker>();
        marker.label = "Ruinas de Kael'Tor";
        marker.markerColor = new Color(1f, 0.64f, 0.1f, 0.95f);
        marker.markerSize = 14f;

        return root;
    }

    static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, true);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        DisableCollider(obj);
        return obj;
    }

    static void CreateCrystal(string name, Transform parent, Vector3 position, Material material)
    {
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = name;
        crystal.transform.SetParent(parent, true);
        crystal.transform.position = position;
        crystal.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        crystal.transform.localScale = new Vector3(1.2f, 3.2f, 1.2f);
        Renderer renderer = crystal.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        DisableCollider(crystal);
    }

    static void CreateRuneTile(string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
    {
        GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rune.name = name;
        rune.transform.SetParent(parent, true);
        rune.transform.position = position;
        rune.transform.rotation = rotation * Quaternion.Euler(0f, 45f, 0f);
        rune.transform.localScale = new Vector3(1.05f, 0.055f, 0.26f);
        Renderer renderer = rune.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        DisableCollider(rune);
    }

    static void AddArenaLight(Transform parent, Vector3 position, Color color)
    {
        GameObject lightObject = new GameObject("ArenaCrystalLight");
        lightObject.transform.SetParent(parent, true);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 24f;
        light.intensity = 1.8f;
    }

    static void DisableCollider(GameObject obj)
    {
        Collider collider = obj != null ? obj.GetComponent<Collider>() : null;
        if (collider != null)
            Object.Destroy(collider);
    }

    public static void ClearArenaResources(Vector3 center, float radius)
    {
        float radiusSqr = radius * radius;

        IReadOnlyList<ResourceNode> nodes = ResourceNode.ActiveNodes;
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            ResourceNode node = nodes[i];
            if (node == null)
                continue;

            if ((node.transform.position - center).sqrMagnitude <= radiusSqr)
                Object.Destroy(node.gameObject);
        }

        PickupRespawner[] pickups = Object.FindObjectsByType<PickupRespawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < pickups.Length; i++)
        {
            PickupRespawner pickup = pickups[i];
            if (pickup == null)
                continue;

            if ((pickup.transform.position - center).sqrMagnitude <= radiusSqr)
                Object.Destroy(pickup.gameObject);
        }

        DestroySpawnPointsInArena<CowSpawnPoint>(center, radiusSqr);
        DestroySpawnPointsInArena<WildChickenSpawnPoint>(center, radiusSqr);
        DestroySpawnPointsInArena<WildBoarSpawnPoint>(center, radiusSqr);
        DestroySpawnPointsInArena<EarthGolemSpawnPoint>(center, radiusSqr);
        DestroySpawnPointsInArena<ForestMushroomMonsterSpawnPoint>(center, radiusSqr);
        DestroyTerrainChunkPropsInArena(center, radiusSqr);

        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            GameObject candidate = ResolveProceduralSceneryObject(hit.gameObject);
            if (candidate == null)
                continue;

            if ((candidate.transform.position - center).sqrMagnitude > radiusSqr)
                continue;

            Object.Destroy(candidate);
        }
    }

    static void DestroyTerrainChunkPropsInArena(Vector3 center, float radiusSqr)
    {
        TerrainChunk[] chunks = Object.FindObjectsByType<TerrainChunk>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        HashSet<GameObject> pendingDestroy = new HashSet<GameObject>();

        for (int i = 0; i < chunks.Length; i++)
        {
            TerrainChunk chunk = chunks[i];
            if (chunk == null)
                continue;

            Transform[] children = chunk.GetComponentsInChildren<Transform>(true);
            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                Transform child = children[childIndex];
                if (child == null || child == chunk.transform)
                    continue;

                if ((child.position - center).sqrMagnitude > radiusSqr)
                    continue;

                GameObject candidate = ResolveProceduralSceneryObject(child.gameObject);
                if (candidate == null || pendingDestroy.Contains(candidate))
                    continue;

                pendingDestroy.Add(candidate);
            }
        }

        foreach (GameObject candidate in pendingDestroy)
        {
            if (candidate != null)
                Object.Destroy(candidate);
        }
    }

    static void DestroySpawnPointsInArena<T>(Vector3 center, float radiusSqr) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null)
                continue;

            if ((component.transform.position - center).sqrMagnitude <= radiusSqr)
                Object.Destroy(component.gameObject);
        }
    }

    static GameObject ResolveProceduralSceneryObject(GameObject source)
    {
        if (source == null)
            return null;

        if (source.GetComponentInParent<PlayerMovement>() != null ||
            source.GetComponentInParent<KaelTorGuardian>() != null ||
            source.GetComponentInParent<KaelTorArenaDecoration>() != null ||
            source.GetComponentInParent<AncestralTotem>() != null ||
            source.GetComponentInParent<FloatingPickup>() != null ||
            source.GetComponentInParent<FirstArtifactDemoPickup>() != null ||
            source.GetComponentInParent<Cow>() != null ||
            source.GetComponentInParent<WildChicken>() != null ||
            source.GetComponentInParent<WildBoar>() != null ||
            source.GetComponentInParent<MiniKrug>() != null ||
            source.GetComponentInParent<EarthGolem>() != null ||
            source.GetComponentInParent<BossEnemy>() != null)
            return null;

        TerrainChunk chunk = source.GetComponentInParent<TerrainChunk>();
        if (chunk == null)
            return null;

        Transform current = source.transform;
        while (current.parent != null && current.parent.GetComponent<TerrainChunk>() == null)
            current = current.parent;

        if (current.GetComponent<TerrainChunk>() != null)
            return null;

        string objectName = current.name.ToLowerInvariant();
        if (objectName.Contains("tree") ||
            objectName.Contains("arvore") ||
            objectName.Contains("rock") ||
            objectName.Contains("pedra") ||
            objectName.Contains("bush") ||
            objectName.Contains("arbusto") ||
            objectName.Contains("mushroom") ||
            objectName.Contains("cogumelo") ||
            objectName.Contains("trigo") ||
            current.GetComponentInChildren<ResourceNode>() != null ||
            current.GetComponentInChildren<PickupRespawner>() != null ||
            IsLikelyProceduralScenery(current))
            return current.gameObject;

        return null;
    }

    static bool IsLikelyProceduralScenery(Transform current)
    {
        if (current == null || current.GetComponent<TerrainChunk>() != null)
            return false;

        string objectName = current.name.ToLowerInvariant();
        if (objectName.Contains("terrain") ||
            objectName.Contains("chunk") ||
            objectName.Contains("water") ||
            objectName.Contains("river") ||
            objectName.Contains("road") ||
            objectName.Contains("path"))
            return false;

        Renderer renderer = current.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return false;

        Bounds bounds = renderer.bounds;
        if (bounds.size.x > 45f || bounds.size.y > 45f || bounds.size.z > 45f)
            return false;

        return true;
    }
}

public class KaelTorArenaDecoration : MonoBehaviour
{
}

public class KaelTorArenaCleaner : MonoBehaviour
{
    Vector3 center;
    float radius;
    float nextCleanupTime;
    float stopCleanupTime;

    public void Configure(Vector3 cleanupCenter, float cleanupRadius)
    {
        center = cleanupCenter;
        radius = cleanupRadius;
        nextCleanupTime = Time.time;
        stopCleanupTime = Time.time + 10f;
    }

    void Update()
    {
        if (Time.time > stopCleanupTime)
        {
            enabled = false;
            return;
        }

        if (Time.time < nextCleanupTime)
            return;

        nextCleanupTime = Time.time + 0.45f;
        KaelTorArenaBuilder.ClearArenaResources(center, radius);
    }
}

public class KaelTorDemoWorldSpawner : MonoBehaviour
{
    public float spawnCheckInterval = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<KaelTorDemoWorldSpawner>() != null)
            return;

        new GameObject("KaelTorDemoWorldSpawner").AddComponent<KaelTorDemoWorldSpawner>();
    }

    IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.5f, spawnCheckInterval));

            if (GameState.IsInLobby)
                yield break;

            if (FindFirstObjectByType<KaelTorGuardian>() != null)
                yield break;

            PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();
            if (player == null)
                continue;

            if (!DemoWorldProgression.HasReachedFinalBossRegion(player.transform.position))
                continue;

            if (!KaelTorSpawnUtility.TryFindArenaSpawnPosition(
                player,
                DemoWorldProgression.FinalBossPreferredDistanceFromPlayer,
                DemoWorldProgression.FinalBossMinDistanceFromSpawn,
                out Vector3 center))
            {
                continue;
            }

            KaelTorArenaBuilder.Build(center);

            GameObject bossObject = new GameObject("Kael'Tor, o Vigia dos Artefatos");
            bossObject.transform.SetPositionAndRotation(center + Vector3.up * 0.05f, Quaternion.LookRotation((player.transform.position - center).normalized, Vector3.up));
            bossObject.AddComponent<KaelTorGuardian>();
            yield break;
        }
    }
}

public static class KaelTorSpawnUtility
{
    public static bool TryFindArenaSpawnPosition(PlayerMovement player, float preferredDistance, out Vector3 center)
    {
        return TryFindArenaSpawnPosition(player, preferredDistance, 0f, out center);
    }

    public static bool TryFindArenaSpawnPosition(PlayerMovement player, float preferredDistance, float minDistanceFromAdventureStart, out Vector3 center)
    {
        center = default;
        if (player == null)
            return false;

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();
        float randomYaw = DemoWorldProgression.ResolveArenaSearchAngle(player.transform.position, 0x4B41454C);
        Vector3 seededForward = Quaternion.Euler(0f, randomYaw, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, seededForward).normalized;
        Vector3[] directions =
        {
            seededForward,
            Quaternion.Euler(0f, 34f, 0f) * seededForward,
            Quaternion.Euler(0f, -34f, 0f) * seededForward,
            right,
            -right,
            -seededForward,
            forward,
            -forward
        };

        float[] distances =
        {
            Mathf.Max(18f, preferredDistance),
            Mathf.Max(24f, preferredDistance + 12f),
            Mathf.Max(30f, preferredDistance + 24f)
        };

        for (int d = 0; d < distances.Length; d++)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 candidate = player.transform.position + directions[i] * distances[d];
                if (!TryResolveGround(candidate, out Vector3 grounded, ~0))
                    continue;

                if (minDistanceFromAdventureStart > 0f &&
                    !DemoWorldProgression.IsFarEnoughForFinalBoss(grounded, minDistanceFromAdventureStart))
                {
                    continue;
                }

                if (Mathf.Abs(grounded.y - player.transform.position.y) > 18f)
                    continue;

                center = grounded;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveGround(Vector3 position, out Vector3 groundedPosition, LayerMask groundMask)
    {
        groundedPosition = position;
        Vector3 origin = position + Vector3.up * 80f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 180f, groundMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit))
                continue;

            groundedPosition = hit.point;
            return true;
        }

        return false;
    }

    public static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        return material;
    }

    static bool IsValidGroundHit(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null || hit.normal.y < 0.35f)
            return false;

        if (collider.GetComponentInParent<KaelTorGuardian>() != null ||
            collider.GetComponentInParent<KaelTorArenaDecoration>() != null ||
            collider.GetComponentInParent<PlayerMovement>() != null ||
            collider.GetComponentInParent<RemotePlayerReplica>() != null ||
            collider.GetComponentInParent<FloatingPickup>() != null ||
            collider.GetComponentInParent<FirstArtifactDemoPickup>() != null ||
            collider.GetComponentInParent<WorldMapMarker>() != null ||
            collider.GetComponentInParent<WildBoar>() != null ||
            collider.GetComponentInParent<WildChicken>() != null ||
            collider.GetComponentInParent<Cow>() != null ||
            collider.GetComponentInParent<MiniKrug>() != null ||
            collider.GetComponentInParent<EarthGolem>() != null ||
            collider.GetComponentInParent<BossEnemy>() != null ||
            collider.GetComponentInParent<AncestralTotem>() != null ||
            collider.GetComponentInParent<ResourceNode>() != null)
            return false;

        return true;
    }
}
