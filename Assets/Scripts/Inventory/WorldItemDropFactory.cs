using UnityEngine;

public sealed class WorldPickupStack : MonoBehaviour
{
    [Min(1)] public int amount = 1;
}

public static class WorldItemDropFactory
{
    public static GameObject Spawn(Vector3 position, Item item, int amount = 1, LayerMask groundMask = default, float collectRadius = 1.25f)
    {
        if (item == null || amount <= 0)
            return null;

        GameObject drop;
        if (StickResourceVisualFactory.IsStickResource(item.itemName))
            drop = StickResourceVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (StoneResourceVisualFactory.IsStoneResource(item.itemName))
            drop = StoneResourceVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (ChickenFeatherVisualFactory.IsFeather(item.itemName))
            drop = ChickenFeatherVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (BoarLeatherVisualFactory.IsBoarLeather(item.itemName))
            drop = BoarLeatherVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (BoarTuskVisualFactory.IsBoarTusk(item.itemName))
            drop = BoarTuskVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (RawBoarMeatVisualFactory.IsRawBoarMeat(item.itemName))
            drop = RawBoarMeatVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (RawChickenMeatVisualFactory.IsRawChickenMeat(item.itemName))
            drop = RawChickenMeatVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius, Random.insideUnitSphere * 0.2f);
        else if (OakWoodDropVisualFactory.IsOakWood(item.itemName))
            drop = OakWoodDropVisualFactory.Spawn(position, item, ResolveMask(groundMask), collectRadius);
        else
            drop = SpawnGeneric(position, item, ResolveMask(groundMask), collectRadius);

        if (drop != null)
            drop.AddComponent<WorldPickupStack>().amount = Mathf.Max(1, amount);
        return drop;
    }

    static GameObject SpawnGeneric(Vector3 position, Item source, LayerMask groundMask, float collectRadius)
    {
        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = $"{source.itemName} Drop";
        drop.transform.SetPositionAndRotation(position, Random.rotation);
        drop.transform.localScale = new Vector3(0.26f, 0.18f, 0.24f);

        Item item = drop.AddComponent<Item>();
        CopyItemData(item, source);

        Renderer renderer = drop.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                int hash = source.itemName != null ? source.itemName.GetHashCode() : 0;
                float hue = Mathf.Abs(hash % 997) / 997f;
                renderer.sharedMaterial = new Material(shader)
                {
                    name = $"{source.itemName}_PickupMaterial",
                    color = Color.HSVToRGB(hue, 0.48f, 0.9f)
                };
            }
        }

        Rigidbody body = drop.AddComponent<Rigidbody>();
        body.mass = 0.1f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.AddForce(Vector3.up * 1.15f + Random.insideUnitSphere * 0.22f, ForceMode.Impulse);

        FloatingPickup pickup = drop.AddComponent<FloatingPickup>();
        pickup.groundMask = groundMask;
        pickup.collectRadius = collectRadius;
        return drop;
    }

    static void CopyItemData(Item target, Item source)
    {
        target.itemName = source.itemName;
        target.icon = source.icon;
        target.itemType = source.itemType;
        target.category = source.GetCategory();
        target.rarity = source.GetRarity();
        target.description = source.GetDescription();
        target.weight = source.GetWeight();
        target.maxStack = source.GetMaxStack();
        target.toolType = source.toolType;
        target.toolDamage = source.toolDamage;
        target.equipmentSlot = source.GetEquipmentSlot();
        target.defense = source.GetDefense();
        target.durability = source.GetDurability();
        target.moveSpeedBonus = source.GetMoveSpeedBonus();
        target.buyPrice = source.GetBuyPrice();
        target.sellPrice = source.GetSellPrice();
    }

    static LayerMask ResolveMask(LayerMask mask)
    {
        return mask.value == 0 ? (LayerMask)~0 : mask;
    }
}
