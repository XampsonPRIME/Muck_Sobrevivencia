using UnityEngine;
using UnityEngine.EventSystems;

public class GlobalUI : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // 🔥 garante que só existe 1
        var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);

        if (systems.Length > 1)
        {
            Destroy(gameObject);
        }
    }
}