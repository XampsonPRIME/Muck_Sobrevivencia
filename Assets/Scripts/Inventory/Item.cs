using UnityEngine;

public enum ItemType
{
    Resource,
    Tool,
    Consumable
}

public enum ToolType
{
    None,
    Axe,
    Pickaxe,
    
}


public class Item : MonoBehaviour
{
    [Header("Base")]
    public string itemName;
    public Sprite icon;

    public ItemType itemType = ItemType.Resource;

    [Header("Ferramenta")]
    public ToolType toolType = ToolType.None;
    public int toolDamage = 1;

    [Header("Comercio")]
    [Min(0)] public int buyPrice = 0;
    [Min(0)] public int sellPrice = 0;

    public int GetBuyPrice()
    {
        return Mathf.Max(0, buyPrice);
    }

    public int GetSellPrice()
    {
        return Mathf.Max(0, sellPrice);
    }
}
