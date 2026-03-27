using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 50f;
    public float currentHealth;

    [Header("Drop")]
    public GameObject lootPrefab;
    public LayerMask groundLayer;

    [Range(0, 1)]
    public float dropChance = 0.8f;

    public bool isBoss = true;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;


    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {

        DropLoot();

        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            spawner.EnemyDied();
        }

        PlayerStats stats = FindFirstObjectByType<PlayerStats>();

        if (stats != null)
        {
            stats.AddXP(5f); // 🔥 XP por kill
        }

        Destroy(gameObject);
    }

    void DropLoot()
    {
        if (lootPrefab == null) return;

        if (Random.value > dropChance)
        {
            Debug.Log("❌ não dropou");
            return;
        }

        Vector3 spawnPos = transform.position + Vector3.up * 3f;

        RaycastHit hit;

        if (Physics.Raycast(spawnPos, Vector3.down, out hit, 10f, groundLayer))
        {
            Instantiate(lootPrefab, hit.point + Vector3.up * 0.2f, Quaternion.identity);
        }
    }
}