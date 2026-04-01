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

    public ToolType requiredTool = ToolType.None;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void Hit(Inventory inventory, Hotbar hotbar, ToolType currentTool, int toolDamage)
    {
        // 🔥 ferramenta errada
        if (requiredTool != ToolType.None && currentTool != requiredTool)
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

        currentHealth -= toolDamage;

        if (hitEffect != null)
            Instantiate(hitEffect, transform.position + Vector3.up, Quaternion.identity);

        if (currentHealth <= 0)
        {
            DropResource(inventory, hotbar);
            Destroy(gameObject);
        }
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

    void DropResource(Inventory inventory, Hotbar hotbar)
    {
        if (inventory == null || hotbar == null)
        {
            Debug.LogError("Inventory ou Hotbar NULL!");
            return;
        }

        int amount = Random.Range(minDrop, maxDrop + 1);

        for (int i = 0; i < amount; i++)
        {
            inventory.AddItem(itemName);
            hotbar.AddItem(itemName, icon);
        }

        // 🔥 mensagem de loot (opcional)
        if (MessageSystem.Instance != null)
        {
            MessageSystem.Instance.ShowMessage($"+{amount} {itemName}");
        }
    }
}