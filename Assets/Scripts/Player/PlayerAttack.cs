using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    public float attackRange = 3f;
    public float damage = 20f;
    public float attackCooldown = 0.5f;
    private float lastAttackTime;
    PlayerStats stats;

    public Camera cam;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
    }

    void Attack()
    {
        RaycastHit hit;

        // 🔥 jeito correto
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, attackRange))
        {
        
            EnemyHealth enemy = hit.transform.GetComponent<EnemyHealth>();

            if (enemy != null)
            {
                PlayerStats stats = GetComponent<PlayerStats>();

                float finalDamage = stats != null ? stats.GetDamage() : damage;

                enemy.TakeDamage(finalDamage);
            }
        }
    }
}