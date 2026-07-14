using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(BearerPowerService))]
public class BearerPowerController : MonoBehaviour
{
    public const float TransformationDuration = 15f;

    PlayerMovement movement;
    PlayerProgression progression;
    BearerPowerService powerService;
    BearerPowerHUD hud;
    BearerWolfCompanion wolf;
    InputAction primaryAction;
    InputAction secondaryAction;
    InputAction utilityAction;
    InputAction transformationAction;
    readonly Dictionary<string, float> cooldownEnds = new Dictionary<string, float>();
    float transformationEndTime;
    float nextTransformationPulseTime;
    GameObject transformationVisual;
    Coroutine leapRoutine;
    int lastKnownLevel = -1;

    public int CurrentLevel => progression != null ? progression.currentLevel : 1;
    public bool IsTransformed => Time.time < transformationEndTime;
    public float MovementSpeedMultiplier
    {
        get
        {
            if (!IsTransformed || powerService == null)
                return 1f;

            return powerService.CurrentDefinition?.id switch
            {
                BearerPowerId.FlameHeir => 1.2f,
                BearerPowerId.VoidWalker => 1.4f,
                BearerPowerId.WildSummoner => 1.22f,
                BearerPowerId.EarthTitan => 0.92f,
                _ => 1f
            };
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player != null && player.GetComponent<BearerPowerController>() == null)
            player.gameObject.AddComponent<BearerPowerController>();
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        progression = GetComponent<PlayerProgression>() ?? gameObject.AddComponent<PlayerProgression>();
        powerService = GetComponent<BearerPowerService>() ?? gameObject.AddComponent<BearerPowerService>();
        primaryAction = new InputAction("BearerPrimary", binding: "<Keyboard>/q");
        secondaryAction = new InputAction("BearerSecondary", binding: "<Keyboard>/r");
        utilityAction = new InputAction("BearerUtility", binding: "<Keyboard>/f");
        transformationAction = new InputAction("BearerTransformation", binding: "<Keyboard>/v");
    }

    void OnEnable()
    {
        primaryAction.Enable();
        secondaryAction.Enable();
        utilityAction.Enable();
        transformationAction.Enable();
        BearerPowerService.PowerChanged += HandlePowerChanged;
    }

    void OnDisable()
    {
        BearerShadowClone.DestroyAllForOwner(movement);
        BearerPowerService.PowerChanged -= HandlePowerChanged;
        transformationAction.Disable();
        utilityAction.Disable();
        secondaryAction.Disable();
        primaryAction.Disable();
    }

    void Start()
    {
        lastKnownLevel = CurrentLevel;
        EnsureHud();
    }

    void Update()
    {
        ResolveReferences();
        EnsureHud();
        CheckAbilityUnlocks();

        if (GameState.IsPlayerDead && IsTransformed)
            CancelTransformation();

        UpdateTransformation();
        hud?.Refresh();

        if (!CanCast())
            return;

        if (primaryAction.WasPressedThisFrame())
            TryUseSlot(BearerAbilitySlot.Primary);
        else if (secondaryAction.WasPressedThisFrame())
            TryUseSlot(BearerAbilitySlot.Secondary);
        else if (utilityAction.WasPressedThisFrame())
            TryUseSlot(BearerAbilitySlot.Utility);
        else if (transformationAction.WasPressedThisFrame())
            TryUseSlot(BearerAbilitySlot.Transformation);
    }

    public float ModifyIncomingDamage(float amount)
    {
        if (!IsTransformed || powerService?.CurrentDefinition == null)
            return amount;

        return powerService.CurrentDefinition.id switch
        {
            BearerPowerId.VoidWalker => amount * 0.7f,
            BearerPowerId.EarthTitan => amount * 0.45f,
            BearerPowerId.FlameHeir => amount * 0.85f,
            BearerPowerId.WildSummoner => amount * 0.8f,
            _ => amount
        };
    }

