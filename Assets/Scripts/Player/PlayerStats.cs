using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Level System")]
    public int level = 1;
    public float xp = 0f;
    public float xpToNextLevel = 10f;

    [Header("Stats")]
    public float strength = 10f;

    // 🔥 DANO FINAL
    public float GetDamage()
    {
        return strength;
    }

    // 🔥 GANHAR XP
    public void AddXP(float amount)
    {
        xp += amount;

        Debug.Log("⭐ XP: " + xp + " / " + xpToNextLevel);

        if (xp >= xpToNextLevel)
        {
            LevelUp();
        }
    }

    // 🔥 SUBIR LEVEL
    void LevelUp()
    {
        level++;
        xp = 0;

        // 🔥 aumenta força
        strength += 3f;

        // 🔥 aumenta dificuldade do próximo nível
        xpToNextLevel *= 1.5f;

        Debug.Log("🔥 LEVEL UP! Agora nível " + level);
        Debug.Log("💪 Força: " + strength);
    }
}