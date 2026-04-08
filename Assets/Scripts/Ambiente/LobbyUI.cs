using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    Canvas canvas;
    SaveGameManager saveGameManager;
    GraphicRaycaster graphicRaycaster;

    void Start()
    {
        saveGameManager = FindFirstObjectByType<SaveGameManager>();
        if (saveGameManager == null)
        {
            GameObject managerObject = new GameObject("SaveGameManager");
            saveGameManager = managerObject.AddComponent<SaveGameManager>();
        }

        EnsureEventSystem();
        BuildUI();
        EnterLobby();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("LobbyCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        graphicRaycaster = canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObject = CreateUiObject("Overlay", canvas.transform);
        RectTransform overlayRect = overlayObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);

        GameObject titleObject = CreateUiObject("Title", overlayObject.transform);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(900f, 120f);
        titleRect.anchoredPosition = new Vector2(0f, 140f);

        TextMeshProUGUI titleText = titleObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "Marped Survivor";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 72f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.96f, 0.82f, 1f);

        GameObject subtitleObject = CreateUiObject("Subtitle", overlayObject.transform);
        RectTransform subtitleRect = subtitleObject.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.sizeDelta = new Vector2(920f, 100f);
        subtitleRect.anchoredPosition = new Vector2(0f, 56f);

        TextMeshProUGUI subtitleText = subtitleObject.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "Sobreviva, evolua e enfrente criaturas cada vez mais fortes.";
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.fontSize = 30f;
        subtitleText.color = new Color(0.84f, 0.9f, 0.98f, 1f);

        bool hasSave = saveGameManager != null && saveGameManager.HasSave();

        if (hasSave)
        {
            CreateButton(
                overlayObject.transform,
                "ContinueButton",
                "Continuar",
                new Vector2(0f, -34f),
                new Color(0.72f, 0.56f, 0.18f, 1f),
                ContinueGame
            );

            CreateButton(
                overlayObject.transform,
                "NewGameButton",
                "Novo jogo",
                new Vector2(0f, -154f),
                new Color(0.34f, 0.54f, 0.78f, 1f),
                StartGame
            );
        }
        else
        {
            CreateButton(
                overlayObject.transform,
                "StartButton",
                "Entrar no jogo",
                new Vector2(0f, -56f),
                new Color(0.72f, 0.56f, 0.18f, 1f),
                StartGame
            );
        }
    }

    void EnterLobby()
    {
        GameState.IsInLobby = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!GameState.IsInLobby || canvas == null || graphicRaycaster == null || EventSystem.current == null || Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            Button button = results[i].gameObject.GetComponentInParent<Button>();
            if (button == null || !button.interactable)
                continue;

            button.onClick.Invoke();
            break;
        }
    }

    void StartGame()
    {
        saveGameManager?.StartNewGame();
        Destroy(gameObject);
    }

    void ContinueGame()
    {
        if (saveGameManager != null && saveGameManager.ContinueFromSave())
            Destroy(gameObject);
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Color buttonColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(360f, 96f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        GameObject buttonTextObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform buttonTextRect = buttonTextObject.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonText = buttonTextObject.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontSize = 40f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = new Color(0.12f, 0.08f, 0.02f, 1f);

        return button;
    }

    GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<CanvasRenderer>();
        return obj;
    }

    void OnDestroy()
    {
        if (GameState.IsInLobby)
            Time.timeScale = 1f;
    }
}
