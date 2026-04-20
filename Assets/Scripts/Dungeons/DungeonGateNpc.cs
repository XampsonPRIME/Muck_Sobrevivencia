using UnityEngine;

[DisallowMultipleComponent]
public class DungeonGateNpc : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] MushroomTyrantDungeon dungeon;
    [SerializeField] string missingKeyMessage = "Voce precisa da chave da Camara do Cogumelo Tirano.";
    [SerializeField] string enterMessage = "A chave foi entregue. Entrando no calabouco...";
    [SerializeField] string busyMessage = "O calabouco ja esta em andamento.";

    public void SetDungeon(MushroomTyrantDungeon targetDungeon)
    {
        dungeon = targetDungeon;
    }

    public bool Interact(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null)
            return false;

        if (dungeon == null)
        {
            MessageSystem.Instance?.ShowMessage("O calabouco nao esta configurado.");
            return true;
        }

        Inventory inventory = playerInteraction.inventory;
        if (inventory == null)
            return true;

        InventoryItem keyItem = inventory.GetItem(MushroomTyrantDungeonKeyRegistry.ItemName);
        if (keyItem == null || keyItem.quantity <= 0)
        {
            MessageSystem.Instance?.ShowMessage(missingKeyMessage);
            return true;
        }

        if (!dungeon.CanAcceptNewEntry(playerInteraction))
        {
            MessageSystem.Instance?.ShowMessage(busyMessage);
            return true;
        }

        inventory.RemoveItem(MushroomTyrantDungeonKeyRegistry.ItemName, 1);
        playerInteraction.hotbar?.RemoveInventoryItem(keyItem, 1);
        dungeon.EnterDungeon(playerInteraction);
        MessageSystem.Instance?.ShowMessage(enterMessage);
        return true;
    }
}
