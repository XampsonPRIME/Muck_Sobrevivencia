using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DebugCommandChat : MonoBehaviour
{
    const int MaxHistoryLines = 8;
    const int MaxSubmittedCommandHistory = 50;
    const float SubmittedHistoryVisibleSeconds = 4f;
    const int DebugUnarmedDamage = 1000;
    const float MerchantTeleportDistance = 2.4f;
    static readonly Vector3 VillageMerchantLocalPosition = new Vector3(23.2f, 1.65f, 63f);
    static readonly Quaternion VillageMerchantFallbackRotation = Quaternion.Euler(0f, 90f, 0f);

    static DebugCommandChat instance;

    Canvas canvas;
    GameObject panel;
    GameObject inputRoot;
    TMP_InputField inputField;
    TextMeshProUGUI historyText;
    Coroutine hideHistoryRoutine;
    PlayerMovement player;
    Inventory inventory;
    Hotbar hotbar;
    readonly List<string> history = new List<string>();
    readonly List<string> submittedCommandHistory = new List<string>();
    readonly Dictionary<string, Item> runtimeItems = new Dictionary<string, Item>();
    int submittedCommandHistoryCursor;
    string submittedCommandDraft = string.Empty;
    bool debugGodCombatModeActive;
    int savedUnarmedDamage = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (instance != null || FindFirstObjectByType<DebugCommandChat>() != null)
            return;

        GameObject chatObject = new GameObject("DebugCommandChat");
        DontDestroyOnLoad(chatObject);
        chatObject.AddComponent<DebugCommandChat>();
    }

    public static void AddSystemMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        DebugCommandChat chat = instance != null ? instance : FindFirstObjectByType<DebugCommandChat>();
        if (chat == null)
            return;

        chat.AddHistory(message);
        chat.ShowHistoryBriefly();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUI();
        SetOpen(false);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!GameState.IsDebugChatOpen)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame && CanOpenChat())
                SetOpen(true);

            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetOpen(false);
            return;
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            ShowPreviousSubmittedCommand();
            return;
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            ShowNextSubmittedCommand();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
            SubmitInput();
    }

    bool CanOpenChat()
    {
        return (Debug.isDebugBuild || Application.isEditor) &&
               !GameState.IsPaused &&
               !GameState.IsInLobby &&
               !GameState.IsWorldLoading &&
               !GameState.IsPowerSelectionOpen &&
               !GameState.IsPlayerDead &&
               !GameState.IsInventoryOpen &&
               !GameState.IsBestiaryOpen &&
               !GameState.IsQuestJournalOpen &&
               !GameState.IsVendorOpen &&
               !GameState.IsCraftingOpen;
    }

    void SetOpen(bool open)
    {
        GameState.IsDebugChatOpen = open;

        if (panel != null)
            panel.SetActive(open);

        if (open)
        {
            if (hideHistoryRoutine != null)
            {
                StopCoroutine(hideHistoryRoutine);
                hideHistoryRoutine = null;
            }

            ResolveReferences();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (inputRoot != null)
                inputRoot.SetActive(true);
            inputField.text = string.Empty;
            inputField.interactable = true;
            ResetSubmittedCommandNavigation();
            inputField.ActivateInputField();
            EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
            AddHistory("Digite /help para ver os comandos.");
        }
        else
        {
            if (inputField != null)
            {
                inputField.DeactivateInputField();
                inputField.interactable = false;
            }

            if (inputRoot != null)
                inputRoot.SetActive(false);

            if (!GameState.IsInventoryOpen && !GameState.IsBestiaryOpen && !GameState.IsQuestJournalOpen && !GameState.IsVendorOpen && !GameState.IsCraftingOpen && !GameState.IsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void SubmitInput()
    {
        string command = inputField != null ? inputField.text.Trim() : string.Empty;
        inputField.text = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            inputField.ActivateInputField();
            return;
        }

        AddHistory($"> {command}");
        AddSubmittedCommand(command);
        ExecuteCommand(command);
        CloseAfterSubmit();
    }

    void AddSubmittedCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        if (submittedCommandHistory.Count > 0 && submittedCommandHistory[submittedCommandHistory.Count - 1] == command)
        {
            ResetSubmittedCommandNavigation();
            return;
        }

        if (submittedCommandHistory.Count >= MaxSubmittedCommandHistory)
            submittedCommandHistory.RemoveAt(0);

        submittedCommandHistory.Add(command);
        ResetSubmittedCommandNavigation();
    }

    void ShowPreviousSubmittedCommand()
    {
        if (inputField == null || submittedCommandHistory.Count == 0)
            return;

        if (submittedCommandHistoryCursor >= submittedCommandHistory.Count)
            submittedCommandDraft = inputField.text;

        submittedCommandHistoryCursor = Mathf.Max(0, submittedCommandHistoryCursor - 1);
        SetInputTextFromHistory(submittedCommandHistory[submittedCommandHistoryCursor]);
    }

    void ShowNextSubmittedCommand()
    {
        if (inputField == null || submittedCommandHistory.Count == 0)
            return;

        if (submittedCommandHistoryCursor >= submittedCommandHistory.Count)
            return;

        submittedCommandHistoryCursor++;
        if (submittedCommandHistoryCursor >= submittedCommandHistory.Count)
            SetInputTextFromHistory(submittedCommandDraft);
        else
            SetInputTextFromHistory(submittedCommandHistory[submittedCommandHistoryCursor]);
    }

    void ResetSubmittedCommandNavigation()
    {
        submittedCommandHistoryCursor = submittedCommandHistory.Count;
        submittedCommandDraft = string.Empty;
    }

    void SetInputTextFromHistory(string value)
    {
        if (inputField == null)
            return;

        inputField.text = value ?? string.Empty;
        inputField.caretPosition = inputField.text.Length;
        inputField.stringPosition = inputField.text.Length;
        inputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
    }

    void CloseAfterSubmit()
    {
        GameState.IsDebugChatOpen = false;

        if (inputField != null)
        {
            inputField.DeactivateInputField();
            inputField.interactable = false;
        }

        if (inputRoot != null)
            inputRoot.SetActive(false);

        if (!GameState.IsInventoryOpen && !GameState.IsBestiaryOpen && !GameState.IsQuestJournalOpen && !GameState.IsVendorOpen && !GameState.IsCraftingOpen && !GameState.IsPaused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (hideHistoryRoutine != null)
            StopCoroutine(hideHistoryRoutine);

        hideHistoryRoutine = StartCoroutine(HideHistoryAfterDelay());
    }

    void ShowHistoryBriefly()
    {
        if (GameState.IsDebugChatOpen)
            return;

        if (panel != null)
            panel.SetActive(true);

        if (inputRoot != null)
            inputRoot.SetActive(false);

        if (hideHistoryRoutine != null)
            StopCoroutine(hideHistoryRoutine);

        hideHistoryRoutine = StartCoroutine(HideHistoryAfterDelay());
    }

    IEnumerator HideHistoryAfterDelay()
    {
        yield return new WaitForSecondsRealtime(SubmittedHistoryVisibleSeconds);

        if (!GameState.IsDebugChatOpen && panel != null)
            panel.SetActive(false);

        hideHistoryRoutine = null;
    }

    void ExecuteCommand(string rawCommand)
    {
        if (!rawCommand.StartsWith("/", StringComparison.Ordinal))
        {
            AddHistory("Comandos precisam comecar com /.");
            return;
        }

        string body = rawCommand.Substring(1).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            AddHistory("Digite /help para ver os comandos.");
            return;
        }

        string[] parts = body.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string command = Normalize(parts[0]);

        switch (command)
        {
            case "help":
            case "ajuda":
                ShowHelp();
                break;
            case "give":
            case "item":
                HandleGive(body.Substring(parts[0].Length).Trim());
                break;
            case "heal":
            case "curar":
                HandleHeal();
                break;
            case "full":
            case "cheio":
                HandleFull();
                break;
            case "level":
            case "lvl":
            case "nivel":
            case "levelup":
            case "upar":
                HandleLevel(command, parts);
                break;
            case "xp":
            case "exp":
                HandleXp(parts);
                break;
            case "habilidades":
            case "skills":
            case "unlockskills":
            case "liberarhabilidades":
                HandleUnlockAbilities();
                break;
            case "god":
            case "imortal":
            case "deus":
            case "testmode":
            case "testecombate":
                HandleGodCombatMode(parts);
                break;
            case "time":
            case "hora":
                HandleTime(parts);
                break;
            case "tp":
            case "teleport":
                HandleTeleport(parts);
                break;
            case "comerciante":
            case "mercador":
            case "vendedor":
            case "vendor":
            case "loja":
                TeleportToMerchant();
                break;
            case "spawn":
            case "summon":
                HandleSpawn(parts);
                break;
            case "golem":
            case "golemterra":
                SpawnEarthGolem();
                break;
            case "kaeltor":
            case "bossdemo":
                SpawnKaelTor();
                break;
            case "totem":
            case "totemancestral":
                SpawnAncestralTotem(AncestralTotemTier.Common);
                break;
            case "clearinventory":
            case "clearinv":
            case "limparinventario":
                HandleClearInventory();
                break;
            case "pos":
            case "position":
                HandlePosition();
                break;
            case "close":
            case "fechar":
                SetOpen(false);
                break;
            default:
                AddHistory($"Comando nao encontrado: /{parts[0]}");
                break;
        }
    }

    void ShowHelp()
    {
        AddHistory("/give Ferro Bruto 10 | /give Trigo 4 | /give Corda 1");
        AddHistory("/heal | /full | /time day | /time night | /time 14");
        AddHistory("/level 15 | /levelup | /levelup 3 | /xp 1000 | /habilidades");
        AddHistory("/god on | /god off  (imortal + soco 1000)");
        AddHistory("/spawn golem | /spawn kaeltor | /spawn totem 1 | /golem | /tp village | /comerciante | /clearinventory | /pos | /close");
    }

    void HandleGive(string arguments)
    {
        ResolveReferences();

        if (inventory == null)
        {
            AddHistory("Inventario do player nao encontrado.");
            return;
        }

        if (string.IsNullOrWhiteSpace(arguments))
        {
            AddHistory("Uso: /give Nome do Item quantidade");
            return;
        }

        string itemName = arguments.Trim();
        int amount = 1;
        int lastSpace = itemName.LastIndexOf(' ');
        if (lastSpace > 0 && int.TryParse(itemName.Substring(lastSpace + 1), out int parsedAmount))
        {
            amount = Mathf.Max(1, parsedAmount);
            itemName = itemName.Substring(0, lastSpace).Trim();
        }

        Item item = ResolveItem(itemName);
        if (item == null)
        {
            AddHistory($"Item nao encontrado: {itemName}");
            return;
        }

        inventory.AddItem(item.itemName, amount, item);
        hotbar?.TryAddInventoryItem(new InventoryItem(item.itemName, amount, item));
        RefreshInventoryUI();

        string message = $"+{amount} {item.itemName}";
        MessageSystem.Instance?.ShowMessage(message);
        AddHistory(message);
    }

    void HandleHeal()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        player.Heal(player.maxHealth);
        AddHistory("Vida restaurada.");
    }

    void HandleFull()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        player.currentHealth = player.maxHealth;
        player.currentStamina = player.maxStamina;
        player.currentHunger = player.maxHunger;
        player.currentThirst = player.maxThirst;
        AddHistory("Vida, stamina, fome e sede restauradas.");
    }

    void HandleLevel(string command, string[] parts)
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        PlayerProgression progression = player.GetComponent<PlayerProgression>() ??
                                        player.gameObject.AddComponent<PlayerProgression>();

        bool relativeLevelUp = command == "levelup" || command == "upar";
        int targetLevel = progression.currentLevel + 1;

        if (parts.Length >= 2)
        {
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
            {
                AddHistory(relativeLevelUp ? "Uso: /levelup 3" : "Uso: /level 10");
                return;
            }

            targetLevel = relativeLevelUp
                ? progression.currentLevel + Mathf.Max(1, parsedValue)
                : parsedValue;
        }

        targetLevel = Mathf.Clamp(targetLevel, 1, progression.maxLevel);
        if (targetLevel <= progression.currentLevel)
        {
            AddHistory($"Nivel atual ja e {progression.currentLevel}.");
            return;
        }

        int targetXp = progression.GetXpThresholdForLevel(targetLevel);
        int xpToAdd = Mathf.Max(0, targetXp - progression.currentXp);
        if (xpToAdd <= 0)
            xpToAdd = 1;

        progression.AddExperience(xpToAdd, "Debug");
        player.currentHealth = player.maxHealth;
        player.currentStamina = player.maxStamina;

        AddHistory($"Nivel debug: {progression.currentLevel}.");
        MessageSystem.Instance?.ShowMessage($"Nivel debug: {progression.currentLevel}.");
    }

    void HandleUnlockAbilities()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        PlayerProgression progression = player.GetComponent<PlayerProgression>() ??
                                        player.gameObject.AddComponent<PlayerProgression>();

        int targetLevel = Mathf.Clamp(GetHighestAbilityUnlockLevel(player), 1, progression.maxLevel);
        if (targetLevel <= progression.currentLevel)
        {
            AddHistory($"Habilidades ja liberadas no nivel {progression.currentLevel}.");
            return;
        }

        int targetXp = progression.GetXpThresholdForLevel(targetLevel);
        int xpToAdd = Mathf.Max(1, targetXp - progression.currentXp);
        progression.AddExperience(xpToAdd, "Debug habilidades");
        player.currentHealth = player.maxHealth;
        player.currentStamina = player.maxStamina;

        AddHistory($"Habilidades liberadas ate o nivel {progression.currentLevel}.");
        MessageSystem.Instance?.ShowMessage($"Habilidades liberadas: nivel {progression.currentLevel}.");
    }

    int GetHighestAbilityUnlockLevel(PlayerMovement targetPlayer)
    {
        BearerPowerService powerService = targetPlayer != null ? targetPlayer.GetComponent<BearerPowerService>() : null;
        BearerPowerDefinition currentDefinition = powerService != null ? powerService.CurrentDefinition : null;
        if (currentDefinition != null)
            return GetHighestAbilityUnlockLevel(currentDefinition);

        int highest = 1;
        IReadOnlyList<BearerPowerDefinition> definitions = BearerPowerCatalog.All;
        for (int i = 0; i < definitions.Count; i++)
            highest = Mathf.Max(highest, GetHighestAbilityUnlockLevel(definitions[i]));

        return highest;
    }

    int GetHighestAbilityUnlockLevel(BearerPowerDefinition definition)
    {
        if (definition?.abilities == null)
            return 1;

        int highest = 1;
        for (int i = 0; i < definition.abilities.Count; i++)
        {
            BearerPowerAbilityDefinition ability = definition.abilities[i];
            if (ability != null)
                highest = Mathf.Max(highest, ability.unlockLevel);
        }

        return highest;
    }

    void HandleXp(string[] parts)
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
        {
            AddHistory("Uso: /xp 1000");
            return;
        }

        PlayerProgression progression = player.GetComponent<PlayerProgression>() ??
                                        player.gameObject.AddComponent<PlayerProgression>();
        amount = Mathf.Max(1, amount);
        progression.AddExperience(amount, "Debug");
        AddHistory($"+{amount} XP debug. Nivel {progression.currentLevel}.");
    }

    void HandleGodCombatMode(string[] parts)
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        PlayerInteraction playerInteraction = player.GetComponent<PlayerInteraction>();
        if (playerInteraction == null)
        {
            AddHistory("Interacao do player nao encontrada.");
            return;
        }

        bool enable = !debugGodCombatModeActive;
        if (parts.Length >= 2)
        {
            string value = Normalize(parts[1]);
            if (value == "on" || value == "1" || value == "true" || value == "ligar" || value == "sim")
                enable = true;
            else if (value == "off" || value == "0" || value == "false" || value == "desligar" || value == "nao")
                enable = false;
        }

        if (enable)
        {
            if (!debugGodCombatModeActive || savedUnarmedDamage < 0)
                savedUnarmedDamage = playerInteraction.unarmedDamage;

            debugGodCombatModeActive = true;
            player.debugImmortal = true;
            playerInteraction.unarmedDamage = DebugUnarmedDamage;
            player.currentHealth = player.maxHealth;

            AddHistory("Modo teste ligado: imortalidade + soco 1000.");
            MessageSystem.Instance?.ShowMessage("Modo teste ligado: imortal + soco 1000.");
            return;
        }

        debugGodCombatModeActive = false;
        player.debugImmortal = false;
        if (savedUnarmedDamage >= 0)
            playerInteraction.unarmedDamage = savedUnarmedDamage;

        savedUnarmedDamage = -1;
        AddHistory("Modo teste desligado.");
        MessageSystem.Instance?.ShowMessage("Modo teste desligado.");
    }

    void HandleTime(string[] parts)
    {
        DayNightCycle cycle = DayNightCycle.Instance ?? FindFirstObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            AddHistory("Ciclo de dia/noite nao encontrado.");
            return;
        }

        if (parts.Length < 2)
        {
            AddHistory("Uso: /time day, /time night ou /time 14");
            return;
        }

        string value = Normalize(parts[1]);
        float hour;

        if (value == "day" || value == "dia")
            hour = 8f;
        else if (value == "night" || value == "noite")
            hour = 20f;
        else if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out hour))
        {
            AddHistory("Hora invalida. Exemplo: /time 14");
            return;
        }

        hour = Mathf.Repeat(hour, 24f);
        cycle.LoadState(cycle.CurrentDay, hour / 24f);
        AddHistory($"Hora ajustada para {hour:00}:00.");
    }

    void HandleTeleport(string[] parts)
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        if (parts.Length < 2)
        {
            AddHistory("Uso: /tp village ou /tp comerciante");
            return;
        }

        string destination = Normalize(parts[1]);
        if (IsMerchantDestination(destination))
        {
            TeleportToMerchant();
            return;
        }

        if (destination != "village" && destination != "vila")
        {
            AddHistory($"Destino nao conhecido: {parts[1]}");
            return;
        }

        Transform target = FindVillageTarget();
        if (target == null)
        {
            AddHistory("Vila nao encontrada nessa cena.");
            return;
        }

        Vector3 desiredPosition = target.position + Vector3.up * 0.5f + target.right * 2f;
        if (!player.WarpToSafePosition(desiredPosition, player.transform.rotation))
        {
            AddHistory("Nao consegui encontrar um ponto seguro perto da vila.");
            return;
        }

        AddHistory("Teleportado para a vila.");
    }

    bool IsMerchantDestination(string destination)
    {
        return destination == "comerciante" ||
               destination == "mercador" ||
               destination == "vendedor" ||
               destination == "vendor" ||
               destination == "loja";
    }

    void TeleportToMerchant()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        VillageCraftingSetup village = FindFirstObjectByType<VillageCraftingSetup>();
        if (village != null)
            village.RequestGroundAlign();

        Transform target = FindMerchantTarget();
        if (target != null)
        {
            if (!TryTeleportNearTarget(target, MerchantTeleportDistance, true, out bool usedAirFallback))
            {
                AddHistory("Nao consegui encontrar um ponto perto do comerciante.");
                return;
            }

            AddHistory(usedAirFallback
                ? "Teleportado para cima do comerciante; aguarde o terreno carregar."
                : "Teleportado para o comerciante.");
            if (village != null)
                village.RequestGroundAlign();
            MessageSystem.Instance?.ShowMessage("Teleportado para o comerciante.");
            return;
        }

        if (TryGetMerchantFallbackPose(out Vector3 fallbackPosition, out Quaternion fallbackRotation))
        {
            if (!TryTeleportNearPosition(fallbackPosition, fallbackRotation, MerchantTeleportDistance, true, out bool usedAirFallback))
            {
                AddHistory("Nao consegui encontrar um ponto perto do comerciante.");
                return;
            }

            AddHistory("Teleportado para a posicao do comerciante da vila.");
            if (usedAirFallback)
                AddHistory("Aguarde o terreno carregar ao redor da vila.");
            if (village != null)
                village.RequestGroundAlign();
            MessageSystem.Instance?.ShowMessage("Teleportado para o comerciante.");
            return;
        }

        AddHistory("Comerciante nao encontrado nessa cena.");
    }

    bool TryTeleportNearTarget(Transform target, float distance, bool allowAirFallback, out bool usedAirFallback)
    {
        usedAirFallback = false;
        if (target == null || player == null)
            return false;

        return TryTeleportNearPosition(target.position, target.rotation, distance, allowAirFallback, out usedAirFallback);
    }

    bool TryTeleportNearPosition(Vector3 targetPosition, Quaternion targetRotation, float distance, bool allowAirFallback, out bool usedAirFallback)
    {
        usedAirFallback = false;
        if (player == null)
            return false;

        Vector3 forward = FlattenDirection(targetRotation * Vector3.forward, Vector3.forward);
        Vector3 right = FlattenDirection(targetRotation * Vector3.right, Vector3.right);

        Vector3[] offsets =
        {
            forward * distance,
            right * distance,
            -right * distance,
            -forward * distance,
            (forward + right).normalized * distance,
            (forward - right).normalized * distance,
            (-forward + right).normalized * distance,
            (-forward - right).normalized * distance
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 desiredPosition = targetPosition + offsets[i];
            Vector3 lookDirection = targetPosition - desiredPosition;
            lookDirection.y = 0f;
            Quaternion rotation = lookDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : player.transform.rotation;

            if (TryResolveTeleportGround(desiredPosition, out Vector3 groundedPosition))
            {
                player.TeleportExact(groundedPosition, rotation);
                return true;
            }
        }

        if (!allowAirFallback)
            return false;

        Vector3 airPosition = targetPosition + forward * distance;
        airPosition.y = Mathf.Max(airPosition.y + 18f, DemoWorldProgression.FreshSpawnHeight);
        Vector3 airLookDirection = targetPosition - airPosition;
        airLookDirection.y = 0f;
        Quaternion airRotation = airLookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(airLookDirection.normalized, Vector3.up)
            : player.transform.rotation;

        player.TeleportExact(airPosition, airRotation);
        usedAirFallback = true;
        return true;
    }

    bool TryResolveTeleportGround(Vector3 desiredPosition, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 80f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 180f, ~0, QueryTriggerInteraction.Ignore);
        groundedPosition = desiredPosition;

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool foundFallback = false;
        Vector3 fallbackGround = desiredPosition;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null || hit.normal.y < 0.35f)
                continue;

            if (IsBlockedTeleportSurface(collider))
                continue;

            Vector3 candidate = hit.point + Vector3.up * GetPlayerGroundLift();
            if (collider.GetComponentInParent<TerrainChunk>() != null ||
                collider.GetComponentInParent<ProceduralTerrain>() != null)
            {
                groundedPosition = candidate;
                return true;
            }

            if (!foundFallback)
            {
                foundFallback = true;
                fallbackGround = candidate;
            }
        }

        if (foundFallback)
        {
            groundedPosition = fallbackGround;
            return true;
        }

        return false;
    }

    bool IsBlockedTeleportSurface(Collider collider)
    {
        return collider.GetComponentInParent<PlayerMovement>() != null ||
               collider.GetComponentInParent<RemotePlayerReplica>() != null ||
               collider.GetComponentInParent<VendorShop>() != null ||
               collider.GetComponentInParent<CraftingNpc>() != null ||
               collider.GetComponentInParent<VillageChest>() != null ||
               collider.GetComponentInParent<CraftingBench>() != null ||
               collider.GetComponentInParent<DistantMountains>() != null ||
               collider.GetComponentInParent<AncestralTotem>() != null ||
               collider.GetComponentInParent<EarthGolem>() != null ||
               collider.GetComponentInParent<WildBoar>() != null ||
               collider.GetComponentInParent<WildChicken>() != null ||
               collider.GetComponentInParent<Cow>() != null ||
               collider.GetComponentInParent<MiniKrug>() != null ||
               collider.GetComponentInParent<BossEnemy>() != null ||
               collider.GetComponentInParent<KaelTorGuardian>() != null;
    }

    float GetPlayerGroundLift()
    {
        CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
        if (controller == null)
            return 1.08f;

        float bottomOffset = controller.center.y - controller.height * 0.5f;
        return Mathf.Max(player.spawnGroundPadding, -bottomOffset + player.spawnGroundPadding);
    }

    Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }

    void HandleSpawn(string[] parts)
    {
        if (parts.Length < 2)
        {
            AddHistory("Uso: /spawn golem");
            return;
        }

        string target = Normalize(parts[1]);
        if (target == "golem" || target == "golemterra" || target == "golem de terra")
        {
            SpawnEarthGolem();
            return;
        }

        if (target == "kaeltor" || target == "kael tor" || target == "bossdemo" || target == "boss demo")
        {
            SpawnKaelTor();
            return;
        }

        if (target == "totem" || target == "totemancestral" || target == "totem ancestral")
        {
            SpawnAncestralTotem(ParseTotemTier(parts));
            return;
        }

        AddHistory($"Spawn desconhecido: {parts[1]}");
    }

    AncestralTotemTier ParseTotemTier(string[] parts)
    {
        if (parts.Length < 3)
            return AncestralTotemTier.Common;

        string tierValue = Normalize(parts[2]);
        if (tierValue == "3" || tierValue == "lendario" || tierValue == "legendary")
            return AncestralTotemTier.Legendary;

        if (tierValue == "2" || tierValue == "raro" || tierValue == "rare")
            return AncestralTotemTier.Rare;

        return AncestralTotemTier.Common;
    }

    void SpawnEarthGolem()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        Vector3 desiredPosition = player.transform.position + forward.normalized * 7f;
        Vector3 spawnPosition = ResolveGroundedSpawnPosition(desiredPosition);

        GameObject golemObject = new GameObject("Golem de Terra Debug");
        golemObject.transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(-forward.normalized, Vector3.up));

        EarthGolem golem = golemObject.AddComponent<EarthGolem>();
        golem.patrolRadius = 6f;
        golem.SetSpawnData(null, spawnPosition);
        LanNetworkEntity.Ensure(golem, $"DebugEarthGolem|{DateTime.UtcNow.Ticks}");

        AddHistory("Golem de Terra spawnado.");
        MessageSystem.Instance?.ShowMessage("Golem de Terra spawnado.");
    }

    void SpawnKaelTor()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        if (!KaelTorSpawnUtility.TryFindArenaSpawnPosition(player, 24f, out Vector3 spawnPosition))
        {
            AddHistory("Nao encontrei chao valido para spawnar Kael'Tor perto do player.");
            MessageSystem.Instance?.ShowMessage("Sem chao valido para Kael'Tor.");
            return;
        }

        KaelTorArenaBuilder.Build(spawnPosition);

        GameObject bossObject = new GameObject("Kael'Tor Debug");
        bossObject.transform.SetPositionAndRotation(spawnPosition + Vector3.up * 0.05f, Quaternion.LookRotation(-forward.normalized, Vector3.up));
        bossObject.AddComponent<KaelTorGuardian>();

        AddHistory("Kael'Tor spawnado.");
        MessageSystem.Instance?.ShowMessage("Kael'Tor spawnado.");
    }

    void SpawnAncestralTotem(AncestralTotemTier tier)
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        Vector3 desiredPosition = player.transform.position + forward.normalized * 6f;
        Vector3 spawnPosition = ResolveGroundedSpawnPosition(desiredPosition);

        GameObject totemObject = new GameObject($"Totem Ancestral Debug {tier}");
        totemObject.transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(-forward.normalized, Vector3.up));

        AncestralTotem totem = totemObject.AddComponent<AncestralTotem>();
        totem.Configure(tier);

        AddHistory($"Totem Ancestral {tier} spawnado.");
        MessageSystem.Instance?.ShowMessage($"Totem Ancestral {tier} spawnado.");
    }

    Vector3 ResolveGroundedSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 24f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 72f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return desiredPosition;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || hits[i].normal.y < 0.35f)
                continue;

            if (collider.GetComponentInParent<PlayerMovement>() != null ||
                collider.GetComponentInParent<RemotePlayerReplica>() != null ||
                collider.GetComponentInParent<AncestralTotem>() != null ||
                collider.GetComponentInParent<EarthGolem>() != null ||
                collider.GetComponentInParent<WildBoar>() != null ||
                collider.GetComponentInParent<WildChicken>() != null ||
                collider.GetComponentInParent<Cow>() != null ||
                collider.GetComponentInParent<MiniKrug>() != null ||
                collider.GetComponentInParent<BossEnemy>() != null ||
                collider.GetComponentInParent<KaelTorGuardian>() != null)
                continue;

            return hits[i].point;
        }

        return desiredPosition;
    }

    void HandleClearInventory()
    {
        ResolveReferences();

        inventory?.ClearAll();
        hotbar?.ClearAll();
        RefreshInventoryUI();
        AddHistory("Inventario limpo.");
    }

    void HandlePosition()
    {
        ResolveReferences();

        if (player == null)
        {
            AddHistory("Player nao encontrado.");
            return;
        }

        Vector3 pos = player.transform.position;
        AddHistory($"Posicao: {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}");
    }

    Item ResolveItem(string requestedName)
    {
        string key = Normalize(requestedName);

        switch (key)
        {
            case "gold":
            case "ouro":
                return GoldItemRegistry.GetOrCreate();
            case "metal enferrujado":
            case "metal":
                return RustyMetalItemRegistry.GetOrCreate();
            case "ferro bruto":
            case "ferro":
                return IronItemRegistry.GetOrCreate();
            case "ferro refinado":
            case "barra de ferro":
                return RefinedIronItemRegistry.GetOrCreate();
            case "fornalha":
                return FurnaceItemRegistry.GetOrCreate();
            case "espada":
            case "espada enferrujada":
                return RustySwordItemRegistry.GetOrCreate();
            case "escudo":
            case "shield":
                return ShieldItemRegistry.GetOrCreate();
            case "trigo":
            case "wheat":
                return WheatItemRegistry.GetOrCreate();
            case "corda":
            case "rope":
                return RopeItemRegistry.GetOrCreate();
            case "arco":
            case "arco simples":
            case "bow":
            case "simple bow":
                return SimpleBowItemRegistry.GetOrCreate();
            case "pena":
            case "feather":
                return FeatherItemRegistry.GetOrCreate();
            case "carne crua":
            case "raw meat":
            case "raw chicken":
                return RawChickenMeatItemRegistry.GetOrCreate();
            case "carne de javali":
            case "boar meat":
                return BoarMeatItemRegistry.GetOrCreate();
            case "carne de vaca":
            case "cow meat":
            case "beef":
                return CowMeatItemRegistry.GetOrCreate();
            case "carne de galinha cozida":
            case "carne cozida de galinha":
            case "cooked chicken meat":
            case "chicken cooked meat":
                return CookedChickenMeatItemRegistry.GetOrCreate();
            case "carne de javali cozida":
            case "carne cozida de javali":
            case "cooked boar meat":
            case "boar cooked meat":
                return CookedBoarMeatItemRegistry.GetOrCreate();
            case "carne de vaca cozida":
            case "carne cozida de vaca":
            case "cooked cow meat":
            case "cow cooked meat":
            case "cooked beef":
                return CookedCowMeatItemRegistry.GetOrCreate();
            case "carne cozida":
            case "cooked meat":
            case "cozida":
                return CookedMeatItemRegistry.GetOrCreate();
            case "flecha":
            case "flechas":
            case "arrow":
            case "arrows":
                return ArrowItemRegistry.GetOrCreate();
            case "fragmento de pedra":
            case "fragmentos de pedra":
            case "stone fragment":
            case "stone fragments":
                return StoneFragmentItemRegistry.GetOrCreate();
            case "musgo resiliente":
            case "moss":
                return ResilientMossItemRegistry.GetOrCreate();
            case "nucleo de terra":
            case "nucleo":
            case "earth core":
                return EarthCoreItemRegistry.GetOrCreate();
            case "nucleo ancestral":
            case "ancestral core":
                return AncestralCoreItemRegistry.GetOrCreate();
            case "fragmento do primeiro artefato":
            case "fragmento artefato":
            case "artifact fragment":
                return FirstArtifactFragmentItemRegistry.GetOrCreate();
            case "trofeu de kaeltor":
            case "trofeu kaeltor":
            case "kaeltor trophy":
                return KaelTorTrophyItemRegistry.GetOrCreate();
            case "primeiro artefato":
            case "first artifact":
                return FirstArtifactItemRegistry.GetOrCreate();
            case "machado":
            case "axe":
                return LoadResourceItem("VendorItems/Axe") ?? LoadResourceItem("Weapons/Axe") ?? GetRuntimeItem("Machado", ItemType.Tool, ToolType.Axe, 2);
            case "picareta":
            case "pickaxe":
            case "axepick":
                return LoadResourceItem("VendorItems/Axepick") ?? LoadResourceItem("Weapons/Axepick") ?? GetRuntimeItem("Picareta", ItemType.Tool, ToolType.Pickaxe, 2);
            case "graveto":
            case "gravetos":
                return StickResourceItemRegistry.GetOrCreate();
            case "pedra":
            case "pedras":
                return StoneResourceItemRegistry.GetOrCreate();
            case "madeira de carvalho":
            case "carvalho":
            case "oak wood":
                return OakWoodItemRegistry.GetOrCreate();
            case "madeira":
            case "madeiras":
                return GetRuntimeItem("Madeira", ItemType.Resource, ToolType.None, 0);
            case "carvao":
            case "carvao vegetal":
                return GetRuntimeItem("Carvao", ItemType.Resource, ToolType.None, 0);
            default:
                return FindExistingItem(requestedName) ?? GetRuntimeItem(requestedName, ItemType.Resource, ToolType.None, 0);
        }
    }

    Item LoadResourceItem(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        return prefab != null ? prefab.GetComponent<Item>() : null;
    }

    Item FindExistingItem(string itemName)
    {
        Item[] sceneItems = FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneItems.Length; i++)
        {
            Item item = sceneItems[i];
            if (item != null && string.Equals(Normalize(item.itemName), Normalize(itemName), StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    Item GetRuntimeItem(string itemName, ItemType itemType, ToolType toolType, int toolDamage)
    {
        string key = Normalize(itemName);
        if (runtimeItems.TryGetValue(key, out Item cachedItem) && cachedItem != null)
            return cachedItem;

        GameObject itemObject = new GameObject($"{SanitizeName(itemName)}DebugItemData");
        DontDestroyOnLoad(itemObject);

        Item item = itemObject.AddComponent<Item>();
        item.itemName = itemName;
        item.itemType = itemType;
        item.toolType = toolType;
        item.toolDamage = toolDamage;
        item.icon = CreateFallbackSprite(itemName);
        runtimeItems[key] = item;
        return item;
    }

    Sprite CreateFallbackSprite(string itemName)
    {
        Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color color = ColorForItem(itemName);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool inside = x >= 4 && x <= 19 && y >= 5 && y <= 18;
                texture.SetPixel(x, y, inside ? color : clear);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        sprite.name = $"{SanitizeName(itemName)}DebugSprite";
        return sprite;
    }

    Color ColorForItem(string itemName)
    {
        string key = Normalize(itemName);
        if (key.Contains("pedra") || key.Contains("ferro"))
            return new Color(0.45f, 0.47f, 0.48f, 1f);
        if (key.Contains("madeira") || key.Contains("graveto"))
            return new Color(0.58f, 0.34f, 0.16f, 1f);
        if (key.Contains("carvao"))
            return new Color(0.08f, 0.08f, 0.08f, 1f);

        return new Color(0.85f, 0.68f, 0.24f, 1f);
    }

    Transform FindVillageTarget()
    {
        VillageCraftingSetup village = FindFirstObjectByType<VillageCraftingSetup>();
        if (village != null)
        {
            Transform chest = village.transform.Find("StarterRustyMetalChest");
            if (chest != null)
                return chest;

            Transform bench = village.transform.Find("CraftingBench");
            if (bench != null)
                return bench;

            return village.transform;
        }

        VillageChest chestComponent = FindFirstObjectByType<VillageChest>();
        if (chestComponent != null)
            return chestComponent.transform;

        CraftingBench benchComponent = FindFirstObjectByType<CraftingBench>();
        return benchComponent != null ? benchComponent.transform : null;
    }

    Transform FindMerchantTarget()
    {
        VillageCraftingSetup village = FindFirstObjectByType<VillageCraftingSetup>();
        if (village != null)
        {
            Transform villageVendor = village.transform.Find("VillageVendor");
            if (villageVendor != null)
                return villageVendor;
        }

        VendorShop[] vendors = FindObjectsByType<VendorShop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform fallback = null;
        for (int i = 0; i < vendors.Length; i++)
        {
            VendorShop vendor = vendors[i];
            if (vendor == null)
                continue;

            Transform vendorTransform = vendor.transform;
            if (fallback == null)
                fallback = vendorTransform;

            string objectName = Normalize(vendorTransform.name);
            string vendorName = Normalize(vendor.vendorName);
            if (objectName.Contains("villagevendor") ||
                objectName.Contains("merchant") ||
                objectName.Contains("mercador") ||
                objectName.Contains("comerciante") ||
                vendorName.Contains("mercador") ||
                vendorName.Contains("comerciante"))
                return vendorTransform;
        }

        GameObject namedVendor = GameObject.Find("VillageVendor");
        if (namedVendor != null)
            return namedVendor.transform;

        return fallback;
    }

    bool TryGetMerchantFallbackPose(out Vector3 position, out Quaternion rotation)
    {
        GameObject villageRoot = GameObject.Find("Village");
        if (villageRoot != null)
        {
            position = villageRoot.transform.TransformPoint(VillageMerchantLocalPosition);
            rotation = villageRoot.transform.rotation * VillageMerchantFallbackRotation;
            return true;
        }

        position = DemoWorldProgression.ResolveVillagePosition() + VillageMerchantLocalPosition;
        rotation = VillageMerchantFallbackRotation;
        return true;
    }

    void ResolveReferences()
    {
        if (player == null)
            player = LanMultiplayerManager.FindGameplayPlayer() ?? FindFirstObjectByType<PlayerMovement>();

        if (player != null)
        {
            if (inventory == null)
                inventory = player.GetComponent<Inventory>();

            if (hotbar == null)
                hotbar = player.GetComponent<Hotbar>();
        }

        if (hotbar == null)
            hotbar = FindFirstObjectByType<Hotbar>();
    }

    void RefreshInventoryUI()
    {
        InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (inventoryUI != null)
            inventoryUI.Refresh();
    }

    void AddHistory(string message)
    {
        if (history.Count >= MaxHistoryLines)
            history.RemoveAt(0);

        history.Add(message);

        if (historyText != null)
            historyText.text = string.Join("\n", history);
    }

    string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(normalized[i]);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(normalized[i]);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    string SanitizeName(string value)
    {
        string normalized = Normalize(value);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.Length > 0 ? builder.ToString() : "Item";
    }

    void BuildUI()
    {
        UIEventSystemUtility.EnsureSingleEventSystem();

        GameObject canvasObject = new GameObject("DebugCommandChatCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = CreatePanel("Panel", canvasObject.transform, new Color(0.04f, 0.05f, 0.055f, 0.9f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(42f, 42f);
        panelRect.sizeDelta = new Vector2(920f, 230f);

        historyText = CreateText("History", panel.transform, 18, FontStyles.Normal, TextAlignmentOptions.BottomLeft, string.Empty);
        RectTransform historyRect = historyText.GetComponent<RectTransform>();
        historyRect.anchorMin = new Vector2(0f, 0f);
        historyRect.anchorMax = new Vector2(1f, 1f);
        historyRect.offsetMin = new Vector2(18f, 58f);
        historyRect.offsetMax = new Vector2(-18f, -18f);
        historyText.color = new Color(0.88f, 0.92f, 0.9f, 1f);

        inputRoot = CreatePanel("InputRoot", panel.transform, new Color(0.11f, 0.12f, 0.13f, 1f));
        RectTransform inputRect = inputRoot.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(16f, 14f);
        inputRect.offsetMax = new Vector2(-16f, 50f);

        inputField = inputRoot.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.textViewport = inputRoot.GetComponent<RectTransform>();

        TextMeshProUGUI text = CreateText("Text", inputRoot.transform, 20, FontStyles.Normal, TextAlignmentOptions.Left, string.Empty);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 2f);
        textRect.offsetMax = new Vector2(-14f, -2f);
        text.color = Color.white;

        TextMeshProUGUI placeholder = CreateText("Placeholder", inputRoot.transform, 20, FontStyles.Italic, TextAlignmentOptions.Left, "Digite /help");
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = textRect.offsetMin;
        placeholderRect.offsetMax = textRect.offsetMax;
        placeholder.color = new Color(0.7f, 0.72f, 0.72f, 0.8f);

        inputField.textComponent = text;
        inputField.placeholder = placeholder;
    }

    GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;

        return panelObject;
    }

    TextMeshProUGUI CreateText(string objectName, Transform parent, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, string text)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

}
