using TMPro;
using UnityEngine;

public class LevelHUD : MonoBehaviour
{
    public Vector2 anchoredPosition = new Vector2(28f, -250f);
    public Vector2 size = new Vector2(320f, 70f);

    PlayerProgression progression;
    PlayerMovement trackedPlayer;
    TextMeshProUGUI levelText;

    void Start()
    {
        ResolveProgression();
        EnsureUI();
        Refresh();
    }

    void Update()
    {
        ResolveProgression();

        EnsureUI();
        Refresh();
    }

    void ResolveProgression()
    {
        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null)
        {
            trackedPlayer = null;
            progression = null;
            return;
        }

        if (trackedPlayer != player || progression == null)
        {
            trackedPlayer = player;
            progression = player.GetComponent<PlayerProgression>() ?? player.gameObject.AddComponent<PlayerProgression>();
        }
    }

    void EnsureUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existingRoot = canvas.transform.Find("LevelPanel");
        GameObject rootObject;

        if (existingRoot != null)
        {
            rootObject = existingRoot.gameObject;
        }
        else
        {
            rootObject = new GameObject("LevelPanel");
            rootObject.transform.SetParent(canvas.transform, false);
            rootObject.AddComponent<CanvasRenderer>();
        }

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = rootObject.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = size;

        if (levelText == null)
            levelText = EnsureText(rootObject.transform);
    }

    TextMeshProUGUI EnsureText(Transform parent)
    {
        Transform existing = parent.Find("LevelText");
        GameObject textObject;
        TextMeshProUGUI text;

        if (existing != null)
        {
            textObject = existing.gameObject;
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textObject = new GameObject("LevelText");
            textObject.transform.SetParent(parent, false);
            textObject.AddComponent<CanvasRenderer>();
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        text.alignment = TextAlignmentOptions.TopLeft;
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Bold;
        text.margin = new Vector4(0f, 0f, 0f, 0f);
        text.color = Color.white;
        return text;
    }

    public void Refresh()
    {
        if (levelText == null)
            return;

        if (progression == null)
        {
            levelText.text = "Nivel 1\n0/0 XP";
            return;
        }

        int requiredXp = progression.GetXpRequiredForNextLevel();
        string xpLabel = requiredXp > 0
            ? $"{progression.currentXp}/{requiredXp} XP"
            : "MAX";

        levelText.text = $"Nivel {progression.currentLevel}\n{xpLabel}";
    }
}
