using UnityEngine;

public class FloatingPickup : MonoBehaviour
{
    const float PlayerCacheRefreshInterval = 0.35f;

    public float collectRadius = 1.15f;
    public float hoverHeight = 0.55f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.6f;
    public float rotationSpeed = 70f;
    public bool autoCollect = true;
    public float autoCollectDelay = 0.8f;
    public float settleCheckDelay = 0.08f;
    public float groundSearchHeight = 3f;
    public float groundSearchDistance = 8f;
    public LayerMask groundMask = ~0;

    static PlayerInteraction[] cachedPlayers = System.Array.Empty<PlayerInteraction>();
    static float nextPlayerCacheRefreshTime;

    readonly RaycastHit[] groundHits = new RaycastHit[24];
    Item item;
    Rigidbody rb;
    Collider[] colliders;
    Vector3 basePosition;
    Vector3 previousPosition;
    float spawnTime;
    float phaseOffset;
    bool settled;
    bool collecting;

    void Awake()
    {
        item = GetComponent<Item>();
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        spawnTime = Time.time;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        previousPosition = transform.position;
    }

    void Start()
    {
        EnsureProjectilePhysics();
    }

    void Update()
    {
        if (collecting)
            return;

        if (!settled)
        {
            TrySettleWhileFalling();
            previousPosition = transform.position;
            return;
        }

        FloatAndRotate();
        TryAutoCollect();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (settled || collision == null || collision.contactCount == 0)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y >= 0.35f)
            {
                SettleAt(contact.point);
                return;
            }
        }
    }

    void EnsureProjectilePhysics()
    {
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = Mathf.Max(0.04f, rb.mass);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void TrySettleWhileFalling()
    {
        if (Time.time - spawnTime < settleCheckDelay)
            return;

        if (TryFindCrossedGround(out Vector3 crossedGround))
        {
            SettleAt(crossedGround);
            return;
        }

        if (!TryFindGroundNearPickup(out Vector3 groundPoint))
            return;

        float distanceToGround = transform.position.y - groundPoint.y;
        bool fallingOrStill = rb == null || rb.linearVelocity.y <= 0.1f;
        if (fallingOrStill && distanceToGround <= hoverHeight + 0.12f)
            SettleAt(groundPoint);
    }

    bool TryFindCrossedGround(out Vector3 groundPoint)
    {
        groundPoint = Vector3.zero;
        Vector3 travel = transform.position - previousPosition;
        float distance = travel.magnitude;
        if (distance <= 0.001f)
            return false;

        int hitCount = Physics.RaycastNonAlloc(
            previousPosition,
            travel.normalized,
            groundHits,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        return TryPickValidGroundHit(groundHits, hitCount, out groundPoint);
    }

    bool TryFindGroundNearPickup(out Vector3 groundPoint)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundSearchHeight;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHits,
            groundSearchHeight + groundSearchDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        return TryPickValidGroundHit(groundHits, hitCount, out groundPoint);
    }

    bool TryPickValidGroundHit(RaycastHit[] hits, int hitCount, out Vector3 groundPoint)
    {
        groundPoint = Vector3.zero;
        if (hits == null || hitCount <= 0)
            return false;

        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit))
                continue;

            float distance = (transform.position - hit.point).sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            groundPoint = hit.point;
            found = true;
        }

        return found;
    }

    bool IsValidGroundHit(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null)
            return false;

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return false;

        if (hit.normal.y < 0.35f)
            return false;

        if (hitCollider.GetComponentInParent<PlayerMovement>() != null)
            return false;

        if (hitCollider.GetComponentInParent<RemotePlayerReplica>() != null)
            return false;

        if (hitCollider.GetComponentInParent<ResourceNode>() != null)
            return false;

        if (hitCollider.GetComponentInParent<WildChicken>() != null)
            return false;

        if (hitCollider.GetComponentInParent<WildBoar>() != null)
            return false;

        if (hitCollider.GetComponentInParent<EarthGolem>() != null)
            return false;

        if (hitCollider.GetComponentInParent<Cow>() != null)
            return false;

        if (hitCollider.GetComponentInParent<MiniKrug>() != null)
            return false;

        if (hitCollider.GetComponentInParent<BossEnemy>() != null)
            return false;

        return true;
    }

    void SettleAt(Vector3 groundPoint)
    {
        settled = true;
        basePosition = groundPoint + Vector3.up * hoverHeight;
        transform.position = basePosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        SetCollidersTrigger(true);
    }

    void SetCollidersTrigger(bool isTrigger)
    {
        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].isTrigger = isTrigger;
        }
    }

    void FloatAndRotate()
    {
        float bob = Mathf.Sin((Time.time * bobSpeed) + phaseOffset) * bobHeight;
        transform.position = basePosition + Vector3.up * bob;
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    void TryAutoCollect()
    {
        if (!autoCollect)
            return;

        if (Time.time - spawnTime < Mathf.Max(0f, autoCollectDelay))
            return;

        if (item == null)
            item = GetComponent<Item>();

        if (item == null || string.IsNullOrWhiteSpace(item.itemName))
            return;

        PlayerInteraction[] players = GetCachedPlayers();
        float collectRadiusSqr = collectRadius * collectRadius;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerInteraction player = players[i];
            if (player == null || !player.isActiveAndEnabled)
                continue;

            if ((transform.position - player.transform.position).sqrMagnitude > collectRadiusSqr)
                continue;

            collecting = player.TryPickup(item);
            if (collecting)
                return;
        }
    }

    static PlayerInteraction[] GetCachedPlayers()
    {
        if (Time.unscaledTime >= nextPlayerCacheRefreshTime || cachedPlayers == null)
        {
            nextPlayerCacheRefreshTime = Time.unscaledTime + PlayerCacheRefreshInterval;
            cachedPlayers = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
        }

        return cachedPlayers ?? System.Array.Empty<PlayerInteraction>();
    }
}
