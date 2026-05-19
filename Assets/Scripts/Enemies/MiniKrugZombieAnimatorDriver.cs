using UnityEngine;

[DisallowMultipleComponent]
public class MiniKrugZombieAnimatorDriver : MonoBehaviour
{
    public Animator animator;
    public string idleState = "Idle";
    public string walkState = "Walk";
    public string attackState = "Attack";
    public string damageState = "Damage";
    public string deathState = "Death";
    public float locomotionThreshold = 0.06f;
    public float crossFadeDuration = 0.08f;
    public float attackLockDuration = 0.45f;
    public float damageLockDuration = 0.3f;
    public float deathHoldDuration = 1.15f;
    public float maxActiveDistance = 18f;
    public float cullingCheckInterval = 0.2f;

    float actionLockUntil;
    float locomotionLockUntil;
    float nextCullingCheckTime;
    bool hasLastPosition;
    bool canAnimate = true;
    bool isDead;
    string currentState;
    Vector3 lastPosition;

    public float DeathDuration => deathHoldDuration;

    void Awake()
    {
        ResolveAnimator();
        lastPosition = transform.position;
        hasLastPosition = true;
    }

    void Update()
    {
        if (isDead)
            return;

        ResolveAnimator();
        if (animator == null)
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
        PlayState(shouldMove ? walkState : idleState);
    }

    public void PlayIdle()
    {
        if (isDead)
            return;

        PlayState(idleState);
    }

    public void PlayAttack()
    {
        if (isDead)
            return;

        actionLockUntil = Time.time + attackLockDuration;
        PlayState(attackState, true);
    }

    public void PlayMoveFor(float duration)
    {
        if (isDead)
            return;

        locomotionLockUntil = Mathf.Max(locomotionLockUntil, Time.time + Mathf.Max(0f, duration));
        PlayState(walkState);
    }

    public void PlayDamage()
    {
        if (isDead)
            return;

        actionLockUntil = Time.time + damageLockDuration;
        PlayState(damageState, true);
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;
        PlayState(deathState, true);
    }

    void ResolveAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
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
        animator.enabled = shouldAnimate;

        if (shouldAnimate)
        {
            currentState = null;
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

    void PlayState(string stateName, bool forceRestart = false)
    {
        ResolveAnimator();
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        UpdateAnimationCulling();
        if (!canAnimate)
            return;

        if (!forceRestart && currentState == stateName)
            return;

        currentState = stateName;
        animator.CrossFadeInFixedTime(stateName, crossFadeDuration, 0, forceRestart ? 0f : float.NegativeInfinity);
    }
}
