using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    public Image staminaFill;

    PlayerMovement player;

    void Update()
    {
        if (player == null || staminaFill == null)
            return;

        float current = player.currentStamina;
        float max = player.maxStamina;

        staminaFill.fillAmount = current / max;
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerMovement>();
    }
}
