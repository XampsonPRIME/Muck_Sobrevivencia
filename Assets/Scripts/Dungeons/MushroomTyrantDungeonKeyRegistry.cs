using UnityEngine;

public static class MushroomTyrantDungeonKeyRegistry
{
    public const string ItemName = "Key-Camara-do-Cogumelo-Tirano";
    const string ResourcesPath = "Items/Key-Camara-do-Cogumelo-Tirano";

    static Item cachedItem;

    public static Item GetOrCreate()
    {
        if (cachedItem != null)
            return cachedItem;

        GameObject prefab = Resources.Load<GameObject>(ResourcesPath);
        if (prefab != null)
        {
            cachedItem = prefab.GetComponent<Item>();
            if (cachedItem != null)
                return cachedItem;
        }

        GameObject fallbackObject = new GameObject(ItemName);
        fallbackObject.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(fallbackObject);

        cachedItem = fallbackObject.AddComponent<Item>();
        cachedItem.itemName = ItemName;
        cachedItem.itemType = ItemType.Resource;
        cachedItem.icon = null;
        return cachedItem;
    }
}
