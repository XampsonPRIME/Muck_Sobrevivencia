using UnityEngine;

public class GameState : MonoBehaviour
{
    public static bool IsInventoryOpen = false;
    public static bool IsVendorOpen = false;
    public static bool IsCraftingOpen = false;
    public static bool IsPlayerDead = false;
    public static bool IsInLobby = false;
    public static bool IsPaused = false;
    public static int LastUiCloseFrame = -1;
}
