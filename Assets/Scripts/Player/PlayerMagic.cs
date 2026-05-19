using System.Collections;
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
    public AudioSource magicSfxSource;

    PlayerMovement playerMovement;
    InputAction castMagicAction;
    float nextMagicCastTime;
    MagicCooldownHUDView cooldownHudView;
    Coroutine stopMagicSfxCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        PlayerMovement player = LanMultiplayerManager.FindGameplayPlayer();
        if (player == null || player.GetComponent<PlayerMagic>() != null)
            return;

        player.gameObject.AddComponent<PlayerMagic>();
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        castMagicAction = new InputAction("CastMagic", binding: "<Keyboard>/g");
        EnsureMagicAudioSource();
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

        if (GameState.IsInLobby || GameState.IsPlayerDead || GameState.IsPaused || GameState.IsInventoryOpen || GameState.IsVendorOpen || GameState.IsCraftingOpen)
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

        Canvas canvas = SceneObjectCache.Find<Canvas>(gameObject.scene, true);
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

        MagicSpellConfig config = GetActiveMagicConfig();

        bool shouldShow = hasUnlockedAreaMagic && !GameState.IsPlayerDead;
        cooldownHudView.gameObject.SetActive(shouldShow);

        if (!shouldShow)
            return;

        cooldownHudView.iconImage.sprite = MagicSpellItemRegistry.GetMagicSprite(config);
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
        PlayMagicCastSound(GetActiveMagicConfig());
        SpawnMagicEffect();

        Collider[] hits = Physics.OverlapSphere(transform.position, areaMagicRange, ~0, QueryTriggerInteraction.Collide);
        HashSet<MiniKrug> hitMiniKrugs = new HashSet<MiniKrug>();
        HashSet<BossEnemy> hitBosses = new HashSet<BossEnemy>();
        int affectedCount = 0;
        bool hitBossOrMiniBoss = false;

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
                    hitBossOrMiniBoss = true;
                    continue;
                }

                miniKrug.Hit(areaMagicDamage, playerMovement);
                affectedCount++;
                hitBossOrMiniBoss = true;
                continue;
            }

            BossEnemy bossEnemy = hit.GetComponent<BossEnemy>() ?? hit.GetComponentInParent<BossEnemy>();
            if (bossEnemy != null && hitBosses.Add(bossEnemy))
            {
                if (LanMultiplayerManager.Instance != null &&
                    LanMultiplayerManager.Instance.TryHandleGameplayHit(bossEnemy, playerMovement, ToolType.Axe, areaMagicDamage))
                {
                    affectedCount++;
                    hitBossOrMiniBoss = true;
                    continue;
                }

                if (!bossEnemy.CanBeChallengedBy(playerMovement))
                {
                    MessageSystem.Instance?.ShowMessage(bossEnemy.BuildMinimumLevelMessage());
                    continue;
                }

                bossEnemy.Hit(areaMagicDamage, playerMovement);
                affectedCount++;
                hitBossOrMiniBoss = true;
            }
        }

        if (hitBossOrMiniBoss)
            playerMovement.RegisterBossOrMiniBossCombat();

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

    MagicSpellConfig GetActiveMagicConfig()
    {
        return MagicSpellConfig.FindConfig();
    }

    void EnsureMagicAudioSource()
    {
        if (magicSfxSource != null)
            return;

        AudioSource existingSource = GetComponent<AudioSource>();
        if (existingSource != null && existingSource != playerMovement?.sfxSource)
        {
            magicSfxSource = existingSource;
        }
        else
        {
            magicSfxSource = gameObject.AddComponent<AudioSource>();
        }

        magicSfxSource.playOnAwake = false;
        magicSfxSource.loop = false;
        magicSfxSource.spatialBlend = 0f;
    }

    void PlayMagicCastSound(MagicSpellConfig config)
    {
        if (config == null || config.castSound == null)
            return;

        EnsureMagicAudioSource();

        if (magicSfxSource == null)
            return;

        if (stopMagicSfxCoroutine != null)
        {
            StopCoroutine(stopMagicSfxCoroutine);
            stopMagicSfxCoroutine = null;
        }

        magicSfxSource.Stop();
        magicSfxSource.clip = config.castSound;
        magicSfxSource.volume = Mathf.Clamp01(config.castSoundVolume);
        magicSfxSource.time = 0f;
        magicSfxSource.Play();

        float duration = config.castSoundDuration > 0f
            ? Mathf.Min(config.castSoundDuration, config.castSound.length)
            : config.castSound.length;

        stopMagicSfxCoroutine = StartCoroutine(StopMagicSfxAfterDelay(duration));
    }

    IEnumerator StopMagicSfxAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (magicSfxSource != null && magicSfxSource.isPlaying)
            magicSfxSource.Stop();

        stopMagicSfxCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.22f, 0.78f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, areaMagicRange);
    }
}
