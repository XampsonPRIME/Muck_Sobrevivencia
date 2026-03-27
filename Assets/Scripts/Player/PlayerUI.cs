using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public Slider healthBar;
    public Image fill; // 🔥 parte colorida da barra

    void Update()
    {
        if (playerHealth == null) return;

        float hp = playerHealth.currentHealth / playerHealth.maxHealth;

        healthBar.value = hp;

        // 🔥 muda cor conforme vida
        fill.color = Color.Lerp(Color.red, Color.green, hp);
    }
}