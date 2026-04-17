using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    const string OpenInventoryClipPath = "Audio/UI/Abrindo_inventario";
    const string CloseInventoryClipPath = "Audio/UI/Fechando_inventario";
    const float RuntimeInventorySoundVolume = 0.08f;
    const float OpenInventorySoundStartOffset = 0.9f;
    const float CloseInventorySoundStartOffset = 1.2f;

    public GameObject panel;
    public Transform content;
    public GameObject slotPrefab;
    public AudioClip openInventorySound;
    public AudioClip closeInventorySound;
    [Range(0f, 1f)] public float inventorySoundVolume = RuntimeInventorySoundVolume;

    PlayerInteraction playerInteraction;
    PlayerMovement playerMovement;
    Inventory inventory;
    InputAction toggleInventoryAction;
    AudioSource uiAudioSource;

    void Awake()
    {
        toggleInventoryAction = new InputAction("ToggleInventory", binding: "<Keyboard>/i");
        inventorySoundVolume = RuntimeInventorySoundVolume;
        EnsureUiAudioSource();
        LoadDefaultSoundsIfNeeded();
        PreloadInventorySounds();
    }

    void OnEnable()
    {
        toggleInventoryAction.Enable();
    }

    void OnDisable()
    {
        toggleInventoryAction.Disable();
    }

    void Start()
    {
        ResolveReferences();
        LoadDefaultSoundsIfNeeded();

        if (panel != null)
            panel.SetActive(false);

        inventorySoundVolume = RuntimeInventorySoundVolume;
    }

    void Update()
    {
        ResolveReferences();

        if (GameState.IsPaused || GameState.IsInLobby || GameState.IsVendorOpen)
            return;

        if (toggleInventoryAction.WasPressedThisFrame())
            Toggle();
    }

    void Toggle()
    {
        if (panel == null || GameState.IsVendorOpen)
            return;

        ResolveReferences();

        bool isOpen = !panel.activeSelf;
        panel.SetActive(isOpen);

        GameState.IsInventoryOpen = isOpen;
        PlayToggleSound(isOpen);

        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerInteraction != null)
                playerInteraction.enabled = false;

            if (playerMovement != null)
                playerMovement.enabled = false;

            Refresh();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerInteraction != null)
                playerInteraction.enabled = true;

            if (playerMovement != null)
                playerMovement.enabled = true;
        }
    }

    void PlayToggleSound(bool isOpen)
    {
        LoadDefaultSoundsIfNeeded();
        EnsureUiAudioSource();

        if (uiAudioSource == null)
            return;

        AudioClip clip = isOpen ? openInventorySound : closeInventorySound;
        if (clip == null)
            return;

        uiAudioSource.Stop();
        uiAudioSource.clip = clip;
        uiAudioSource.volume = RuntimeInventorySoundVolume;
        float startOffset = isOpen ? OpenInventorySoundStartOffset : CloseInventorySoundStartOffset;
        uiAudioSource.time = Mathf.Min(startOffset, Mathf.Max(0f, clip.length - 0.01f));
        uiAudioSource.Play();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (inventory == null || content == null || slotPrefab == null)
        {
            Debug.LogError("InventoryUI nao configurado!");
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        foreach (var item in inventory.items)
        {
            GameObject slotGO = Instantiate(slotPrefab, content);
            InventorySlotUI slot = slotGO.GetComponent<InventorySlotUI>();

            if (slot != null)
                slot.Setup(item);
            else
                Debug.LogError("Slot sem InventorySlotUI!");
        }
    }

    void ResolveReferences()
    {
        PlayerMovement resolvedPlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (resolvedPlayer == null)
            return;

        if (playerMovement == resolvedPlayer && inventory != null && playerInteraction != null)
            return;

        playerMovement = resolvedPlayer;
        inventory = playerMovement.GetComponent<Inventory>();
        playerInteraction = playerMovement.GetComponent<PlayerInteraction>();
    }

    void LoadDefaultSoundsIfNeeded()
    {
        if (openInventorySound == null)
            openInventorySound = Resources.Load<AudioClip>(OpenInventoryClipPath);

        if (closeInventorySound == null)
            closeInventorySound = Resources.Load<AudioClip>(CloseInventoryClipPath);
    }

    void PreloadInventorySounds()
    {
        if (openInventorySound != null)
            openInventorySound.LoadAudioData();

        if (closeInventorySound != null)
            closeInventorySound.LoadAudioData();
    }

    void EnsureUiAudioSource()
    {
        if (uiAudioSource != null)
            return;

        uiAudioSource = GetComponent<AudioSource>();
        if (uiAudioSource == null)
            uiAudioSource = gameObject.AddComponent<AudioSource>();

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
    }
}
