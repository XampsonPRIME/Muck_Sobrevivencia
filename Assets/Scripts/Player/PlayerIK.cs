using UnityEngine;

public class PlayerIK : MonoBehaviour
{
    Animator anim;

    public Transform rightHandTarget;

    [Range(0,1)]
    public float ikWeight = 0.7f; // 👈 controla força

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (rightHandTarget == null) return;

        // 🔥 posição com peso controlado
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, ikWeight);
        anim.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);

        // ❗ DESLIGA rotação por enquanto
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
    }
}