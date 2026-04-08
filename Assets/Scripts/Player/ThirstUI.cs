using UnityEngine;
using UnityEngine.UI;

public class ThirstUI : MonoBehaviour
{
    public PlayerMovement player;
    public Image thirstFill;

    void Start()
    {
        ResolvePlayer();
    }

    void Update()
    {
        ResolvePlayer();

        if (player == null || thirstFill == null)
            return;

        thirstFill.fillAmount = player.currentThirst / player.maxThirst;
    }

    void ResolvePlayer()
    {
        if (player == null)
            player = LanMultiplayerManager.FindGameplayPlayer();
    }
}
