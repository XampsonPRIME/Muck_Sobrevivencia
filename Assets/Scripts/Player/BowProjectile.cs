using UnityEngine;

public enum ArrowEffectType
{
    Basic,
    Fire,
    Poison,
    Explosive
}

public class BowProjectile : MonoBehaviour
{
    public int damage = 15;
    public float speed = 32f;
    public float maxDistance = 40f;
    public ArrowEffectType effectType = ArrowEffectType.Basic;
    public LayerMask hitMask = ~0;

    PlayerMovement attacker;
    Transform ownerTransform;
    Rigidbody rb;
    bool launched;
    bool impacted;
    Vector3 startPosition;
    Vector3 launchDirection = Vector3.forward;
    static Material arrowWoodMaterial;
    static Material arrowStoneMaterial;
    static Material arrowFeatherMaterial;
    static Material arrowEnemyImpactMaterial;
    static Material arrowObjectImpactMaterial;

    public void Launch(PlayerMovement shotAttacker, Vector3 direction, int shotDamage, float shotSpeed, float shotRange, ArrowEffectType shotEffectType = ArrowEffectType.Basic)
    {
        attacker = shotAttacker;
        ownerTransform = shotAttacker != null ? shotAttacker.transform : null;
        damage = Mathf.Max(1, shotDamage);
        speed = Mathf.Max(1f, shotSpeed);
        maxDistance = Mathf.Max(1f, shotRange);
        effectType = shotEffectType;
        launchDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;

        EnsureProjectile();
        IgnoreOwnerColliders();

        transform.rotation = Quaternion.LookRotation(launchDirection, Vector3.up);
        startPosition = transform.position;
        launched = true;

        rb.linearVelocity = launchDirection * speed;
    }

    void Start()
    {
        EnsureProjectile();

        if (!launched)
        {
            startPosition = transform.position;
            launchDirection = transform.forward;
            rb.linearVelocity = transform.forward * speed;
            launched = true;
        }
    }

    void FixedUpdate()
    {
        if (impacted)
            return;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized, Vector3.up);

