using UnityEngine;

public sealed class StoneResourcePrefabBinder : MonoBehaviour
{
    void Awake()
    {
        Item item = GetComponent<Item>();
        Item stable = StoneResourceItemRegistry.GetOrCreate();
        if (item == null) return;
        item.itemName = StoneResourceItemRegistry.ItemName;
        item.icon = StoneResourceItemRegistry.GetSprite();
        item.itemType = stable.itemType;
        item.category = stable.GetCategory();
        item.rarity = stable.GetRarity();
        item.description = stable.GetDescription();
        item.weight = stable.GetWeight();
        item.maxStack = stable.GetMaxStack();
    }
}
