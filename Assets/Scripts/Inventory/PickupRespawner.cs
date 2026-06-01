using System.Collections;
using UnityEngine;

public class PickupRespawner : MonoBehaviour
{
    public Vector2 respawnDelayRange = new Vector2(120f, 240f);

    bool available = true;
    Coroutine respawnRoutine;
    Renderer[] cachedRenderers;
    Collider[] cachedColliders;

    public bool IsAvailable => available;

    void Awake()
    {
        CacheVisibilityComponents();
    }

    public bool Collect()
    {
        if (!available)
            return false;

        available = false;
        SetVisible(false);

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);

        respawnRoutine = StartCoroutine(RespawnAfterDelay());
        return true;
    }

    IEnumerator RespawnAfterDelay()
    {
        float minDelay = Mathf.Max(1f, respawnDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, respawnDelayRange.y);
        yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

        available = true;
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        CacheVisibilityComponents();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = visible;
        }
    }

    void CacheVisibilityComponents()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (cachedColliders == null || cachedColliders.Length == 0)
            cachedColliders = GetComponentsInChildren<Collider>(true);
    }
}
