using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    public string targetSceneSetId = "EnchantedForest";
    public string targetSpawnPointId = "Default";
    public float travelCooldown = 1.5f;
    public bool onlyHostCanUseInMultiplayer = true;

    float nextUseTime;

    void Reset()
    {
        Collider portalCollider = GetComponent<Collider>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < nextUseTime)
            return;

        PlayerMovement player = other.GetComponent<PlayerMovement>() ?? other.GetComponentInParent<PlayerMovement>();
        if (player == null || LanMultiplayerManager.IsReplica(player))
            return;

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (onlyHostCanUseInMultiplayer &&
            manager != null &&
            manager.IsMultiplayerActive &&
            !manager.IsServerAuthority)
        {
            MessageSystem.Instance?.ShowMessage("Somente o host pode ativar o portal.");
            return;
        }

        if (!PortalTravelManager.RequestTravel(targetSceneSetId, targetSpawnPointId, travelCooldown))
            return;

        nextUseTime = Time.time + Mathf.Max(0.25f, travelCooldown);
    }
}
