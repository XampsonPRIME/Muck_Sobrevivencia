using UnityEngine;
using UnityEngine.AI;

public class BruteAI : MonoBehaviour
{
    public Transform player;

    [Header("Stats Brute")]
    public float speed = 2.5f;
    public float detectionRange = 50f;
    public float attackRange = 4f;
    public float damage = 25f;

    [Header("Boss")]
    public int requiredLevel = 3;

    private float attackCooldown = 2f;
    private float lastAttackTime;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private BossHealthUI bossUI;

    void Start()
{
    bossUI = FindFirstObjectByType<BossHealthUI>();
    health = GetComponent<EnemyHealth>();

    if (bossUI != null)
    {
        bossUI.SetBoss(health);
    }
}

    void Update()
    {
        if (agent == null || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRange)
        {
            // 🔥 ativa UI do boss
           
        }

        Rotate();
    }

    void Chase(float distance)
    {
        if (agent == null) return;

        agent.SetDestination(player.position);

        if (distance <= attackRange)
        {
            agent.isStopped = true;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
        else
        {
            agent.isStopped = false;
        }
    }

    void Attack()
    {
        Debug.Log("💀 Brute atacou!");

        PlayerHealth ph = player.GetComponent<PlayerHealth>();
        PlayerStats stats = player.GetComponent<PlayerStats>();

        if (ph == null || stats == null) return;

        float finalDamage;

        // 🔥 insta-kill se for fraco
        if (stats.level < requiredLevel)
        {
            Debug.Log("☠️ Player muito fraco! Insta-kill");
            finalDamage = ph.maxHealth;
        }
        else
        {
            finalDamage = damage;
        }

        ph.TakeDamage(finalDamage);
    }

    void Rotate()
    {
        if (agent == null) return;

        if (agent.velocity.magnitude > 0.1f)
        {
            Vector3 dir = agent.velocity.normalized;
            dir.y = 0;

            Quaternion rot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rot,
                Time.deltaTime * 3f
            );
        }
    }
}