    public BearerPowerAbilityDefinition GetAbility(BearerAbilitySlot slot)
    {
        BearerPowerDefinition definition = powerService?.CurrentDefinition;
        if (definition?.abilities == null)
            return null;

        for (int i = 0; i < definition.abilities.Count; i++)
        {
            BearerPowerAbilityDefinition ability = definition.abilities[i];
            if (ability != null && ability.slot == slot)
                return ability;
        }

        if (slot == BearerAbilitySlot.Utility)
        {
            for (int i = 0; i < definition.abilities.Count; i++)
            {
                BearerPowerAbilityDefinition ability = definition.abilities[i];
                if (ability != null && ability.slot == BearerAbilitySlot.Passive)
                    return ability;
            }
        }

        return null;
    }

    public float GetCooldownRemaining(BearerPowerAbilityDefinition ability)
    {
        if (ability == null || !cooldownEnds.TryGetValue(ability.stableId, out float endTime))
            return 0f;

        return Mathf.Max(0f, endTime - Time.time);
    }

    public float GetTransformationRemaining()
    {
        return Mathf.Max(0f, transformationEndTime - Time.time);
    }

    void ResolveReferences()
    {
        movement ??= GetComponent<PlayerMovement>();
        progression ??= GetComponent<PlayerProgression>();
        powerService ??= GetComponent<BearerPowerService>();
    }

    bool CanCast()
    {
        return powerService != null &&
               powerService.HasPower &&
               !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPowerSelectionOpen &&
               !GameState.IsPlayerDead &&
               !GameState.IsPaused &&
               !GameState.IsInventoryOpen &&
               !GameState.IsBestiaryOpen &&
               !GameState.IsQuestJournalOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen &&
               !GameState.IsDebugChatOpen &&
               !GameState.IsDemoGuideOpen;
    }

    void TryUseSlot(BearerAbilitySlot slot)
    {
        BearerPowerAbilityDefinition ability = GetAbility(slot);
        if (ability == null)
            return;

        if (ability.slot == BearerAbilitySlot.Passive)
        {
            bool unlocked = CurrentLevel >= ability.unlockLevel;
            if (unlocked &&
                slot == BearerAbilitySlot.Utility &&
                powerService?.CurrentDefinition?.id == BearerPowerId.FlameHeir)
            {
                TriggerPlayerAnimation(PlayerAnimationBridge.Power3Trigger);
            }

            MessageSystem.Instance?.ShowMessage(
                unlocked
                    ? $"{ability.displayName} esta ativo."
                    : $"{ability.displayName} desbloqueia no nivel {ability.unlockLevel}.");
            return;
        }

        if (CurrentLevel < ability.unlockLevel)
        {
            MessageSystem.Instance?.ShowMessage($"{ability.displayName} desbloqueia no nivel {ability.unlockLevel}.");
            return;
        }

        float remaining = GetCooldownRemaining(ability);
        if (remaining > 0.01f)
        {
            MessageSystem.Instance?.ShowMessage($"{ability.displayName}: {remaining:0.0}s.");
            return;
        }

        if (movement.currentStamina < ability.staminaCost)
        {
            MessageSystem.Instance?.ShowMessage("Stamina insuficiente.");
            return;
        }

        if (!ExecuteAbility(ability))
            return;

        movement.currentStamina = Mathf.Max(0f, movement.currentStamina - ability.staminaCost);
        cooldownEnds[ability.stableId] = Time.time + ability.cooldown;
        hud?.Refresh();
    }

    bool ExecuteAbility(BearerPowerAbilityDefinition ability)
    {
        return ability.stableId switch
        {
            "fireball" => CastFireball(),
            "flame_burst" => CastFlameBurst(),
            "void_step" => CastVoidStep(),
            "void_rift" => CastVoidRift(),
            "shadow_clone" => CastShadowClone(),
            "summon_wolf" => CastSummonWolf(),
            "entangling_roots" => CastRoots(),
            "forest_spirit" => CastForestSpirit(),
            "seismic_punch" => CastSeismicPunch(),
            "stone_wall" => CastStoneWall(),
            "titanic_leap" => CastTitanicLeap(),
            "igneous_form" or "void_form" or "wild_form" or "titan_form" => StartTransformation(),
            _ => false
        };
    }

