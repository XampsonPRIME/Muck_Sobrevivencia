using UnityEngine;

public enum ItemType
{
    Resource,
    Tool
}

public enum ToolType
{
    None,
    Axe,
    Pickaxe
}

public class Item : MonoBehaviour
{
    public string itemName;
    public Sprite icon;

    public ItemType itemType = ItemType.Resource;

    // 🔥 Só usado se for ferramenta
    public ToolType toolType = ToolType.None;
    public int toolDamage = 1;
}