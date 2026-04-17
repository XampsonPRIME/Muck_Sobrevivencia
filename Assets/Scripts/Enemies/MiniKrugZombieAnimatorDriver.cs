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

    float actionLockUntil;
    bool hasLastPosition;
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

        if (!hasLastPosition)
        {
            lastPosition = transform.position;
            hasLastPosition = true;
        }

        float movedDistanceSqr = (transform.position - lastPosition).sqrMagnitude;
        lastPosition = transform.position;

        if (Time.time < actionLockUntil)
            return;

        PlayState(movedDistanceSqr > locomotionThreshold * locomotionThreshold ? walkState : idleState);
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
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
    }

    void PlayState(string stateName, bool forceRestart = false)
    {
        ResolveAnimator();
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        if (!forceRestart && currentState == stateName)
            return;

        currentState = stateName;
        animator.CrossFadeInFixedTime(stateName, crossFadeDuration, 0, forceRestart ? 0f : float.NegativeInfinity);
    }
}
