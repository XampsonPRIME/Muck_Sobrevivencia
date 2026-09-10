using UnityEngine;

public enum PlaceableKind
{
    Furnace,
    CraftingBench
}

public class PlaceableItem : MonoBehaviour
{
    public PlaceableKind kind = PlaceableKind.Furnace;
    public string placedObjectName = "Objeto Colocado";
    public Vector3 placedScale = Vector3.one;
}
