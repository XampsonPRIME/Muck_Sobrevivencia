using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    public string itemName = "Toras";
    public Sprite icon;

    public int maxHealth = 3;
    private int currentHealth;

    public GameObject hitEffect;

    public int minDrop = 1;
    public int maxDrop = 5;
    public Item itemData;

    public ToolType requiredTool = ToolType.None;
    public int emptyHandDamage = 1;
    public int emptyHandMinDrop = 1;
    public int emptyHandMaxDrop = 1;
    public int CurrentHealth => currentHealth;

    void Start()
    {
        LanNetworkEntity.Ensure(this);
        currentHealth = maxHealth;
    }

    public void Hit(Inventory inventory, Hotbar hotbar, ToolType currentTool, int toolDamage)
    {
        bool usingEmptyHand = currentTool == ToolType.None;
        bool usingCorrectTool = requiredTool == ToolType.None || currentTool == requiredTool;

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
            Destroy(gameObject);
        }
    }

    public bool TryHitForReward(ToolType currentTool, int toolDamage, out string rewardItemName, out string rewardPrefabName, out int rewardAmount, out int remainingHealth, out bool destroyed)
    {
        rewardItemName = itemName;
        rewardPrefabName = itemData != null ? itemData.gameObject.name : string.Empty;
        rewardAmount = 0;
        destroyed = false;

        bool usingEmptyHand = currentTool == ToolType.None;
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
            Destroy(gameObject);

        return true;
    }

    public bool CanBeHitBy(ToolType currentTool)
    {
        bool usingEmptyHand = currentTool == ToolType.None;
        bool usingCorrectTool = requiredTool == ToolType.None || currentTool == requiredTool;
        return usingCorrectTool || usingEmptyHand;
    }

    public void ApplyNetworkState(int networkHealth, bool destroyed)
    {
        currentHealth = Mathf.Max(0, networkHealth);

        if (destroyed)
            Destroy(gameObject);
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
                return "Use uma picareta para pedra.";

            default:
                return "Ferramenta inadequada.";
        }
    }

    void DropResource(Inventory inventory, Hotbar hotbar, bool usingCorrectTool)
    {
        int minAmount = usingCorrectTool ? minDrop : emptyHandMinDrop;
        int maxAmount = usingCorrectTool ? maxDrop : emptyHandMaxDrop;
        int amount = Random.Range(minAmount, maxAmount + 1);

        inventory.AddItem(itemName, amount, itemData);

        if (MessageSystem.Instance != null)
        {
            MessageSystem.Instance.ShowMessage($"+{amount} {itemName}");
        }
    }
}
