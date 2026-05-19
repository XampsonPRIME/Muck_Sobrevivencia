using UnityEngine;

[DisallowMultipleComponent]
public class MiniKrugLegacyAnimationDriver : MonoBehaviour
{
    public Animation animationComponent;
    public string idleClip = "Idle";
    public string runClip = "Run";
    public string attackClip = "Attack";
    public string damageClip = "Damage";
    public string deathClip = "Death";
    public float crossFadeDuration = 0.12f;
    public float locomotionThreshold = 0.06f;
    public float attackLockDuration = 0.45f;
    public float damageLockDuration = 0.3f;
    public float deathHoldDuration = 1.15f;
    public bool autoConfigureWrapModes = true;
    public float maxActiveDistance = 18f;
    public float cullingCheckInterval = 0.2f;

    float actionLockUntil;
    float locomotionLockUntil;
    float nextCullingCheckTime;
    bool hasLastPosition;
    bool canAnimate = true;
    bool isDead;
    string currentClip;
    Vector3 lastPosition;

    public float DeathDuration => GetClipDuration(deathClip, deathHoldDuration);

    void Awake()
    {
        ResolveAnimation();
        ConfigureWrapModes();
        lastPosition = transform.position;
        hasLastPosition = true;
    }

    void Update()
    {
        if (isDead)
            return;

        ResolveAnimation();
        if (animationComponent == null)
            return;

        UpdateAnimationCulling();
        if (!canAnimate)
            return;

        if (!hasLastPosition)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
        }

        float movedDistanceSqr = (transform.position - lastPosition).sqrMagnitude;
        lastPosition = transform.position;

        if (Time.time < actionLockUntil)
            return;

        bool shouldMove = movedDistanceSqr > locomotionThreshold * locomotionThreshold || Time.time < locomotionLockUntil;
        PlayClip(shouldMove ? runClip : idleClip);
    }

    public void PlayIdle()
    {
        if (isDead)
            return;

        ResolveAnimation();
        PlayClip(idleClip);
    }

    public void PlayAttack()
    {
        if (isDead)
            return;

        PlayLockedClip(attackClip, attackLockDuration);
    }

    public void PlayMoveFor(float duration)
    {
        if (isDead)
            return;

        locomotionLockUntil = Mathf.Max(locomotionLockUntil, Time.time + Mathf.Max(0f, duration));
        PlayClip(runClip);
    }

    public void PlayDamage()
    {
        if (isDead)
            return;

        PlayLockedClip(damageClip, damageLockDuration);
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;
        ResolveAnimation();
        PlayClip(deathClip);
    }

    public void SetClipConfiguration(
        Animation animationOverride,
        string idle,
        string run,
        string attack,
        string damage,
        string death)
    {
        animationComponent = animationOverride;
        idleClip = idle;
        runClip = run;
        attackClip = attack;
        damageClip = damage;
        deathClip = death;
        isDead = false;
        currentClip = null;
        actionLockUntil = 0f;
        lastPosition = transform.position;
        hasLastPosition = true;
        ConfigureWrapModes();
    }

    void ResolveAnimation()
    {
        if (animationComponent == null)
        {
            animationComponent = GetComponent<Animation>() ?? GetComponentInChildren<Animation>(true);

            if (animationComponent != null)
                animationComponent.cullingType = AnimationCullingType.BasedOnRenderers;
        }
    }

    void UpdateAnimationCulling()
    {
        if (Time.time < nextCullingCheckTime)
            return;

        nextCullingCheckTime = Time.time + Mathf.Max(0.05f, cullingCheckInterval);
        bool shouldAnimate = ShouldAnimate();
        if (canAnimate == shouldAnimate)
            return;

        canAnimate = shouldAnimate;
        animationComponent.enabled = shouldAnimate;

        if (shouldAnimate)
        {
            currentClip = null;
            hasLastPosition = false;
        }
    }

    bool ShouldAnimate()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return true;

        float maxDistance = Mathf.Max(1f, maxActiveDistance);
        return (transform.position - activeCamera.transform.position).sqrMagnitude <= maxDistance * maxDistance;
    }

    void ConfigureWrapModes()
    {
        if (!autoConfigureWrapModes)
            return;

        ResolveAnimation();
        if (animationComponent == null)
            return;

        SetWrapMode(idleClip, WrapMode.Loop);
        SetWrapMode(runClip, WrapMode.Loop);
        SetWrapMode(attackClip, WrapMode.Once);
        SetWrapMode(damageClip, WrapMode.Once);
        SetWrapMode(deathClip, WrapMode.ClampForever);
    }

    void SetWrapMode(string clipName, WrapMode wrapMode)
    {
        string resolvedClipName = ResolveClipName(clipName);
        if (string.IsNullOrWhiteSpace(resolvedClipName))
            return;

        AnimationState state = animationComponent[resolvedClipName];
        if (state != null)
            state.wrapMode = wrapMode;
    }

    void PlayLockedClip(string clipName, float fallbackDuration)
    {
        ResolveAnimation();
        string resolvedClipName = ResolveClipName(clipName);
        if (animationComponent == null || string.IsNullOrWhiteSpace(resolvedClipName))
            return;

        actionLockUntil = Time.time + GetClipDuration(resolvedClipName, fallbackDuration);
        PlayClip(resolvedClipName);
    }

    void PlayClip(string clipName)
    {
        string resolvedClipName = ResolveClipName(clipName);
        if (animationComponent == null || string.IsNullOrWhiteSpace(resolvedClipName))
            return;

        UpdateAnimationCulling();
        if (!canAnimate)
            return;

        if (animationComponent[resolvedClipName] == null)
            return;

        if (currentClip == resolvedClipName && animationComponent.IsPlaying(resolvedClipName))
            return;

        currentClip = resolvedClipName;
        animationComponent.CrossFade(resolvedClipName, crossFadeDuration);
    }

    float GetClipDuration(string clipName, float fallbackDuration)
    {
        ResolveAnimation();
        string resolvedClipName = ResolveClipName(clipName);
        if (animationComponent == null || string.IsNullOrWhiteSpace(resolvedClipName))
            return fallbackDuration;

        AnimationState state = animationComponent[resolvedClipName];
        if (state == null || state.length <= 0f)
            return fallbackDuration;

        return state.length;
    }

    string ResolveClipName(string configuredClipName)
    {
        ResolveAnimation();
        if (animationComponent == null || string.IsNullOrWhiteSpace(configuredClipName))
            return configuredClipName;

        if (animationComponent[configuredClipName] != null)
            return configuredClipName;

        int separatorIndex = configuredClipName.LastIndexOf('|');
        string shortName = separatorIndex >= 0 && separatorIndex < configuredClipName.Length - 1
            ? configuredClipName.Substring(separatorIndex + 1)
            : configuredClipName;

        if (animationComponent[shortName] != null)
            return shortName;

        foreach (AnimationState state in animationComponent)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.name))
                continue;

            if (state.name == configuredClipName || state.name == shortName)
                return state.name;

            if (state.name.EndsWith($"|{shortName}", System.StringComparison.OrdinalIgnoreCase))
                return state.name;
        }

        return configuredClipName;
    }
}
