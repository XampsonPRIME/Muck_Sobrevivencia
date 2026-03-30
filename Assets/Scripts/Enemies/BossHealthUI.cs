using UnityEngine;
using UnityEngine.UI;

public class BossHealthUI : MonoBehaviour
{
    public Slider healthBar;

    private EnemyHealth currentBoss;

    void Start()
    {
        gameObject.SetActive(false);
    }

    public void SetBoss(EnemyHealth boss)
    {
        Debug.Log("🔥 SETOU BOSS");

        currentBoss = boss;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (currentBoss == null) return;

        healthBar.value = currentBoss.CurrentHealth / currentBoss.MaxHealth;
    }
}