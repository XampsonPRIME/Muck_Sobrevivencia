using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class BestiaryTracker : MonoBehaviour
{
    public float discoveryRadius = 18f;
    public float scanInterval = 0.45f;
    public LayerMask scanMask = ~0;

    readonly Collider[] hits = new Collider[96];
    float nextScanTime;

    void Update()
    {
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + Mathf.Max(0.1f, scanInterval);

        if (GameState.IsInLobby || GameState.IsPlayerDead)
            return;

        BestiaryService service = BestiaryService.Instance;
        if (service == null)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, discoveryRadius, hits, scanMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (BestiaryDatabase.TryResolveCreature(hit, out string creatureId))
                service.Discover(creatureId);
        }
    }
}
