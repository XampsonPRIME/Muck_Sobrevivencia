using UnityEngine;

[DisallowMultipleComponent]
public class MiniKrugEnemyProfile : MonoBehaviour
{
    [Header("Identidade")]
    public string rewardDisplayName = "Mushroom Mon";

    [Header("Visual")]
    public Vector3 spawnScale = new Vector3(1.18f, 1.18f, 1.18f);

    [Header("Vida")]
    public int maxHealth = 18;
    public int healthBonusPerLevel = 4;

    [Header("Combate")]
    public float contactDamage = 9f;
    public float contactDamageBonusPerLevel = 1.8f;
    public float attackRange = 1.35f;
    public float attackCooldown = 1.15f;
    public float moveSpeed = 3.2f;
    public float moveSpeedBonusPerLevel = 0.12f;
    public float rotationSpeed = 9f;
    public float targetRefreshInterval = 0.35f;
    public bool pursueTarget = true;
    public float engageRange = 999f;
    public bool huntPlayerAtNight = false;

    [Header("Drop")]
    public int minGoldDrop = 4;
    public int maxGoldDrop = 9;
    public int goldBonusPerLevel = 1;
    public int xpReward = 24;
    public int xpBonusPerLevel = 8;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 1.6f, 0f);
    public float damagePopupLifetime = 0.9f;
    public float damagePopupRiseSpeed = 1.55f;

    [Header("Chao")]
    public float groundRayHeight = 9f;
    public float maxGroundRayDistance = 22f;

    [Header("Colisao")]
    public Vector3 colliderCenter = new Vector3(0f, 0.78f, 0f);
    public float colliderRadius = 0.4f;
    public float colliderHeight = 1.45f;

    public void ApplyTo(MiniKrug enemy)
    {
        if (enemy == null)
            return;

        enemy.rewardDisplayName = rewardDisplayName;
        enemy.maxHealth = maxHealth;
        enemy.healthBonusPerLevel = healthBonusPerLevel;
        enemy.contactDamage = contactDamage;
        enemy.contactDamageBonusPerLevel = contactDamageBonusPerLevel;
        enemy.attackRange = attackRange;
        enemy.attackCooldown = attackCooldown;
        enemy.moveSpeed = moveSpeed;
        enemy.moveSpeedBonusPerLevel = moveSpeedBonusPerLevel;
        enemy.rotationSpeed = rotationSpeed;
        enemy.targetRefreshInterval = targetRefreshInterval;
        enemy.pursueTarget = pursueTarget;
        enemy.engageRange = engageRange;
        enemy.huntPlayerAtNight = huntPlayerAtNight;
        enemy.minGoldDrop = minGoldDrop;
        enemy.maxGoldDrop = maxGoldDrop;
        enemy.goldBonusPerLevel = goldBonusPerLevel;
        enemy.xpReward = xpReward;
        enemy.xpBonusPerLevel = xpBonusPerLevel;
        enemy.uiWorldOffset = uiWorldOffset;
        enemy.damagePopupLifetime = damagePopupLifetime;
        enemy.damagePopupRiseSpeed = damagePopupRiseSpeed;
        enemy.groundRayHeight = groundRayHeight;
        enemy.maxGroundRayDistance = maxGroundRayDistance;
        enemy.colliderCenter = colliderCenter;
        enemy.colliderRadius = colliderRadius;
        enemy.colliderHeight = colliderHeight;
    }
}
