using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Solo-world library and dialogs; all disk operations go through SaveGameManager.</summary>
public sealed class WorldLibraryUI : MonoBehaviour
{
    static readonly Color Gold = new Color(0.77f, 0.62f, 0.36f);
    static readonly Color Muted = new Color(0.65f, 0.72f, 0.71f);
    static readonly Color Danger = new Color(0.94f, 0.54f, 0.46f);
    SaveGameManager saves;
    Action onStarted;
    Action onClosed;
    RectTransform backdrop;
    RectTransform panel;
    TMP_Text error;
    TMP_InputField nameInput;
    Button submit;
    string page;
    bool busy;
    int lastActionFrame = -1;
    public bool IsOpen => backdrop != null && backdrop.gameObject.activeSelf;

    public void Initialize(Canvas canvas, SaveGameManager manager, Action started, Action closed)
    {
        saves = manager; onStarted = started; onClosed = closed;
        backdrop = new GameObject("World library", typeof(RectTransform)).GetComponent<RectTransform>();
        backdrop.SetParent(canvas.transform, false);
        backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.008f, 0.018f, 0.022f, 0.93f);
        panel = Box(backdrop, "World library panel", Vector2.zero, new Vector2(1160, 670), new Color(0.03f, 0.055f, 0.059f, 1));
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
        panel.anchoredPosition = Vector2.zero;
        backdrop.gameObject.SetActive(false);
    }

    void BeginPage(string next)
    {
        page = next; busy = false; nameInput = null; submit = null;
        backdrop.gameObject.SetActive(true); backdrop.SetAsLastSibling();
        foreach (Transform child in panel) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Box(panel, "Gold edge", Vector2.zero, new Vector2(1160, 2), Gold);
        Text(panel, "SEU LEGADO EM ELARION", new Vector2(46, -28), new Vector2(880, 26), 14, Gold).characterSpacing = 3;
        error = Text(panel, "", new Vector2(46, -591), new Vector2(1068, 60), 17, Danger);
        error.textWrappingMode = TextWrappingModes.Normal;
    }

    public void ShowLibrary()
    {
        BeginPage("library");
        Text(panel, "Continuar jornada", new Vector2(46, -69), new Vector2(850, 55), 40, LobbyPresentation.Ivory);
        ActionButton(panel, "Voltar", new Vector2(946, -55), new Vector2(168, 54), Close).Select();
        try
        {
            var worlds = saves.GetSoloWorlds();
            Text(panel, $"Escolha o mundo ao qual seu legado pertence.        {worlds.Count} / {SoloWorldStore.Capacity} mundos", new Vector2(46, -134), new Vector2(1068, 32), 19, Muted);
            for (int i = 0; i < SoloWorldStore.Capacity; i++)
            {
                int index = i;
                var card = Box(panel, "World slot " + i, new Vector2(46 + i * 362, -199), new Vector2(344, 366), new Color(0.053f, 0.084f, 0.087f, 1));
                Box(card, "Slot accent", Vector2.zero, new Vector2(344, 1), new Color(Gold.r, Gold.g, Gold.b, 0.55f));
                Text(card, "MUNDO 0" + (index + 1), new Vector2(24, -20), new Vector2(296, 25), 13, Gold).characterSpacing = 2;
                if (i >= worlds.Count)
                {
                    Text(card, "+", new Vector2(24, -69), new Vector2(296, 66), 56, Gold);
                    Text(card, "Uma história por começar", new Vector2(24, -152), new Vector2(296, 60), 24, LobbyPresentation.Ivory).textWrappingMode = TextWrappingModes.Normal;
                    Text(card, "Espaço disponível", new Vector2(24, -218), new Vector2(296, 30), 17, Muted);
                    ActionButton(card, "Criar mundo", new Vector2(24, -286), new Vector2(296, 54), ShowNew, true);
                    continue;
                }
                SoloWorldSave world = worlds[i];
                var title = Text(card, world.name, new Vector2(24, -66), new Vector2(296, 96), 29, LobbyPresentation.Ivory);
                title.textWrappingMode = TextWrappingModes.Normal; title.enableAutoSizing = true; title.fontSizeMin = 21; title.fontSizeMax = 29;
                string detail = world.unreadable ? "Save indisponível" : world.progress == null ? "Pronto para explorar" : "Dia " + Mathf.Max(1, world.progress.currentDay) + "  ·  Jornada solo";
                Text(card, detail, new Vector2(24, -170), new Vector2(296, 28), 18, world.unreadable ? Danger : Gold);
                Text(card, SavedDate(world), new Vector2(24, -207), new Vector2(296, 38), 15, Muted);
                var play = ActionButton(card, "Entrar no mundo", new Vector2(24, -260), new Vector2(296, 52), () => Continue(world.id), true);
                play.interactable = !world.unreadable;
                ActionButton(card, "Renomear", new Vector2(24, -323), new Vector2(141, 30), () => ShowRename(world)).interactable = !world.unreadable;
                ActionButton(card, "Excluir", new Vector2(179, -323), new Vector2(141, 30), () => ShowDelete(world), false, true);
            }
        }
        catch (Exception e) { ShowError(e); }
    }

    public void ShowNew()
    {
        try
        {
            if (saves.GetSoloWorlds().Count >= SoloWorldStore.Capacity)
            {
                ShowLibrary();
                error.text = "Seus 3 espaços estão ocupados. Exclua um mundo para começar outra jornada.";
                return;
            }
        }
        catch (Exception e) { ShowLibrary(); ShowError(e); return; }
        ShowNameDialog(null);
    }

    void ShowRename(SoloWorldSave world) { ShowNameDialog(world); }

    void ShowNameDialog(SoloWorldSave world)
    {
        BeginPage(world == null ? "new" : "rename");
        Text(panel, world == null ? "Toda jornada começa com um nome." : "Um novo nome. O mesmo legado.", new Vector2(46, -80), new Vector2(1068, 60), 38, LobbyPresentation.Ivory);
        Text(panel, world == null ? "Dê um nome ao mundo que você vai descobrir." : "O progresso e os itens deste mundo serão preservados.", new Vector2(46, -152), new Vector2(1068, 40), 21, Muted);
        Text(panel, "NOME DO MUNDO", new Vector2(46, -243), new Vector2(1068, 28), 15, Gold).characterSpacing = 2;
        nameInput = Input(panel, new Vector2(46, -286), new Vector2(1068, 76));
        nameInput.text = world != null ? world.name : "";
        var count = Text(panel, "", new Vector2(915, -378), new Vector2(199, 28), 16, Muted);
        count.alignment = TextAlignmentOptions.MidlineRight;
        Text(panel, "Até 32 caracteres. Cada mundo tem seu próprio progresso.", new Vector2(46, -378), new Vector2(840, 30), 18, Muted);
        ActionButton(panel, "Cancelar", new Vector2(46, -492), new Vector2(250, 62), ShowLibrary);
        submit = ActionButton(panel, world == null ? "Criar mundo e explorar" : "Salvar nome", new Vector2(754, -492), new Vector2(360, 62), () => CommitName(world), true);
        Action<string> validate = value =>
        {
            count.text = value.Length + " / " + SoloWorldStore.MaxNameLength;
            try
            {
                SoloWorldStore.ValidateName(value, saves.GetSoloWorlds(), world?.id);
                error.text = ""; submit.interactable = !busy;
            }
            catch (Exception e) { submit.interactable = false; error.text = value.Length == 0 ? "" : FriendlyError(e); }
        };
        nameInput.onValueChanged.AddListener(value => validate(value));
        nameInput.onSubmit.AddListener(_ => { if (submit != null && submit.interactable) CommitName(world); });
        validate(nameInput.text);
        nameInput.Select(); nameInput.ActivateInputField();
    }

    void CommitName(SoloWorldSave world)
    {
        if (busy || lastActionFrame == Time.frameCount) return;
        lastActionFrame = Time.frameCount; busy = true; submit.interactable = false;
        try
        {
            if (world != null) { saves.RenameSoloWorld(world.id, nameInput.text); ShowLibrary(); }
            else { saves.StartNewGame(nameInput.text); onStarted(); }
        }
        catch (Exception e) { busy = false; submit.interactable = true; ShowError(e); }
    }

    void Continue(string id)
    {
        if (busy) return;
        busy = true;
        try
        {
            if (saves.ContinueFromSave(id)) onStarted();
            else { busy = false; error.text = "Não foi possível carregar este mundo. Tente novamente."; }
        }
        catch (Exception e) { busy = false; ShowError(e); }
    }

    void ShowDelete(SoloWorldSave world)
    {
        BeginPage("delete");
        Text(panel, "Encerrar este legado?", new Vector2(46, -88), new Vector2(1068, 65), 42, LobbyPresentation.Ivory);
        var name = Text(panel, world.name, new Vector2(46, -200), new Vector2(1068, 96), 34, Gold);
        name.textWrappingMode = TextWrappingModes.Normal;
        Text(panel, "O mundo e todo o seu progresso serão excluídos.\nEsta ação não pode ser desfeita.", new Vector2(46, -322), new Vector2(1068, 86), 23, Muted).textWrappingMode = TextWrappingModes.Normal;
        ActionButton(panel, "Manter mundo", new Vector2(46, -492), new Vector2(300, 62), ShowLibrary, true).Select();
        ActionButton(panel, "Excluir mundo", new Vector2(794, -492), new Vector2(320, 62), () =>
        {
            if (busy) return;
            busy = true;
            try { saves.DeleteSoloWorld(world.id); ShowLibrary(); }
            catch (Exception e) { busy = false; ShowError(e); }
        }, false, true);
    }

    public void Close() { backdrop.gameObject.SetActive(false); onClosed?.Invoke(); }
    void Update()
    {
        if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (page == "library") Close(); else ShowLibrary();
    }
    void ShowError(Exception e) { error.text = FriendlyError(e); Debug.LogWarning("[WorldLibrary] " + e.Message); }
    static string FriendlyError(Exception e)
    {
        if (e is ArgumentException || e is InvalidOperationException) return e.Message;
        return "Não foi possível acessar o save. Verifique o espaço em disco e as permissões da pasta de saves.";
    }
    static string SavedDate(SoloWorldSave world)
    {
        if (world.unreadable) return "Você pode excluir este espaço.";
        if (world.recoveredFromBackup) return "Recuperado do backup anterior";
        if (!DateTime.TryParse(world.updatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)) return "";
        return (world.progress == null ? "Criado em " : "Salvo em ") + date.ToLocalTime().ToString("dd/MM/yyyy • HH:mm");
    }

    static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }
    static RectTransform Box(Transform p, string name, Vector2 pos, Vector2 size, Color color)
    {
        var r = Rect(p, name, pos, size); r.gameObject.AddComponent<Image>().color = color; return r;
    }
    static TextMeshProUGUI Text(Transform p, string value, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var r = Rect(p, "Label", pos, size); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = value; t.fontSize = fontSize; t.color = color; t.richText = false; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.alignment = TextAlignmentOptions.MidlineLeft; return t;
    }
    static Button ActionButton(Transform p, string label, Vector2 pos, Vector2 size, Action action, bool featured = false, bool danger = false)
    {
        var r = Box(p, label, pos, size, Color.white); var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = r.GetComponent<Image>();
        Color normal = danger ? new Color(0.24f, 0.10f, 0.09f, 1) : featured ? new Color(0.30f, 0.24f, 0.14f, 1) : new Color(0.075f, 0.115f, 0.12f, 1);
        var c = button.colors; c.normalColor = normal; c.highlightedColor = Color.Lerp(normal, Gold, 0.3f); c.selectedColor = c.highlightedColor;
        c.pressedColor = Color.Lerp(normal, Color.black, 0.3f); c.disabledColor = new Color(0.08f, 0.09f, 0.09f, 0.6f); c.fadeDuration = 0.15f; button.colors = c;
        var t = Text(r, label, new Vector2(12, 0), new Vector2(size.x - 24, size.y), size.y < 40 ? 15 : 21, danger ? Danger : LobbyPresentation.Ivory);
        t.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action()); return button;
    }
    static TMP_InputField Input(Transform p, Vector2 pos, Vector2 size)
    {
        var r = Box(p, "World name", pos, size, new Color(0.073f, 0.105f, 0.11f, 1));
        Box(r, "Input underline", new Vector2(0, -size.y + 2), new Vector2(size.x, 2), Gold).GetComponent<Image>().raycastTarget = false;
        var input = r.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = r.GetComponent<Image>();
        var viewport = Rect(r, "Viewport", new Vector2(23, -12), new Vector2(size.x - 46, size.y - 24)); viewport.gameObject.AddComponent<RectMask2D>();
        var text = Text(viewport, "", Vector2.zero, viewport.sizeDelta, 28, LobbyPresentation.Ivory);
        var placeholder = Text(viewport, "Ex.: Vale das Cinzas", Vector2.zero, viewport.sizeDelta, 28, Muted);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
        input.characterLimit = SoloWorldStore.MaxNameLength; input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false; input.customCaretColor = true; input.caretColor = Gold; input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.3f);
        return input;
    }
}
