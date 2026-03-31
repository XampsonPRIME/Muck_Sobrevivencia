using UnityEngine;
using System;

public class EnemyBase : MonoBehaviour
{
    public EnemyStats stats;

    protected float currentHealth;

    public Action onDeath;

    protected virtual void Start()
    {
        if (stats != null)
        {
            currentHealth = stats.maxHealth;
        }
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        TryDropLoot();

        onDeath?.Invoke();

        Destroy(gameObject);
    }

    protected void TryDropLoot()
    {
        if (stats != null && UnityEngine.Random.value <= stats.lootChance)
        {
            Debug.Log("Dropou loot!");
        }
    }
}