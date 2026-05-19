using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class CraftingNpc : MonoBehaviour
{
    [SerializeField] string professionName = "Profissão";
    [SerializeField] string proximityMessage = "Nessa bancada voce pode criar os itens que esse mundo tem a oferecer.";
    [SerializeField] float talkDistance = 4f;
    [SerializeField] float messageCooldown = 7f;
    [SerializeField] Vector3 nameOffset = new Vector3(0f, 1.35f, 0f);

    TextMeshPro nameText;
    float nextMessageTime;

    void Awake()
    {
        EnsureNameLabel();
    }

    void Update()
    {
        UpdateNameLabelFacing();
        TryShowProximityMessage();
    }

    public void Configure(string npcProfessionName, string npcProximityMessage)
    {
        if (!string.IsNullOrWhiteSpace(npcProfessionName))
            professionName = npcProfessionName;

        if (!string.IsNullOrWhiteSpace(npcProximityMessage))
            proximityMessage = npcProximityMessage;

        if (nameText != null)
            nameText.text = professionName;
    }

    void TryShowProximityMessage()
    {
        if (GameState.IsPaused || GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsInventoryOpen || GameState.IsVendorOpen || GameState.IsCraftingOpen)
            return;

        if (Time.time < nextMessageTime)
            return;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null)
            return;

        if (Vector3.Distance(transform.position, player.transform.position) > talkDistance)
            return;

        nextMessageTime = Time.time + messageCooldown;
        MessageSystem.Instance?.ShowMessage(proximityMessage);
    }

    void EnsureNameLabel()
    {
        Transform existing = transform.Find("NameLabel");
        if (existing != null)
        {
            nameText = existing.GetComponent<TextMeshPro>();
            if (nameText != null)
                nameText.text = professionName;

            return;
        }

        GameObject label = new GameObject("NameLabel");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = nameOffset;

        nameText = label.AddComponent<TextMeshPro>();
        nameText.text = professionName;
        nameText.fontSize = 2.8f;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = new Color(1f, 0.94f, 0.55f, 1f);
        nameText.outlineWidth = 0.18f;
        nameText.outlineColor = Color.black;
        nameText.rectTransform.sizeDelta = new Vector2(6f, 1.1f);
    }

    void UpdateNameLabelFacing()
    {
        if (nameText == null || Camera.main == null)
            return;

        Transform labelTransform = nameText.transform;
        labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - Camera.main.transform.position);
    }
}
