using UnityEngine;

public class GameState : MonoBehaviour
{
    public static bool IsInventoryOpen = false;
    public static bool IsVendorOpen = false;
    public static bool IsCraftingOpen = false;
    public static bool IsDebugChatOpen = false;
    public static bool IsBestiaryOpen = false;
    public static bool IsQuestJournalOpen = false;
    public static bool IsMapOpen = false;
    public static bool IsDemoGuideOpen = false;
    public static bool IsWorldLoading = false;
    public static bool IsPowerSelectionOpen = false;
    public static bool IsPlayerDead = false;
    public static bool IsInLobby = false;
    public static bool IsPaused = false;
    public static int LastUiCloseFrame = -1;
}
