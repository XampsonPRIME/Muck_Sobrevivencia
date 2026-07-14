using UnityEngine;

[CreateAssetMenu(menuName = "Elarion/Creatures/Wild Boar Config")]
public class WildBoarConfig : ScriptableObject
{
    [Header("Vida")]
    public int maxHealth = 120;

    [Header("Movimento e IA")]
    public float detectionDistance = 12f;
    public float loseTargetDistance = 22f;
    public float attackDistance = 2f;
    public float patrolSpeed = 2.1f;
    public float chaseSpeed = 5.8f;
    public float chargeSpeed = 9.5f;
    public float patrolRadius = 10f;

    [Header("Ataque")]
    public int minDamage = 24;
    public int maxDamage = 38;
    public float attackCooldown = 4f;
    public float knockbackForce = 7.5f;
    public float knockbackDuration = 0.26f;

    [Header("Drops")]
    [Range(0f, 1f)] public float thickLeatherChance = 0.6f;
    public int minThickLeatherDrop = 1;
    public int maxThickLeatherDrop = 3;
    [Range(0f, 1f)] public float sharpTuskChance = 0.25f;
    [Range(0f, 1f)] public float boarMeatChance = 0.1f;
    public int minBoarMeatDrop = 1;
    public int maxBoarMeatDrop = 2;
    [Range(0f, 1f)] public float trophyChance = 0.05f;
}
