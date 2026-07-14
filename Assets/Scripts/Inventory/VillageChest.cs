using UnityEngine;

[DisallowMultipleComponent]
public class VillageChest : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] int minRustyMetalAmount = 7;
    [SerializeField] int maxRustyMetalAmount = 15;
    [SerializeField] Item rustyMetalItem;

    bool collected;

    public void Configure(Item item, int minAmount = 7, int maxAmount = 15)
    {
        rustyMetalItem = item;
        minRustyMetalAmount = Mathf.Max(1, minAmount);
        maxRustyMetalAmount = Mathf.Max(minRustyMetalAmount, maxAmount);
    }

    public bool Interact(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null || playerInteraction.inventory == null)
            return false;

        if (collected)
        {
            MessageSystem.Instance?.ShowMessage("Bau vazio.");
            return true;
        }

        Item item = rustyMetalItem != null ? rustyMetalItem : RustyMetalItemRegistry.GetOrCreate();
        if (item == null)
        {
            MessageSystem.Instance?.ShowMessage("Recurso do bau nao configurado.");
            return true;
        }

        int amount = Random.Range(minRustyMetalAmount, maxRustyMetalAmount + 1);
        playerInteraction.inventory.AddItem(item.itemName, amount, item);

        collected = true;
        UpdateOpenedVisual();

        PickupMessageSystem.Show(item.itemName, amount, transform.position + Vector3.up * 1.15f, item.icon);
        return true;
    }

    void UpdateOpenedVisual()
    {
        Transform lid = transform.Find("Lid");
        if (lid != null)
            lid.localRotation = Quaternion.Euler(-55f, 0f, 0f);
    }
}