        if ((transform.position - startPosition).sqrMagnitude >= maxDistance * maxDistance)
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null || impacted)
            return;

        Vector3 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        HandleImpact(collision.collider, impactPoint);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null || impacted)
            return;

        HandleImpact(other, transform.position);
    }

    void EnsureProjectile()
    {
        if (GetComponentInChildren<Renderer>() == null)
            BuildVisual();

        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        if (collider == null)
            collider = gameObject.AddComponent<CapsuleCollider>();

        collider.direction = 2;
        collider.radius = 0.055f;
        collider.height = 0.88f;
        collider.center = new Vector3(0f, 0f, 0.2f);
        collider.isTrigger = false;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.mass = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void BuildVisual()
    {
        Material wood = GetArrowWoodMaterial();
        Material stone = GetArrowStoneMaterial();
        Material feather = GetArrowFeatherMaterial();

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(transform, false);
        shaft.transform.localPosition = Vector3.zero;
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localScale = new Vector3(0.035f, 0.38f, 0.035f);
        RemoveCollider(shaft);
        shaft.GetComponent<Renderer>().sharedMaterial = wood;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "StoneTip";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.43f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.13f, 0.08f, 0.13f);
        RemoveCollider(head);
        head.GetComponent<Renderer>().sharedMaterial = stone;

        CreateFeather("FeatherL", new Vector3(-0.08f, 0f, -0.34f), Quaternion.Euler(0f, 0f, 24f), feather);
        CreateFeather("FeatherR", new Vector3(0.08f, 0f, -0.34f), Quaternion.Euler(0f, 0f, -24f), feather);
    }

    void CreateFeather(string featherName, Vector3 localPosition, Quaternion localRotation, Material material)
    {
        GameObject feather = GameObject.CreatePrimitive(PrimitiveType.Cube);
        feather.name = featherName;
        feather.transform.SetParent(transform, false);
        feather.transform.localPosition = localPosition;
        feather.transform.localRotation = localRotation;
        feather.transform.localScale = new Vector3(0.045f, 0.02f, 0.22f);
        RemoveCollider(feather);
        feather.GetComponent<Renderer>().sharedMaterial = material;
    }

    void RemoveCollider(GameObject target)
    {
        Collider collider = target != null ? target.GetComponent<Collider>() : null;
        if (collider != null)
            Destroy(collider);
    }

    void IgnoreOwnerColliders()
    {
        if (ownerTransform == null)
            return;

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider == null)
            return;

        Collider[] ownerColliders = ownerTransform.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null)
                Physics.IgnoreCollision(ownCollider, ownerColliders[i], true);
        }
    }

    void HandleImpact(Collider other, Vector3 impactPoint)
    {
        if (IsOwnerCollider(other))
            return;

        impacted = true;
        bool damagedTarget = TryApplyDamage(other);
        BowCombatAudio.PlayImpact(impactPoint, damagedTarget);
        CreateImpactVisual(impactPoint, damagedTarget);
        Destroy(gameObject);
    }

    bool IsOwnerCollider(Collider other)
    {
        if (other == null || ownerTransform == null)
            return false;

        return other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform);
    }

    bool TryApplyDamage(Collider other)
    {
        MiniKrug miniKrug = other.GetComponent<MiniKrug>() ?? other.GetComponentInParent<MiniKrug>();
        if (miniKrug != null)
        {
            attacker?.RegisterBossOrMiniBossCombat();
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(miniKrug, attacker, ToolType.Bow, damage))
                return true;

            miniKrug.Hit(damage, attacker);
            return true;
        }

        BossEnemy boss = other.GetComponent<BossEnemy>() ?? other.GetComponentInParent<BossEnemy>();
        if (boss != null)
        {
            if (!boss.CanBeChallengedBy(attacker))
            {
                MessageSystem.Instance?.ShowMessage(boss.BuildMinimumLevelMessage());
                return false;
            }

            attacker?.RegisterBossOrMiniBossCombat();
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(boss, attacker, ToolType.Bow, damage))
                return true;

            boss.Hit(damage, attacker);
            return true;
        }

        WildBoar boar = other.GetComponent<WildBoar>() ?? other.GetComponentInParent<WildBoar>();
        if (boar != null)
        {
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(boar, attacker, ToolType.Bow, damage))
                return true;

            boar.Hit(damage, attacker);
            return true;
        }

        EarthGolem earthGolem = other.GetComponent<EarthGolem>() ?? other.GetComponentInParent<EarthGolem>();
        if (earthGolem != null)
        {
            attacker?.RegisterBossOrMiniBossCombat();

            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(earthGolem, attacker, ToolType.Bow, damage))
                return true;

            earthGolem.Hit(damage, attacker);
            return true;
        }

        KaelTorGuardian kaelTor = other.GetComponent<KaelTorGuardian>() ?? other.GetComponentInParent<KaelTorGuardian>();
        if (kaelTor != null)
        {
            attacker?.RegisterBossOrMiniBossCombat();
            kaelTor.Hit(damage, attacker);
            return true;
        }

        WildChicken chicken = other.GetComponent<WildChicken>() ?? other.GetComponentInParent<WildChicken>();
        if (chicken != null)
        {
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(chicken, attacker, ToolType.Bow, damage))
                return true;

            Vector3 threatPosition = attacker != null ? attacker.transform.position : transform.position - launchDirection;
            chicken.Hit(damage, threatPosition);
            return true;
        }

        Cow cow = other.GetComponent<Cow>() ?? other.GetComponentInParent<Cow>();
        if (cow != null)
        {
            if (LanMultiplayerManager.Instance != null &&
                LanMultiplayerManager.Instance.TryHandleGameplayHit(cow, attacker, ToolType.Bow, damage))
                return true;

            cow.Hit(damage);
            return true;
        }

        return false;
    }

    void CreateImpactVisual(Vector3 impactPoint, bool damagedTarget)
    {
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = damagedTarget ? "ArrowEnemyImpact" : "ArrowImpact";
        effect.transform.position = impactPoint;
        effect.transform.localScale = Vector3.one * (damagedTarget ? 0.22f : 0.14f);

        Collider collider = effect.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = effect.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = damagedTarget
                ? GetArrowEnemyImpactMaterial()
                : GetArrowObjectImpactMaterial();
        }

        Destroy(effect, 0.28f);
    }

    static Material GetArrowWoodMaterial()
    {
        return arrowWoodMaterial ??= BowCombatAudio.CreateRuntimeMaterial("ArrowWoodRuntime", new Color(0.54f, 0.31f, 0.13f, 1f));
    }

    static Material GetArrowStoneMaterial()
    {
        return arrowStoneMaterial ??= BowCombatAudio.CreateRuntimeMaterial("ArrowStoneRuntime", new Color(0.62f, 0.62f, 0.58f, 1f));
    }

    static Material GetArrowFeatherMaterial()
    {
        return arrowFeatherMaterial ??= BowCombatAudio.CreateRuntimeMaterial("ArrowFeatherRuntime", new Color(0.92f, 0.86f, 0.68f, 1f));
    }

    static Material GetArrowEnemyImpactMaterial()
    {
        return arrowEnemyImpactMaterial ??= BowCombatAudio.CreateRuntimeMaterial("ArrowEnemyImpact", new Color(1f, 0.28f, 0.16f, 0.9f));
    }

    static Material GetArrowObjectImpactMaterial()
    {
        return arrowObjectImpactMaterial ??= BowCombatAudio.CreateRuntimeMaterial("ArrowImpact", new Color(0.72f, 0.66f, 0.55f, 0.8f));
    }
}

