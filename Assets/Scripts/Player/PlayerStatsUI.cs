using UnityEngine;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    public PlayerStats stats;
    public TextMeshProUGUI text;

    void Update()
    {
        if (stats == null) return;

        text.text =
            "Força: " + Mathf.RoundToInt(stats.strength) +
            "\nLevel: " + stats.level +
            "\nXP: " + Mathf.RoundToInt(stats.xp) + " / " + Mathf.RoundToInt(stats.xpToNextLevel);
    }
}