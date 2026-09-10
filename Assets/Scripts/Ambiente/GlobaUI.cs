using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GlobalUI : MonoBehaviour
{
    void Awake()
    {
        UIEventSystemUtility.EnsureSingleEventSystem();
    }
}

public class GameplayCursorGuard : MonoBehaviour
{
    float nextLockCheckTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<GameplayCursorGuard>() != null)
            return;

        GameObject guardObject = new GameObject("Gameplay Cursor Guard");
        guardObject.AddComponent<GameplayCursorGuard>();
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        nextLockCheckTime = Time.unscaledTime + 0.25f;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            nextLockCheckTime = Time.unscaledTime + 0.15f;
    }

    void Update()
    {
        if (Time.unscaledTime < nextLockCheckTime)
            return;

        if (!ShouldLockCursor())
            return;

        if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    static bool ShouldLockCursor()
    {
        return !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPaused &&
               !GameState.IsPlayerDead &&
               !GameState.IsInventoryOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen &&
               !GameState.IsDebugChatOpen &&
               !GameState.IsBestiaryOpen &&
               !GameState.IsQuestJournalOpen &&
               !GameState.IsMapOpen &&
               !GameState.IsPowerSelectionOpen;
    }
}

public static class UIEventSystemUtility
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void DisablePersistentEventSystemsBeforeSceneLoad()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system == null)
                continue;

            system.enabled = false;
            foreach (BaseInputModule module in system.GetComponents<BaseInputModule>())
                module.enabled = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SanitizeSceneEventSystems()
    {
        EnsureSingleEventSystem();
    }

    public static EventSystem EnsureSingleEventSystem()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem primary = EventSystem.current != null &&
                              EventSystem.current.isActiveAndEnabled &&
                              EventSystem.current.gameObject.activeInHierarchy
            ? EventSystem.current
            : null;

        if (primary == null)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].isActiveAndEnabled && systems[i].gameObject.activeInHierarchy)
                {
                    primary = systems[i];
                    break;
                }
            }
        }

        if (primary == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            primary = eventSystemObject.GetComponent<EventSystem>();
        }

        DisableDuplicateSystems(systems, primary);

        if (!primary.gameObject.activeSelf)
            primary.gameObject.SetActive(true);

        primary.enabled = true;
        EventSystem.current = primary;
        EnsureInputSystemModule(primary);
        return primary;
    }

    static void DisableDuplicateSystems(EventSystem[] systems, EventSystem primary)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system == null || system == primary)
                continue;

            system.enabled = false;
            foreach (BaseInputModule module in system.GetComponents<BaseInputModule>())
                module.enabled = false;
        }
    }

    static void EnsureInputSystemModule(EventSystem eventSystem)
    {
        InputSystemUIInputModule[] inputModules = eventSystem.GetComponents<InputSystemUIInputModule>();
        InputSystemUIInputModule activeInputModule = inputModules.Length > 0
            ? inputModules[0]
            : eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        activeInputModule.enabled = true;
        for (int i = 1; i < inputModules.Length; i++)
            inputModules[i].enabled = false;

        foreach (StandaloneInputModule legacyModule in eventSystem.GetComponents<StandaloneInputModule>())
            legacyModule.enabled = false;
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class EditorCursorRecovery
{
    static EditorCursorRecovery()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += RestoreEditorCursor;
    }

    static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreEditorCursor();
            EditorApplication.delayCall += RestoreEditorCursor;
        }
    }

    static void RestoreEditorCursor()
    {
        if (EditorApplication.isPlaying)
            return;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
#endif
