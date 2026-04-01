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

    // 🔥 ferramenta necessária
    public ToolType requiredTool = ToolType.None;

    UIMessage uiMessage;

    void Start()
    {
        currentHealth = maxHealth;
        uiMessage = FindFirstObjectByType<UIMessage>();

    }

    public void Hit(Inventory inventory, Hotbar hotbar, ToolType currentTool, int toolDamage)
    {
        if (requiredTool != ToolType.None && currentTool != requiredTool)
        {
            UIMessage ui = FindFirstObjectByType<UIMessage>();

            if (requiredTool != ToolType.None && currentTool != requiredTool)
            {
                if (ui != null)
                {
                    string msg = GetToolMessage();
                    ui.ShowMessage(msg);
                }

                return;
            }

            return;
        }

        int damage = toolDamage;

        currentHealth -= damage;

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
    }
}
