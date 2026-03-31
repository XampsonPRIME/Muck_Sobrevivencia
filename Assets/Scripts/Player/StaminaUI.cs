using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    public Image staminaFill;

    PlayerMovement player;

    void Update()
    {
        float current = player.currentStamina;
        float max = player.maxStamina;

        staminaFill.fillAmount = current / max;
    }

    void Start()
    {
        player = FindObjectOfType<PlayerMovement>();
    }
}