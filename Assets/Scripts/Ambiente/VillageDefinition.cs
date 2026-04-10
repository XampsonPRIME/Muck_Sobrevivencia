using UnityEngine;

public class VillageDefinition : MonoBehaviour
{
    public string villageId = "starter_village";
    [Min(8f)] public float reservationRadius = 26f;
    [Min(6f)] public float flattenRadius = 18f;
    [Min(0f)] public float flattenBlendRadius = 10f;
}
