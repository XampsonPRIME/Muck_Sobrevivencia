using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    public PlayerMovement player;
    public Image healthFill;

    void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();
    }

    void Update()
    {
        if (player == null || healthFill == null) return;

        float value = player.currentHealth / player.maxHealth;

        healthFill.fillAmount = value;

        // 🔥 cor dinâmica
        healthFill.color = Color.Lerp(
            new Color(0.10f, 0f, 0f), // vermelho escuro
            Color.red, // vermelho brilhante
            player.currentHealth / player.maxHealth);
    }
}