public static class BowCombatAudio
{
    static AudioClip drawClip;
    static AudioClip releaseClip;
    static AudioClip enemyImpactClip;
    static AudioClip objectImpactClip;

    public static void PlayDraw(Vector3 position)
    {
        if (drawClip == null)
            drawClip = CreateClip("BowDrawRuntime", 0.18f, 170f, 0.18f, 0.04f);

        AudioSource.PlayClipAtPoint(drawClip, position, 0.28f);
    }

    public static void PlayRelease(Vector3 position)
    {
        if (releaseClip == null)
            releaseClip = CreateClip("BowReleaseRuntime", 0.16f, 540f, 0.22f, 0.12f);

        AudioSource.PlayClipAtPoint(releaseClip, position, 0.42f);
    }

    public static void PlayImpact(Vector3 position, bool enemy)
    {
        if (enemy)
        {
            if (enemyImpactClip == null)
                enemyImpactClip = CreateClip("ArrowEnemyImpactRuntime", 0.12f, 120f, 0.28f, 0.18f);

            AudioSource.PlayClipAtPoint(enemyImpactClip, position, 0.36f);
            return;
        }

        if (objectImpactClip == null)
            objectImpactClip = CreateClip("ArrowObjectImpactRuntime", 0.1f, 260f, 0.2f, 0.1f);

        AudioSource.PlayClipAtPoint(objectImpactClip, position, 0.26f);
    }

    public static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        return RuntimeMaterialUtility.Create(materialName, color);
    }

    static AudioClip CreateClip(string clipName, float duration, float baseFrequency, float tonalAmount, float noiseAmount)
    {
        int sampleRate = 22050;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalized = i / (float)sampleCount;
            float envelope = Mathf.Exp(-normalized * 7f);
            float tone = Mathf.Sin(t * baseFrequency * Mathf.PI * 2f) * tonalAmount;
            float noise = Mathf.Sin((i * 12.9898f + 78.233f) * 0.17f) * noiseAmount;
            samples[i] = (tone + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
