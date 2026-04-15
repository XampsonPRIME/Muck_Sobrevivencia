using UnityEngine;

[DisallowMultipleComponent]
public class PortalSpawnPoint : MonoBehaviour
{
    public string spawnPointId = "Default";

    public bool Matches(string requestedId)
    {
        if (string.IsNullOrWhiteSpace(requestedId))
            return string.IsNullOrWhiteSpace(spawnPointId) || spawnPointId == "Default";

        return string.Equals(spawnPointId, requestedId, System.StringComparison.OrdinalIgnoreCase);
    }
}
