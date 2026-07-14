using UnityEngine;

[DisallowMultipleComponent]
public class BestiaryCreatureIdentity : MonoBehaviour
{
    public BestiaryCreatureData creatureData;
    public string creatureId;

    public string ResolveCreatureId()
    {
        if (creatureData != null && !string.IsNullOrWhiteSpace(creatureData.creatureId))
            return creatureData.creatureId;

        return creatureId;
    }
}
