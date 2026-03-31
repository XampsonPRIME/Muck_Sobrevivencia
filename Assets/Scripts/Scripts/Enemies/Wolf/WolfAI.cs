using UnityEngine;
using UnityEngine.AI;

public class WolfAI : EnemyAI
{
    private float lastDashTime;
    private Rigidbody rb;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
    }

    protected override void Start()
    {
        base.Start();

        agent.speed = stats.speed;
        agent.angularSpeed = 720f;
        agent.acceleration = 20f;
    }

    protected override void Update()
    {
        if (player == null || stats == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // 🐺 Fora do range
        if (distance > stats.detectionRange)
        {
            Patrol();
            return;
        }

        // 🐺 Muito perto → ataque
        if (distance <= stats.attackRange)
        {
            Attack();
            return;
        }

        // 🐺 Dash (distância média)
        if (distance <= stats.dashRange && distance > stats.attackRange + 1f)
        {
            if (CanDash())
            {
                Dash();
                return;
            }
        }

        // 🐺 Persegue normalmente
        ChasePlayer(distance);
    }

    protected override void ChasePlayer(float distance)
    {
        if (!agent.enabled) return;

        agent.SetDestination(player.position);
        RotateToPlayer();
    }

    void RotateToPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }

    bool CanDash()
    {
        return Time.time >= lastDashTime + stats.dashCooldown;
    }

    void Dash()
    {
        lastDashTime = Time.time;

        agent.enabled = false;

        rb.linearVelocity = Vector3.zero;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;

        // gira antes do dash
        transform.rotation = Quaternion.LookRotation(direction);

        rb.AddForce(direction * stats.dashForce, ForceMode.Impulse);

        Invoke(nameof(ResetAgent), 0.5f);
    }

    void ResetAgent()
    {
        agent.enabled = true;

        agent.Warp(transform.position);
        agent.SetDestination(player.position);
    }

    protected override void Attack()
    {
        Debug.Log("🐺 Lobo atacando!");

        // próximo passo: dano real
    }
}