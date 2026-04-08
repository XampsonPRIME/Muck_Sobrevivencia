using UnityEngine;
using UnityEngine.UI;

public class ThirstUI : MonoBehaviour
{
    public PlayerMovement player;
    public Image thirstFill;

    void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();
    }

    void Update()
    {
        if (player == null || thirstFill == null)
            return;

        thirstFill.fillAmount = player.currentThirst / player.maxThirst;
    }
}
