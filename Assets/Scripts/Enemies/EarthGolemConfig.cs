using UnityEngine;

[CreateAssetMenu(menuName = "Elarion/Creatures/Earth Golem Config")]
public class EarthGolemConfig : ScriptableObject
{
    [Header("Vida e atributos")]
    public int maxHealth = 1200;
    public int defense = 80;
    public int resistance = 90;
    [Range(0f, 1f)] public float physicalDamageReduction = 0.25f;
    public float visualScale = 1.9f;

    [Header("IA")]
    public float detectionDistance = 15f;
    public float provocationDistance = 4.5f;
    public float loseTargetDistance = 26f;
    public float attackDistance = 6f;
    public float patrolSpeed = 1.15f;
    public float chaseSpeed = 2.15f;
    public float patrolRadius = 8f;
    public float returnHealPerSecond = 18f;

    [Header("Golpe no Chao")]
    public int groundSlamDamage = 85;
    public float groundSlamRadius = 6.8f;
    public float groundSlamCooldown = 4.25f;
    public float groundSlamWindupDuration = 0.58f;
    public float groundSlamRecoveryDuration = 0.42f;
    public float groundSlamStunDuration = 1.1f;

    [Header("Soco Pesado")]
    public int heavyPunchDamage = 70;
    public float heavyPunchRange = 5f;
    public float heavyPunchCooldown = 3.1f;
    public float heavyPunchWindupDuration = 0.32f;
    public float heavyPunchRecoveryDuration = 0.35f;
    public float heavyPunchKnockbackForce = 8f;

    [Header("Drops")]
    [Range(0f, 1f)] public float stoneFragmentChance = 0.7f;
    public int minStoneFragments = 3;
    public int maxStoneFragments = 8;
    [Range(0f, 1f)] public float resilientMossChance = 0.4f;
    public int minResilientMoss = 1;
    public int maxResilientMoss = 3;
    [Range(0f, 1f)] public float ironOreChance = 0.1f;
    public int minIronOre = 1;
    public int maxIronOre = 2;
    [Range(0f, 1f)] public float earthCoreChance = 0.2f;
}
