using UnityEngine;

[DisallowMultipleComponent]
public class BossEnemyProfile : MonoBehaviour
{
    [Header("Progressao")]
    public string bossDisplayName = "Forest Mushroom Boss";
    public int bossLevel = 7;
    public int minimumPlayerLevel = 4;
    public int healthBonusPerLevel = 45;
    public float contactDamageBonusPerLevel = 5f;
    public float moveSpeedBonusPerLevel = 0.08f;
    public int goldBonusPerLevel = 7;
    public int xpBonusPerLevel = 45;

    [Header("Vida")]
    public int maxHealth = 280;

    [Header("Combate")]
    public float contactDamage = 42f;
    public float attackRange = 2.4f;
    public float attackCooldown = 1.2f;
    public float moveSpeed = 3.1f;
    public float returnSpeed = 3.5f;
    public float rotationSpeed = 8.5f;
    public float detectionRange = 20f;
    public float targetRefreshInterval = 0.25f;

    [Header("Ataque em Area")]
    public bool enableAreaAttack = true;
    public float areaAttackDamage = 55f;
    public float areaAttackRadius = 5.2f;
    public float areaAttackTriggerRange = 8.5f;
    public float areaAttackCooldown = 6.25f;
    public float areaAttackTelegraphDuration = 1.15f;
    public float areaAttackMinRange = 2.4f;
    public float areaAttackIndicatorYOffset = 0.06f;
    public float areaAttackIndicatorThickness = 0.08f;
    public Color areaAttackIndicatorColor = new Color(0.95f, 0.22f, 0.16f, 0.5f);

    [Header("Territorio")]
    public float maxChaseDistanceFromSpawn = 18f;
    public float patrolRadius = 5f;
    public float patrolPointReachDistance = 1f;
    public float patrolRefreshInterval = 2.4f;

    [Header("Recompensa")]
    public int minGoldDrop = 65;
    public int maxGoldDrop = 110;
    public int xpReward = 650;

    [Header("Chao")]
    public float groundRayHeight = 14f;
    public float maxGroundRayDistance = 36f;
    public Vector3 colliderCenter = new Vector3(0f, 1.5f, 0f);
    public float colliderRadius = 1.05f;
    public float colliderHeight = 3f;

    [Header("Feedback")]
    public Vector3 uiWorldOffset = new Vector3(0f, 2.8f, 0f);
    public float healthUiVisibleDuration = 3.5f;
    public float damagePopupLifetime = 1f;
    public float damagePopupRiseSpeed = 1.9f;

    public void ApplyTo(BossEnemy enemy)
    {
        if (enemy == null)
            return;

        enemy.bossDisplayName = bossDisplayName;
        enemy.bossLevel = bossLevel;
        enemy.minimumPlayerLevel = minimumPlayerLevel;
        enemy.healthBonusPerLevel = healthBonusPerLevel;
        enemy.contactDamageBonusPerLevel = contactDamageBonusPerLevel;
        enemy.moveSpeedBonusPerLevel = moveSpeedBonusPerLevel;
        enemy.goldBonusPerLevel = goldBonusPerLevel;
        enemy.xpBonusPerLevel = xpBonusPerLevel;
        enemy.maxHealth = maxHealth;
        enemy.contactDamage = contactDamage;
        enemy.attackRange = attackRange;
        enemy.attackCooldown = attackCooldown;
        enemy.moveSpeed = moveSpeed;
        enemy.returnSpeed = returnSpeed;
        enemy.rotationSpeed = rotationSpeed;
        enemy.detectionRange = detectionRange;
        enemy.targetRefreshInterval = targetRefreshInterval;
        enemy.enableAreaAttack = enableAreaAttack;
        enemy.areaAttackDamage = areaAttackDamage;
        enemy.areaAttackRadius = areaAttackRadius;
        enemy.areaAttackTriggerRange = areaAttackTriggerRange;
        enemy.areaAttackCooldown = areaAttackCooldown;
        enemy.areaAttackTelegraphDuration = areaAttackTelegraphDuration;
        enemy.areaAttackMinRange = areaAttackMinRange;
        enemy.areaAttackIndicatorYOffset = areaAttackIndicatorYOffset;
        enemy.areaAttackIndicatorThickness = areaAttackIndicatorThickness;
        enemy.areaAttackIndicatorColor = areaAttackIndicatorColor;
        enemy.maxChaseDistanceFromSpawn = maxChaseDistanceFromSpawn;
        enemy.patrolRadius = patrolRadius;
        enemy.patrolPointReachDistance = patrolPointReachDistance;
        enemy.patrolRefreshInterval = patrolRefreshInterval;
        enemy.minGoldDrop = minGoldDrop;
        enemy.maxGoldDrop = maxGoldDrop;
        enemy.xpReward = xpReward;
        enemy.groundRayHeight = groundRayHeight;
        enemy.maxGroundRayDistance = maxGroundRayDistance;
        enemy.colliderCenter = colliderCenter;
        enemy.colliderRadius = colliderRadius;
        enemy.colliderHeight = colliderHeight;
        enemy.uiWorldOffset = uiWorldOffset;
        enemy.healthUiVisibleDuration = healthUiVisibleDuration;
        enemy.damagePopupLifetime = damagePopupLifetime;
        enemy.damagePopupRiseSpeed = damagePopupRiseSpeed;
    }
}
