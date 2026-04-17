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

    float actionLockUntil;
    bool hasLastPosition;
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

        if (!hasLastPosition)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
        }

        float movedDistanceSqr = (transform.position - lastPosition).sqrMagnitude;
        lastPosition = transform.position;

        if (Time.time < actionLockUntil)
            return;

        PlayClip(movedDistanceSqr > locomotionThreshold * locomotionThreshold ? runClip : idleClip);
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

    void ResolveAnimation()
    {
        if (animationComponent == null)
            animationComponent = GetComponent<Animation>() ?? GetComponentInChildren<Animation>(true);
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
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        AnimationState state = animationComponent[clipName];
        if (state != null)
            state.wrapMode = wrapMode;
    }

    void PlayLockedClip(string clipName, float fallbackDuration)
    {
        ResolveAnimation();
        if (animationComponent == null || string.IsNullOrWhiteSpace(clipName))
            return;

        actionLockUntil = Time.time + GetClipDuration(clipName, fallbackDuration);
        PlayClip(clipName);
    }

    void PlayClip(string clipName)
    {
        if (animationComponent == null || string.IsNullOrWhiteSpace(clipName))
            return;

        if (animationComponent[clipName] == null)
            return;

        if (currentClip == clipName && animationComponent.IsPlaying(clipName))
            return;

        currentClip = clipName;
        animationComponent.CrossFade(clipName, crossFadeDuration);
    }

    float GetClipDuration(string clipName, float fallbackDuration)
    {
        ResolveAnimation();
        if (animationComponent == null || string.IsNullOrWhiteSpace(clipName))
            return fallbackDuration;

        AnimationState state = animationComponent[clipName];
        if (state == null || state.length <= 0f)
            return fallbackDuration;

        return state.length;
    }
}
