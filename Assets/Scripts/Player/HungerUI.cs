using UnityEngine;
using UnityEngine.UI;

public class HungerUI : MonoBehaviour
{
    public PlayerMovement player;
    public Image hungerFill;

    void Start()
    {
        ResolvePlayer();
    }

    void Update()
    {
        ResolvePlayer();

        if (player == null || hungerFill == null)
            return;

        hungerFill.fillAmount = player.currentHunger / player.maxHunger;
    }

    void ResolvePlayer()
    {
        if (player == null)
            player = LanMultiplayerManager.FindGameplayPlayer();
    }
}
