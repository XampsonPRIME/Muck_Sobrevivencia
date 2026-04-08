using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerMagic : MonoBehaviour
{
    public bool hasUnlockedAreaMagic;

    [Header("Magia em Area")]
    public float areaMagicRange = 8f;
    public int areaMagicDamage = 16;
    public float areaMagicCooldown = 10f;
    [Range(0f, 1f)] public float areaMagicStaminaCostPercent = 0.8f;
    public float effectDuration = 0.6f;
    public Color effectColor = new Color(0.24f, 0.86f, 1f, 0.9f);

    PlayerMovement playerMovement;
    InputAction castMagicAction;
    float nextMagicCastTime;
    MagicCooldownHUDView cooldownHudView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null || player.GetComponent<PlayerMagic>() != null)
            return;

        player.gameObject.AddComponent<PlayerMagic>();
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        castMagicAction = new InputAction("CastMagic", binding: "<Keyboard>/g");
    }

    void OnEnable()
    {
        castMagicAction.Enable();
    }

    void OnDisable()
    {
        castMagicAction.Disable();
    }

    void Start()
    {
        EnsureCooldownUI();
        RefreshCooldownUI();
    }

    void Update()
    {
        EnsureCooldownUI();
        RefreshCooldownUI();

        if (!hasUnlockedAreaMagic)
            return;

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || GameState.IsInventoryOpen)
            return;

        if (!castMagicAction.WasPressedThisFrame())
            return;

        TryCastAreaMagic();
    }

    public bool UnlockAreaMagic(string magicName = "Magia Ancestral")
    {
        if (hasUnlockedAreaMagic)
        {
            MessageSystem.Instance?.ShowMessage("Voce ja domina essa magia.");
            return false;
        }

        hasUnlockedAreaMagic = true;
        MessageSystem.Instance?.ShowMessage($"{magicName} desbloqueada!");
        return true;
    }

    public void LoadState(bool unlockedAreaMagic)
    {
        hasUnlockedAreaMagic = unlockedAreaMagic;
        nextMagicCastTime = 0f;
        RefreshCooldownUI();
    }

    void EnsureCooldownUI()
    {
        if (cooldownHudView != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MagicCooldownHUD");
        if (existing != null)
        {
            cooldownHudView = existing.GetComponent<MagicCooldownHUDView>();
            return;
        }

        GameObject hudPrefab = Resources.Load<GameObject>("MagicCooldownHUD");
        if (hudPrefab == null)
            return;

        GameObject hudInstance = Instantiate(hudPrefab, canvas.transform, false);
        cooldownHudView = hudInstance.GetComponent<MagicCooldownHUDView>();
    }

    void RefreshCooldownUI()
    {
        if (cooldownHudView == null || cooldownHudView.iconImage == null || cooldownHudView.cooldownFillImage == null)
            return;

        bool shouldShow = hasUnlockedAreaMagic && !GameState.IsPlayerDead;
        cooldownHudView.gameObject.SetActive(shouldShow);

        if (!shouldShow)
            return;

        cooldownHudView.iconImage.sprite = MagicSpellItemRegistry.GetMagicSprite(MagicSpellConfig.FindConfig());
        cooldownHudView.iconImage.enabled = cooldownHudView.iconImage.sprite != null;

        float cooldownRemaining = Mathf.Max(0f, nextMagicCastTime - Time.time);
        if (cooldownRemaining > 0.01f)
        {
            cooldownHudView.iconImage.color = new Color(1f, 1f, 1f, 0.38f);
            cooldownHudView.cooldownFillImage.gameObject.SetActive(true);
            cooldownHudView.cooldownFillImage.fillAmount = Mathf.Clamp01(cooldownRemaining / Mathf.Max(0.01f, areaMagicCooldown));
        }
        else
        {
            cooldownHudView.iconImage.color = new Color(1f, 1f, 1f, 1f);
            cooldownHudView.cooldownFillImage.fillAmount = 0f;
            cooldownHudView.cooldownFillImage.gameObject.SetActive(false);
        }
    }

    public bool TryCastAreaMagic()
    {
        if (!hasUnlockedAreaMagic || playerMovement == null)
            return false;

        if (Time.time < nextMagicCastTime)
        {
            MessageSystem.Instance?.ShowMessage("Magia em recarga.");
            return false;
        }

        float staminaCost = playerMovement.maxStamina * areaMagicStaminaCostPercent;
        if (playerMovement.currentStamina < staminaCost)
        {
            MessageSystem.Instance?.ShowMessage("Stamina insuficiente para usar a magia.");
            return false;
        }

        playerMovement.currentStamina = Mathf.Max(0f, playerMovement.currentStamina - staminaCost);
        nextMagicCastTime = Time.time + areaMagicCooldown;
        SpawnMagicEffect();

        Collider[] hits = Physics.OverlapSphere(transform.position, areaMagicRange, ~0, QueryTriggerInteraction.Collide);
        HashSet<MiniKrug> hitMiniKrugs = new HashSet<MiniKrug>();
        HashSet<BossEnemy> hitBosses = new HashSet<BossEnemy>();
        int affectedCount = 0;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            MiniKrug miniKrug = hit.GetComponent<MiniKrug>() ?? hit.GetComponentInParent<MiniKrug>();
            if (miniKrug != null && hitMiniKrugs.Add(miniKrug))
            {
                if (LanMultiplayerManager.Instance != null &&
                    LanMultiplayerManager.Instance.TryHandleGameplayHit(miniKrug, playerMovement, ToolType.Axe, areaMagicDamage))
                {
                    affectedCount++;
                    continue;
                }

                miniKrug.Hit(areaMagicDamage, playerMovement);
                affectedCount++;
                continue;
            }

            BossEnemy bossEnemy = hit.GetComponent<BossEnemy>() ?? hit.GetComponentInParent<BossEnemy>();
            if (bossEnemy != null && hitBosses.Add(bossEnemy))
            {
                if (LanMultiplayerManager.Instance != null &&
                    LanMultiplayerManager.Instance.TryHandleGameplayHit(bossEnemy, playerMovement, ToolType.Axe, areaMagicDamage))
                {
                    affectedCount++;
                    continue;
                }

                bossEnemy.Hit(areaMagicDamage, playerMovement);
                affectedCount++;
            }
        }

        MessageSystem.Instance?.ShowMessage(affectedCount > 0 ? "Magia ancestral ativada!" : "A magia explodiu, mas nao atingiu inimigos.");
        return true;
    }

    void SpawnMagicEffect()
    {
        GameObject effectObject = new GameObject("MagicAreaEffect");
        effectObject.transform.position = transform.position;

        MagicAreaEffect effect = effectObject.AddComponent<MagicAreaEffect>();
        effect.duration = effectDuration;
        effect.maxRadius = areaMagicRange;
        effect.effectColor = effectColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.22f, 0.78f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, areaMagicRange);
    }
}
