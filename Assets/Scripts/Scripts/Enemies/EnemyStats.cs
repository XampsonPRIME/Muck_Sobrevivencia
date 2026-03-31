using UnityEngine;

[CreateAssetMenu(fileName = "EnemyStats", menuName = "Game/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    public string enemyName;
    public EnemyType type;

    public float maxHealth = 100;
    public float damage = 10;
    public float speed = 3.5f;

    public float detectionRange = 10f;
    public float attackRange = 2f;

    public float lootChance = 0.3f;

    public float dashRange = 6f;
    public float dashForce = 12f;
    public float dashCooldown = 3f;
}