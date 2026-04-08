using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIViewRefs : MonoBehaviour
{
    public Canvas canvas;
    public GraphicRaycaster graphicRaycaster;
    public GameObject mainMenuRoot;
    public GameObject multiplayerPopupBackdrop;
    public GameObject multiplayerPopupPanel;
    public Button primarySoloButton;
    public Button newSoloButton;
    public Button openMultiplayerButton;
    public Button closeMultiplayerButton;
    public Button hostButton;
    public Button continueSessionButton;
    public Button joinButton;
    public TMP_InputField addressInput;
    public TMP_InputField portInput;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI discoveryText;

    public bool IsConfigured()
    {
        return canvas != null &&
               graphicRaycaster != null &&
               mainMenuRoot != null &&
               multiplayerPopupBackdrop != null &&
               multiplayerPopupPanel != null &&
               primarySoloButton != null &&
               newSoloButton != null &&
               openMultiplayerButton != null &&
               closeMultiplayerButton != null &&
               hostButton != null &&
               continueSessionButton != null &&
               joinButton != null &&
               addressInput != null &&
               portInput != null &&
               statusText != null &&
               discoveryText != null;
    }
}
