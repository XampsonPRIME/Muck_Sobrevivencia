using UnityEngine;

[DisallowMultipleComponent]
public class ScenePortal : MonoBehaviour
{
    [Header("Destino")]
    public string targetSceneSetId = "EnchantedForest";
    public string fallbackSceneName = "EnchantedForest";
    public string targetSpawnPointId = "Default";
    public Vector3 fallbackDestinationPosition = Vector3.zero;
    public Vector3 fallbackDestinationEulerAngles = Vector3.zero;

    [Header("Comportamento")]
    public bool triggerOnEnter = true;
    public bool triggerOnlyOnce;
    public float retriggerCooldown = 1.5f;

    [Header("Feedback")]
    public string travelMessage = "Entrando na Floresta Encantada...";
    public string failedMessage = "Nao foi possivel abrir o portal.";

    bool hasTriggered;
    float nextAllowedTriggerTime;

    void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter)
            return;

        if (!CanTrigger())
            return;

        if (other == null || other.GetComponentInParent<PlayerMovement>() == null)
            return;

        TryTravel();
    }

    public void ActivatePortal()
    {
        if (!CanTrigger())
            return;

        TryTravel();
    }

    bool CanTrigger()
    {
        if (triggerOnlyOnce && hasTriggered)
            return false;

        return Time.unscaledTime >= nextAllowedTriggerTime;
    }

    void TryTravel()
    {
        nextAllowedTriggerTime = Time.unscaledTime + Mathf.Max(0.1f, retriggerCooldown);

        MultiplayerSceneSetState targetSceneSet = MultiplayerSceneSetCatalog.ResolveStartupState(targetSceneSetId, fallbackSceneName);
        string targetSceneName = targetSceneSet?.activeSceneName;
        if (string.IsNullOrWhiteSpace(targetSceneName))
            targetSceneName = fallbackSceneName;

        Quaternion fallbackRotation = Quaternion.Euler(fallbackDestinationEulerAngles);
        PortalTravelManager.QueueArrival(targetSceneName, targetSpawnPointId, fallbackDestinationPosition, fallbackRotation);

        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        bool success;

        if (manager != null)
        {
            success = manager.TravelToSceneSet(targetSceneSetId, fallbackSceneName);
        }
        else
        {
            success = MultiplayerSceneSetCatalog.ApplyToRuntime(targetSceneSet);
        }

        if (!success)
        {
            PortalTravelManager.CancelPendingArrival();
            MessageSystem.Instance?.ShowMessage(string.IsNullOrWhiteSpace(failedMessage) ? "Falha ao viajar pelo portal." : failedMessage);
            return;
        }

        hasTriggered = true;
        GameState.IsInLobby = false;
        GameState.IsPaused = false;
        GameState.IsInventoryOpen = false;
        MessageSystem.Instance?.ShowMessage(string.IsNullOrWhiteSpace(travelMessage) ? "Portal ativado." : travelMessage);
    }
}
