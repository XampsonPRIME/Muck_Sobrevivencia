using UnityEngine;
using UnityEngine.UI;

public class HungerUI : MonoBehaviour
{
    public PlayerMovement player;
    public Image hungerFill;

        void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();
    }

    void Update()
    {
        if (player == null) return;

        hungerFill.fillAmount = player.currentHunger / player.maxHunger;
    }
}