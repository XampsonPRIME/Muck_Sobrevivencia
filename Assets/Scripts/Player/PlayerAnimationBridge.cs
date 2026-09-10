using UnityEngine;

public static class PlayerAnimationBridge
{
    public const string SpeedParameter = "Speed";
    public const string DirectionXParameter = "DirectionX";
    public const string LegacyChopTrigger = "Chop";
    public const string AttackTrigger = "Attack";
    public const string CutWoodTrigger = "CutWood";
    public const string MineTrigger = "Mine";
    public const string PickupTrigger = "Pickup";
    public const string PowerStoneWallTrigger = "PowerStoneWall";
    public const string PowerDiveTrigger = "PowerDive";
    public const string Power1Trigger = "Power1";
    public const string Power2Trigger = "Power2";
    public const string Power3Trigger = "Power3";
    public const string VictoryTrigger = "Victory";
    public const string DieTrigger = "Die";
    public const string JumpTrigger = "Jump";
    public const string RunJumpTrigger = "RunJump";
    public const string FallTrigger = "Fall";

    public static bool Trigger(Component owner, string triggerName)
    {
        if (owner == null)
            return false;

        PlayerMovement movement = owner as PlayerMovement ?? owner.GetComponent<PlayerMovement>();
        if (movement != null && movement.VisualAnimator != null)
            return Trigger(movement.VisualAnimator, triggerName);

        return Trigger(owner.GetComponentInChildren<Animator>(true), triggerName);
    }

    public static bool Trigger(GameObject owner, string triggerName)
    {
        if (owner == null)
            return false;

        return Trigger(owner.GetComponentInChildren<Animator>(true), triggerName);
    }

    public static bool Trigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
            return false;

        MeshyTitanAnimatorDriver titanDriver = animator.GetComponentInParent<MeshyTitanAnimatorDriver>();
        if (titanDriver != null && titanDriver.Play(triggerName))
            return true;

        if (SetTriggerIfPresent(animator, triggerName))
            return true;

        if (triggerName == RunJumpTrigger && SetTriggerIfPresent(animator, JumpTrigger))
            return true;

        if (ShouldFallbackToLegacyChop(triggerName))
            return SetTriggerIfPresent(animator, LegacyChopTrigger);

        return false;
    }

    public static bool HasParameter(Animator animator, string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter != null && parameter.name == parameterName && parameter.type == type)
                return true;
        }

        return false;
    }

    public static bool SetFloatIfPresent(Animator animator, string parameterName, float value)
    {
        if (!HasParameter(animator, parameterName, AnimatorControllerParameterType.Float))
            return false;

        animator.SetFloat(parameterName, value);
        return true;
    }

    static bool SetTriggerIfPresent(Animator animator, string triggerName)
    {
        if (!HasParameter(animator, triggerName, AnimatorControllerParameterType.Trigger))
            return false;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
        return true;
    }

    static bool ShouldFallbackToLegacyChop(string triggerName)
    {
        return triggerName == AttackTrigger ||
               triggerName == CutWoodTrigger ||
               triggerName == MineTrigger ||
               triggerName == PickupTrigger;
    }
}
