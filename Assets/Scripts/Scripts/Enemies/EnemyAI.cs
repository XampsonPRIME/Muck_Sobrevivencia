using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : EnemyBase
{
    protected NavMeshAgent agent;
    protected Transform player;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    protected virtual void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= stats.detectionRange)
        {
            ChasePlayer(distance);
        }
        else
        {
            Patrol();
        }
    }

    protected virtual void ChasePlayer(float distance)
    {
        agent.SetDestination(player.position);

        if (distance <= stats.attackRange)
        {
            Attack();
        }
    }

    protected virtual void Patrol()
    {
        // depois a gente melhora isso
    }

    protected virtual void Attack()
    {
        Debug.Log("Inimigo atacando");
    }
}