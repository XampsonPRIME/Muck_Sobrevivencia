using System.Collections.Generic;
using UnityEngine;

public class MeshyTitanAnimatorDriver : MonoBehaviour
{
    const string BaseResourcePath = "Characters/MeshyHero/Meshy_AI_Stylized_fantasy_male_biped/Meshy_AI_Stylized_fantasy_male_biped_";

    Animator animator;
    AnimatorOverrideController overrideController;
    readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
    AnimationClip actionSourceClip;

    readonly Dictionary<string, AnimationClip> loadedClips = new Dictionary<string, AnimationClip>();
    AnimationClip currentActionClip;
    float nextSameActionTime;

    public bool Initialize(Animator targetAnimator, RuntimeAnimatorController baseController)
    {
        if (targetAnimator == null || baseController == null)
            return false;

        animator = targetAnimator;
        overrideController = new AnimatorOverrideController(baseController)
        {
            name = "MeshyTitanRuntimeOverride"
        };

        overrideController.GetOverrides(overrides);
        if (overrides.Count == 0)
            return false;

        ApplyLocomotionOverrides();
        animator.runtimeAnimatorController = overrideController;
        return true;
    }

    public bool Play(string triggerName)
    {
        if (animator == null || overrideController == null)
            return false;

        AnimationClip clip = ResolveActionClip(triggerName);
        if (clip == null)
            return false;

        if (actionSourceClip == null)
            actionSourceClip = FindActionSourceClip();

        if (actionSourceClip == null)
            return false;

        if (clip == currentActionClip && Time.unscaledTime < nextSameActionTime)
            return true;

        if (clip != currentActionClip)
        {
            SetOverride(actionSourceClip, clip);
            currentActionClip = clip;
        }

        if (!PlayerAnimationBridge.HasParameter(animator, PlayerAnimationBridge.LegacyChopTrigger, AnimatorControllerParameterType.Trigger))
            return false;

        nextSameActionTime = Time.unscaledTime + 0.18f;
        animator.ResetTrigger(PlayerAnimationBridge.LegacyChopTrigger);
        animator.SetTrigger(PlayerAnimationBridge.LegacyChopTrigger);
        return true;
    }

    void ApplyLocomotionOverrides()
    {
        AnimationClip idle = LoadClip("Animation_Idle_03");
        AnimationClip walk = LoadClip("Animation_Walking");
        AnimationClip run = LoadClip("Animation_Running");

        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            if (original == null)
                continue;

            string name = original.name.ToLowerInvariant();
            if (idle != null && name.Contains("idle"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, idle);
            else if (walk != null && name.Contains("walk"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, walk);
            else if (run != null && name.Contains("run"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, run);
        }

        overrideController.ApplyOverrides(overrides);
    }

    void SetOverride(AnimationClip source, AnimationClip replacement)
    {
        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].Key != source)
                continue;

            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(source, replacement);
            overrideController.ApplyOverrides(overrides);
            return;
        }
    }

    AnimationClip FindActionSourceClip()
    {
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            if (original == null)
                continue;

            string name = original.name.ToLowerInvariant();
            if (name.Contains("axe") || name.Contains("hit") || name.Contains("chop") || name.Contains("attack"))
                return original;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            if (original == null)
                continue;

            string name = original.name.ToLowerInvariant();
            if (!name.Contains("idle") && !name.Contains("walk") && !name.Contains("run"))
                return original;
        }

        return null;
    }

    AnimationClip ResolveActionClip(string triggerName)
    {
        switch (triggerName)
        {
            case PlayerAnimationBridge.LegacyChopTrigger:
            case PlayerAnimationBridge.AttackTrigger:
                return LoadClip("Animation_Charged_Upward_Slash");

            case PlayerAnimationBridge.CutWoodTrigger:
                return LoadClip("Animation_Right_Hand_Sword_Slash");

            case PlayerAnimationBridge.MineTrigger:
                return LoadClip("Animation_Charged_Ground_Slam");

            case PlayerAnimationBridge.PickupTrigger:
                return LoadClip("Animation_Collect_Object");

            case PlayerAnimationBridge.PowerStoneWallTrigger:
            case PlayerAnimationBridge.Power2Trigger:
                return LoadClip("Animation_Angry_Ground_Stomp_2");

            case PlayerAnimationBridge.PowerDiveTrigger:
            case PlayerAnimationBridge.Power3Trigger:
                return LoadClip("Animation_Dive_Down_and_Land_2");

            case PlayerAnimationBridge.Power1Trigger:
                return LoadClip("Animation_Charged_Ground_Slam");

            case PlayerAnimationBridge.VictoryTrigger:
                return LoadClip("Animation_Idle_11");

            case PlayerAnimationBridge.DieTrigger:
                return LoadClip("Animation_Dead");

            case PlayerAnimationBridge.JumpTrigger:
                return LoadClip("Animation_Regular_Jump");

            case PlayerAnimationBridge.RunJumpTrigger:
                return LoadClip("Animation_Run_and_Leap");

            case PlayerAnimationBridge.FallTrigger:
                return LoadClip("Animation_falling_down");
        }

        return null;
    }

    AnimationClip LoadClip(string assetName)
    {
        if (loadedClips.TryGetValue(assetName, out AnimationClip cachedClip))
            return cachedClip;

        AnimationClip clip = null;
        Object[] assets = Resources.LoadAll(BaseResourcePath + assetName + "_withSkin");
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip candidate = assets[i] as AnimationClip;
            if (candidate == null || candidate.name.StartsWith("__preview__"))
                continue;

            clip = candidate;
            break;
        }

        loadedClips[assetName] = clip;
        return clip;
    }
}