    void TriggerPlayerAnimation(string triggerName)
    {
        if (movement != null && movement.VisualAnimator != null)
        {
            PlayerAnimationBridge.Trigger(movement.VisualAnimator, triggerName);
            return;
        }

        PlayerAnimationBridge.Trigger(this, triggerName);
    }

    bool CastFireball()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power1Trigger);
        Transform aim = ResolveAimTransform();
        Vector3 direction = aim != null ? aim.forward : transform.forward;
        Vector3 origin = aim != null ? aim.position + direction * 1.05f : transform.position + Vector3.up * 1.4f + direction;
        bool empowered = CurrentLevel >= 10;
        int damage = ScaleDamage(empowered ? 48 : 34);
        float radius = empowered ? 4.1f : 3.25f;
        BearerPowerProjectile.Spawn(movement, origin, direction, 24f, 38f, radius, damage, new Color(1f, 0.18f, 0.02f, 1f));
        return true;
    }

    bool CastFlameBurst()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power2Trigger);
        Vector3 center = transform.position + transform.forward * 0.7f;
        int damage = ScaleDamage(CurrentLevel >= 10 ? 34 : 24);
        BearerPowerCombatUtility.SpawnPulse(center, new Color(1f, 0.22f, 0.02f, 1f), 5.2f);
        BearerPowerCombatUtility.DamageRadius(center, 5.2f, movement, damage, 10f);
        return true;
    }

    bool CastVoidStep()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power1Trigger);

        Vector3 direction = ResolveFlatAimDirection();
        Vector3 start = transform.position + Vector3.up * 0.9f;
        float distance = 10f;
        RaycastHit[] hits = Physics.SphereCastAll(start + direction * 0.8f, 0.4f, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        float nearestDistance = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform))
                continue;
            nearestDistance = Mathf.Min(nearestDistance, hits[i].distance);
        }

        Vector3 candidate = transform.position + direction * Mathf.Max(1f, nearestDistance - 0.75f);
        if (Physics.Raycast(candidate + Vector3.up * 18f, Vector3.down, out RaycastHit groundHit, 40f, ~0, QueryTriggerInteraction.Ignore))
            candidate.y = groundHit.point.y + 0.08f;

        BearerPowerCombatUtility.SpawnPulse(transform.position, new Color(0.48f, 0.05f, 0.85f, 1f), 1.5f, 0.3f);
        movement.TeleportExact(candidate, transform.rotation, false);
        BearerPowerCombatUtility.SpawnPulse(transform.position, new Color(0.72f, 0.2f, 1f, 1f), 2.1f, 0.35f);
        return true;
    }

    bool CastVoidRift()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power2Trigger);

        Vector3 center = ResolveAimPoint(14f);
        BearerPowerCombatUtility.SpawnPulse(center, new Color(0.45f, 0.03f, 0.72f, 1f), 6f, 1.15f);
        List<Component> targets = BearerPowerCombatUtility.FindTargets(center, 6f);
        for (int i = 0; i < targets.Count; i++)
        {
            BearerPowerCombatUtility.ApplyDamage(targets[i], movement, ScaleDamage(12));
            BearerPowerCombatUtility.ApplyPull(targets[i], center, 1.35f, 12f);
        }
        return true;
    }

    bool CastShadowClone()
    {
        bool spawned = BearerShadowClone.Spawn(movement, IsTransformed ? 18f : 12f) != null;
        if (spawned)
            TriggerPlayerAnimation(PlayerAnimationBridge.Power3Trigger);
        return spawned;
    }

    bool CastSummonWolf()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power1Trigger);

        if (wolf == null)
        {
            wolf = BearerWolfCompanion.Spawn(movement);
            MessageSystem.Instance?.ShowMessage("Um lobo respondeu ao chamado.");
        }
        else
        {
            wolf.Recall();
            MessageSystem.Instance?.ShowMessage("O lobo retornou ao portador.");
        }
        return true;
    }

    bool CastRoots()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power2Trigger);

        Vector3 center = ResolveAimPoint(12f);
        BearerPowerCombatUtility.SpawnPulse(center, new Color(0.22f, 0.8f, 0.12f, 1f), 5f, 0.8f);
        List<Component> targets = BearerPowerCombatUtility.FindTargets(center, 5f);
        for (int i = 0; i < targets.Count; i++)
        {
            BearerPowerCombatUtility.ApplyDamage(targets[i], movement, ScaleDamage(10));
            BearerPowerCombatUtility.ApplyRoot(targets[i], IsTransformed ? 7f : 5f);
        }
        return true;
    }

    bool CastForestSpirit()
    {
        if (wolf == null)
        {
            MessageSystem.Instance?.ShowMessage("Invoque o lobo antes de fortalece-lo.");
            return false;
        }

        TriggerPlayerAnimation(PlayerAnimationBridge.Power3Trigger);
        wolf.ActivateSpiritBuff(IsTransformed ? 28f : 20f);
        MessageSystem.Instance?.ShowMessage("O Espirito da Floresta fortaleceu seu lobo.");
        return true;
    }

    bool CastSeismicPunch()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.Power1Trigger);
        Vector3 center = transform.position + ResolveFlatAimDirection() * 2.1f;
        BearerPowerCombatUtility.SpawnPulse(center, new Color(0.18f, 0.85f, 0.92f, 1f), 4.6f, 0.65f);
        BearerPowerCombatUtility.DamageRadius(center, 4.6f, movement, ScaleDamage(38), 8f);
        return true;
    }

    bool CastStoneWall()
    {
        TriggerPlayerAnimation(PlayerAnimationBridge.PowerStoneWallTrigger);
        Vector3 direction = ResolveFlatAimDirection();
        Vector3 center = transform.position + direction * 3.2f;
        if (Physics.Raycast(center + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 24f, ~0, QueryTriggerInteraction.Ignore))
            center.y = hit.point.y;
        BearerStoneWall.Spawn(center, direction, new Color(0.16f, 0.8f, 0.9f, 1f), IsTransformed ? 18f : 12f);
        return true;
    }

    bool CastTitanicLeap()
    {
        if (leapRoutine != null)
            return false;

        TriggerPlayerAnimation(PlayerAnimationBridge.PowerDiveTrigger);
        leapRoutine = StartCoroutine(TitanicLeapRoutine());
        return true;
    }

    IEnumerator TitanicLeapRoutine()
    {
        movement.ApplyStun(0.9f);
        Vector3 direction = ResolveFlatAimDirection();
        Vector3 start = transform.position;
        Vector3 end = start + direction * 8f;
        if (Physics.Raycast(end + Vector3.up * 18f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            end.y = hit.point.y + 0.08f;

        const float duration = 0.78f;
        float elapsed = 0f;
        while (elapsed < duration && !GameState.IsPlayerDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 desired = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 4.8f);
            movement.MoveByAbility(desired - transform.position);
            yield return null;
        }

        Vector3 center = transform.position;
        BearerPowerCombatUtility.SpawnPulse(center, new Color(0.15f, 0.78f, 0.88f, 1f), 6f, 0.8f);
        BearerPowerCombatUtility.DamageRadius(center, 6f, movement, ScaleDamage(52), 11f);
        leapRoutine = null;
    }

    bool StartTransformation()
    {
        transformationEndTime = Time.time + TransformationDuration;
        nextTransformationPulseTime = 0f;
        BuildTransformationVisual();
        BearerPowerDefinition definition = powerService.CurrentDefinition;
        MessageSystem.Instance?.ShowMessage($"{definition.displayName}: poder intensificado.");
        if (definition.id == BearerPowerId.WildSummoner && wolf != null)
            wolf.ActivateSpiritBuff(TransformationDuration);
        return true;
    }

    void UpdateTransformation()
    {
        if (!IsTransformed)
        {
            if (transformationVisual != null)
                Destroy(transformationVisual);
            return;
        }

        if (transformationVisual != null && transformationVisual.GetComponent<MeshyHeroRuntimeVisual>() == null)
            transformationVisual.transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.Self);

        if (Time.time < nextTransformationPulseTime || powerService?.CurrentDefinition == null)
            return;

        nextTransformationPulseTime = Time.time + 1f;
        switch (powerService.CurrentDefinition.id)
        {
            case BearerPowerId.FlameHeir:
                BearerPowerCombatUtility.DamageRadius(transform.position, 3.1f, movement, ScaleDamage(8));
                break;
            case BearerPowerId.WildSummoner:
                movement.Heal(4f);
                break;
        }
    }

    int ScaleDamage(int baseDamage)
    {
        float multiplier = 1f;
        if (powerService?.CurrentDefinition?.id == BearerPowerId.FlameHeir && CurrentLevel >= 10)
            multiplier *= 1.22f;
        if (IsTransformed)
            multiplier *= powerService.CurrentDefinition.id == BearerPowerId.EarthTitan ? 1.5f : 1.35f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
    }

    Vector3 ResolveAimPoint(float maxDistance)
    {
        Transform aim = ResolveAimTransform();
        Vector3 origin = aim != null ? aim.position : transform.position + Vector3.up * 1.4f;
        Vector3 direction = aim != null ? aim.forward : transform.forward;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return origin + direction * maxDistance;
    }

    Vector3 ResolveFlatAimDirection()
    {
        Transform aim = ResolveAimTransform();
        Vector3 direction = aim != null ? aim.forward : transform.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    Transform ResolveAimTransform()
    {
        if (movement != null && movement.cameraHolder != null)
            return movement.cameraHolder;
        return RuntimeCameraCache.Main != null ? RuntimeCameraCache.Main.transform : transform;
    }

    void BuildTransformationVisual()
    {
        if (transformationVisual != null)
            Destroy(transformationVisual);

        BearerPowerDefinition definition = powerService.CurrentDefinition;
        transformationVisual = new GameObject("BearerTransformationVisual");
        transformationVisual.transform.SetParent(transform, false);
        transformationVisual.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        Material material = CreateEmissiveMaterial(definition.secondaryColor);

        for (int i = 0; i < 4; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = $"TransformationShard_{i}";
            shard.transform.SetParent(transformationVisual.transform, false);
            float angle = i / 4f * Mathf.PI * 2f;
            shard.transform.localPosition = new Vector3(Mathf.Sin(angle) * 0.9f, (i % 2) * 0.45f, Mathf.Cos(angle) * 0.9f);
            shard.transform.localRotation = Quaternion.Euler(25f, -angle * Mathf.Rad2Deg, 45f);
            shard.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            Renderer renderer = shard.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            Collider collider = shard.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        BearerPowerCombatUtility.SpawnPulse(transform.position, definition.secondaryColor, 4f, 0.7f);
    }

    bool BuildWildSummonerTransformationVisual(BearerPowerDefinition definition)
    {
        transformationVisual = new GameObject("WildSummonerTransformationVisual");
        transformationVisual.transform.SetParent(transform, false);
        transformationVisual.transform.localPosition = Vector3.zero;
        transformationVisual.transform.localRotation = Quaternion.identity;

        MeshyHeroRuntimeVisual invokerVisual = transformationVisual.AddComponent<MeshyHeroRuntimeVisual>();
        invokerVisual.characterPrefabPath = "Characters/SavageElfInvoker/Meshy_AI_Savage_Elf_Invoker_biped/Meshy_AI_Savage_Elf_Invoker_biped_Character_output";
        invokerVisual.materialPath = "Characters/SavageElfInvoker/SavageElfInvoker_Material";
        invokerVisual.controllerPath = "Characters/SavageElfInvoker/SavageElfInvoker";
        invokerVisual.visualInstanceName = "SavageElfInvokerVisual";
        invokerVisual.visualLocalPosition = Vector3.zero;
        invokerVisual.visualLocalEuler = Vector3.zero;
        invokerVisual.visualLocalScale = Vector3.one;
        invokerVisual.targetWorldHeight = 2.12f;
        invokerVisual.pauseAllAnimations = false;
        invokerVisual.disableAnimatorWhenPaused = false;
        invokerVisual.useDedicatedTitanController = true;
        invokerVisual.useRuntimeOverrideFallback = false;
        invokerVisual.maxSkinQuality = SkinQuality.Bone2;

        if (!invokerVisual.ActivateTitan(movement))
        {
            Destroy(transformationVisual);
            transformationVisual = null;
            return false;
        }

        BearerPowerCombatUtility.SpawnPulse(transform.position, definition.secondaryColor, 4.4f, 0.8f);
        return true;
    }

    bool BuildFlameHeirTransformationVisual(BearerPowerDefinition definition)
    {
        transformationVisual = new GameObject("FlameHeirTransformationVisual");
        transformationVisual.transform.SetParent(transform, false);
        transformationVisual.transform.localPosition = Vector3.zero;
        transformationVisual.transform.localRotation = Quaternion.identity;

        MeshyHeroRuntimeVisual flameVisual = transformationVisual.AddComponent<MeshyHeroRuntimeVisual>();
        flameVisual.characterPrefabPath = "Characters/FlameHeir/Meshy_AI_Savage_Elf_Invoker_biped/Meshy_AI_Savage_Elf_Invoker_biped_Character_output";
        flameVisual.materialPath = "Characters/FlameHeir/FlameHeir_Material";
        flameVisual.controllerPath = "Characters/FlameHeir/FlameHeir";
        flameVisual.visualInstanceName = "FlameHeirVisual";
        flameVisual.visualLocalPosition = Vector3.zero;
        flameVisual.visualLocalEuler = Vector3.zero;
        flameVisual.visualLocalScale = Vector3.one;
        flameVisual.targetWorldHeight = 2.12f;
        flameVisual.pauseAllAnimations = false;
        flameVisual.disableAnimatorWhenPaused = false;
        flameVisual.useDedicatedTitanController = true;
        flameVisual.useRuntimeOverrideFallback = false;
        flameVisual.maxSkinQuality = SkinQuality.Bone2;

        if (!flameVisual.ActivateTitan(movement))
        {
            Destroy(transformationVisual);
            transformationVisual = null;
            return false;
        }

        BearerPowerCombatUtility.SpawnPulse(transform.position, definition.secondaryColor, 4.5f, 0.8f);
        return true;
    }

    bool BuildVoidWalkerTransformationVisual(BearerPowerDefinition definition)
    {
        transformationVisual = new GameObject("VoidWalkerTransformationVisual");
        transformationVisual.transform.SetParent(transform, false);
        transformationVisual.transform.localPosition = Vector3.zero;
        transformationVisual.transform.localRotation = Quaternion.identity;

        MeshyHeroRuntimeVisual voidVisual = transformationVisual.AddComponent<MeshyHeroRuntimeVisual>();
        voidVisual.characterPrefabPath = "Characters/VoidWalker/Meshy_AI_Savage_Elf_Invoker_biped/Meshy_AI_Savage_Elf_Invoker_biped_Character_output";
        voidVisual.materialPath = "Characters/VoidWalker/VoidWalker_Material";
        voidVisual.controllerPath = "Characters/VoidWalker/VoidWalker";
        voidVisual.visualInstanceName = "VoidWalkerVisual";
        voidVisual.visualLocalPosition = Vector3.zero;
        voidVisual.visualLocalEuler = Vector3.zero;
        voidVisual.visualLocalScale = Vector3.one;
        voidVisual.targetWorldHeight = 2.12f;
        voidVisual.pauseAllAnimations = false;
        voidVisual.disableAnimatorWhenPaused = false;
        voidVisual.useDedicatedTitanController = true;
        voidVisual.useRuntimeOverrideFallback = false;
        voidVisual.maxSkinQuality = SkinQuality.Bone2;

        if (!voidVisual.ActivateTitan(movement))
        {
            Destroy(transformationVisual);
            transformationVisual = null;
            return false;
        }

        BearerPowerCombatUtility.SpawnPulse(transform.position, definition.secondaryColor, 4.5f, 0.8f);
        return true;
    }

    bool BuildEarthTitanTransformationVisual(BearerPowerDefinition definition)
    {
        transformationVisual = new GameObject("EarthTitanTransformationVisual");
        transformationVisual.transform.SetParent(transform, false);
        transformationVisual.transform.localPosition = Vector3.zero;
        transformationVisual.transform.localRotation = Quaternion.identity;

        MeshyHeroRuntimeVisual titanVisual = transformationVisual.AddComponent<MeshyHeroRuntimeVisual>();
        titanVisual.visualLocalPosition = Vector3.zero;
        titanVisual.visualLocalEuler = Vector3.zero;
        titanVisual.visualLocalScale = Vector3.one * 1.12f;
        titanVisual.pauseAllAnimations = false;
        titanVisual.disableAnimatorWhenPaused = false;
        titanVisual.useDedicatedTitanController = true;
        titanVisual.maxSkinQuality = SkinQuality.Bone2;

        if (!titanVisual.ActivateTitan(movement))
        {
            Destroy(transformationVisual);
            transformationVisual = null;
            return false;
        }

        BearerPowerCombatUtility.SpawnPulse(transform.position, definition.secondaryColor, 4.6f, 0.8f);
        return true;
    }

    static Material CreateEmissiveMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "BearerTransformationMaterial",
            color = color
        };
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.5f);
        }
        return material;
    }

    void HandlePowerChanged(BearerPowerService changedService)
    {
        if (changedService != powerService)
            return;

        cooldownEnds.Clear();
        CancelTransformation();
        BearerShadowClone.DestroyAllForOwner(movement);
        if (wolf != null && changedService.CurrentDefinition?.id != BearerPowerId.WildSummoner)
        {
            Destroy(wolf.gameObject);
            wolf = null;
        }
        hud?.Refresh();
    }

    void CheckAbilityUnlocks()
    {
        int level = CurrentLevel;
        if (lastKnownLevel < 0)
        {
            lastKnownLevel = level;
            return;
        }

        if (level <= lastKnownLevel)
        {
            lastKnownLevel = level;
            return;
        }

        BearerPowerDefinition definition = powerService?.CurrentDefinition;
        if (definition?.abilities != null)
        {
            for (int i = 0; i < definition.abilities.Count; i++)
            {
                BearerPowerAbilityDefinition ability = definition.abilities[i];
                if (ability == null || ability.unlockLevel <= lastKnownLevel || ability.unlockLevel > level)
                    continue;

                string key = GetSlotKey(ability.slot);
                string suffix = string.IsNullOrWhiteSpace(key) ? string.Empty : $" [{key}]";
                MessageSystem.Instance?.ShowMessage($"Nova habilidade: {ability.displayName}{suffix}");
            }
        }

        lastKnownLevel = level;
    }

    static string GetSlotKey(BearerAbilitySlot slot)
    {
        return slot switch
        {
            BearerAbilitySlot.Primary => "Q",
            BearerAbilitySlot.Secondary => "R",
            BearerAbilitySlot.Utility => "F",
            BearerAbilitySlot.Transformation => "V",
            _ => string.Empty
        };
    }

    void CancelTransformation()
    {
        transformationEndTime = 0f;
        nextTransformationPulseTime = 0f;
        if (transformationVisual != null)
        {
            Destroy(transformationVisual);
            transformationVisual = null;
        }
    }

    void EnsureHud()
    {
        if (hud != null)
            return;

        GameObject hudObject = new GameObject("BearerPowerHUD");
        hudObject.transform.SetParent(transform, false);
        hud = hudObject.AddComponent<BearerPowerHUD>();
        hud.Configure(this);
    }
}
