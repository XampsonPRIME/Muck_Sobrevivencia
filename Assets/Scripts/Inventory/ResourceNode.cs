using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    static readonly List<ResourceNode> activeNodes = new List<ResourceNode>();

    public string itemName = "Toras";
    public Sprite icon;

    public int maxHealth = 3;
    private int currentHealth;

    public GameObject hitEffect;

    public int minDrop = 1;
    public int maxDrop = 5;
    public Item itemData;

    public ToolType requiredTool = ToolType.None;
    public bool allowEmptyHand = true;
    public int emptyHandDamage = 1;
    public int emptyHandMinDrop = 1;
    public int emptyHandMaxDrop = 1;
    [Header("Respawn")]
    public bool respawnAfterDepleted = true;
    public Vector2 respawnDelayRange = new Vector2(180f, 300f);

    public int CurrentHealth => currentHealth;
    public bool IsDepleted => depleted;
    public static IReadOnlyList<ResourceNode> ActiveNodes => activeNodes;

    bool depleted;
    Coroutine respawnRoutine;
    Renderer[] cachedRenderers;
    Collider[] cachedColliders;

    void OnEnable()
    {
        if (!activeNodes.Contains(this))
            activeNodes.Add(this);
    }

    void OnDisable()
    {
        activeNodes.Remove(this);
    }

    void Start()
    {
        LanNetworkEntity.Ensure(this);
        currentHealth = maxHealth;
        CacheVisibilityComponents();
    }

    public void Hit(Inventory inventory, Hotbar hotbar, ToolType currentTool, int toolDamage)
    {
        if (depleted)
            return;

        bool usingEmptyHand = allowEmptyHand && currentTool == ToolType.None;
        bool usingCorrectTool = requiredTool == ToolType.None || currentTool == requiredTool;

        if (!usingCorrectTool && !usingEmptyHand)
        {
            MessageSystem.Instance?.ShowMessage(GetToolMessage());
            return;
        }

        // Ferramenta errada continua bloqueada. Mao vazia agora funciona com rendimento baixo.
        if (!usingCorrectTool && !usingEmptyHand)
        {
            string msg = GetToolMessage();

            // 🔥 NOVO SISTEMA DE MENSAGEM
            if (MessageSystem.Instance != null)
            {
                MessageSystem.Instance.ShowMessage(msg);
            }
            else
            {
                Debug.LogWarning("MessageSystem não encontrado!");
            }

            return;
        }

        int appliedDamage = usingCorrectTool ? toolDamage : emptyHandDamage;
        currentHealth -= Mathf.Max(1, appliedDamage);

        if (hitEffect != null)
            Instantiate(hitEffect, transform.position + Vector3.up, Quaternion.identity);

        if (currentHealth <= 0)
        {
            DropResource(inventory, hotbar, usingCorrectTool);
            Deplete();
        }
    }

    public bool TryHitForReward(ToolType currentTool, int toolDamage, out string rewardItemName, out string rewardPrefabName, out int rewardAmount, out int remainingHealth, out bool destroyed)
    {
        rewardItemName = itemName;
        rewardPrefabName = itemData != null ? itemData.gameObject.name : string.Empty;
        rewardAmount = 0;
        destroyed = false;

        if (depleted)
        {
            remainingHealth = 0;
            return false;
        }

        bool usingEmptyHand = allowEmptyHand && currentTool == ToolType.None;
        bool usingCorrectTool = requiredTool == ToolType.None || currentTool == requiredTool;
        if (!usingCorrectTool && !usingEmptyHand)
        {
            remainingHealth = currentHealth;
            return false;
        }

        int appliedDamage = usingCorrectTool ? toolDamage : emptyHandDamage;
        currentHealth -= Mathf.Max(1, appliedDamage);

        if (currentHealth <= 0)
        {
            int minAmount = usingCorrectTool ? minDrop : emptyHandMinDrop;
            int maxAmount = usingCorrectTool ? maxDrop : emptyHandMaxDrop;
            rewardAmount = Random.Range(minAmount, maxAmount + 1);
            destroyed = true;
        }

        remainingHealth = Mathf.Max(0, currentHealth);

        if (destroyed)
            Deplete();

        return true;
    }

    public bool CanBeHitBy(ToolType currentTool)
    {
        if (depleted)
            return false;

        bool usingEmptyHand = allowEmptyHand && currentTool == ToolType.None;
        bool usingCorrectTool = requiredTool == ToolType.None || currentTool == requiredTool;
        return usingCorrectTool || usingEmptyHand;
    }

    public void ApplyNetworkState(int networkHealth, bool destroyed)
    {
        currentHealth = Mathf.Max(0, networkHealth);

        if (destroyed)
        {
            HideNode();
            return;
        }

        depleted = false;
        ShowNode();
    }

    public void PlayHitFeedback()
    {
        if (hitEffect != null)
            Instantiate(hitEffect, transform.position + Vector3.up, Quaternion.identity);
    }

    string GetToolMessage()
    {
        switch (requiredTool)
        {
            case ToolType.Axe:
                return "Use um machado para madeira.";

            case ToolType.Pickaxe:
                return "Use uma picareta para minerar.";

            default:
                return "Ferramenta inadequada.";
        }
    }

    void DropResource(Inventory inventory, Hotbar hotbar, bool usingCorrectTool)
    {
        int minAmount = usingCorrectTool ? minDrop : emptyHandMinDrop;
        int maxAmount = usingCorrectTool ? maxDrop : emptyHandMaxDrop;
        int amount = Random.Range(minAmount, maxAmount + 1);

        AncestralPowerService powers = inventory != null ? inventory.GetComponent<AncestralPowerService>() : null;
        if (powers != null && powers.TryRollBonusResource())
            amount += 1;

        if (ShouldSpawnOakWoodPickup())
        {
            SpawnOakWoodPickups(amount);
            return;
        }

        if (inventory == null || !inventory.AddItem(itemName, amount, itemData))
        {
            MessageSystem.Instance?.ShowMessage("Inventario cheio");
            return;
        }

        Sprite pickupIcon = itemData != null ? itemData.icon : null;
        PickupMessageSystem.Show(itemName, amount, transform.position + Vector3.up * 1.05f, pickupIcon);
    }

    bool ShouldSpawnOakWoodPickup()
    {
        return requiredTool == ToolType.Axe && OakWoodDropVisualFactory.IsOakWood(itemName);
    }

    void SpawnOakWoodPickups(int amount)
    {
        if (amount <= 0)
            return;

        Item oakWoodItem = itemData != null ? itemData : OakWoodItemRegistry.GetOrCreate();
        for (int i = 0; i < amount; i++)
        {
            Vector2 circle = Random.insideUnitCircle * 0.9f;
            Vector3 spawnPosition = transform.position + new Vector3(circle.x, 1.35f + i * 0.04f, circle.y);
            OakWoodDropVisualFactory.Spawn(spawnPosition, oakWoodItem, ~0, 1.25f);
        }
    }

    void Deplete()
    {
        currentHealth = 0;
        HideNode();

        if (!respawnAfterDepleted)
        {
            Destroy(gameObject);
            return;
        }

        if (ShouldControlRespawn())
        {
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);

            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }
    }

    bool ShouldControlRespawn()
    {
        LanMultiplayerManager lan = LanMultiplayerManager.Instance;
        return lan == null || !lan.IsMultiplayerActive || lan.IsServerAuthority;
    }

    System.Collections.IEnumerator RespawnAfterDelay()
    {
        float minDelay = Mathf.Max(1f, respawnDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, respawnDelayRange.y);
        yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

        currentHealth = maxHealth;
        depleted = false;
        ShowNode();

        LanMultiplayerManager lan = LanMultiplayerManager.Instance;
        if (lan != null)
            lan.NotifyResourceRespawned(this, currentHealth);
    }

    void HideNode()
    {
        depleted = true;
        SetNodeVisible(false);
    }

    void ShowNode()
    {
        SetNodeVisible(true);
    }

    void SetNodeVisible(bool visible)
    {
        CacheVisibilityComponents();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = visible;
        }
    }

    void CacheVisibilityComponents()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (cachedColliders == null || cachedColliders.Length == 0)
            cachedColliders = GetComponentsInChildren<Collider>(true);
    }
}
