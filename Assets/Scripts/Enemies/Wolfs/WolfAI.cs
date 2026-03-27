using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class WolfAI : MonoBehaviour
{
    public Transform player;

    [Header("Stats Lobo")]
    public float speed = 6f;
    public float detectionRange = 30f;
    public float attackRange = 2f;
    public float damage = 15f;
    public float attackCooldown = 1.2f;

    private float lastAttackTime;
    private NavMeshAgent agent;

    EnemyCombatAudio combatAudio;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
        combatAudio = GetComponent<EnemyCombatAudio>();
        StartCoroutine(Setup());
    }


    IEnumerator Setup()
    {
        yield return new WaitForSeconds(0.6f);

        NavMeshHit hit;

        if (NavMesh.SamplePosition(transform.position, out hit, 200f, NavMesh.AllAreas))
        {
            transform.position = hit.position;

            agent.enabled = true;
            agent.Warp(hit.position);
            agent.speed = speed;


        }
        else
        {
            Debug.LogError("❌ Lobo não encontrou NavMesh!");
        }
    }

    void Update()
    {
        if (!agent.enabled || player == null) return;

        PlayerHealth ph = player.GetComponent<PlayerHealth>();

        if (ph != null && ph.isDead)
        {
            agent.isStopped = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRange)
        {
            Chase(distance);
        }

        Rotate();
    }
    void Chase(float distance)
    {
        // 🔥 SEMPRE tenta ir até o player
        NavMeshHit hit;

        if (NavMesh.SamplePosition(player.position, out hit, 10f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        agent.isStopped = false;

        float realAttackRange = attackRange + 0.5f;

        if (distance <= realAttackRange)
        {
            agent.stoppingDistance = attackRange * 0.6f;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
            if (combatAudio != null)
            {
                combatAudio.PlayAttackSound();
            }

        }
        else
        {
            agent.stoppingDistance = 0f;
        }
    }

    void Attack()
    {
        PlayerHealth ph = player.GetComponent<PlayerHealth>();

        if (ph != null)
        {
            ph.TakeDamage(damage);
        }

        // 🔥 avanço (mordida)
        Vector3 dir = (player.position - transform.position).normalized;
        transform.position += dir * 0.5f;
    }

    void Rotate()
    {
        if (agent.velocity.magnitude > 0.1f)
        {
            Vector3 dir = agent.velocity.normalized;
            dir.y = 0;

            Quaternion rot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rot,
                Time.deltaTime * 8f
            );
        }
    }
}