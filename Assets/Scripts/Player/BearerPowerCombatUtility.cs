using System.Collections.Generic;
using UnityEngine;

public static class BearerPowerCombatUtility
{
    public static List<Component> FindTargets(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        List<Component> targets = new List<Component>();
        HashSet<int> seen = new HashSet<int>();

        for (int i = 0; i < hits.Length; i++)
        {
            Component target = ResolveDamageable(hits[i]);
            if (target == null || !seen.Add(target.GetInstanceID()))
                continue;

            targets.Add(target);
        }

        return targets;
    }

    public static Component FindNearestTarget(Vector3 center, float radius)
    {
        List<Component> targets = FindTargets(center, radius);
        Component nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < targets.Count; i++)
        {
            Component target = targets[i];
            if (target == null)
                continue;

            float sqrDistance = (target.transform.position - center).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            nearest = target;
        }

        return nearest;
    }

    public static Component ResolveDamageable(Collider hit)
    {
        if (hit == null)
            return null;

        Component target = hit.GetComponentInParent<KaelTorGuardian>();
        if (target != null)
            return target;

        target = hit.GetComponentInParent<EarthGolem>();
        if (target != null)
            return target;

        target = hit.GetComponentInParent<BossEnemy>();
        if (target != null)
            return target;

        target = hit.GetComponentInParent<MiniKrug>();
        if (target != null)
            return target;

        target = hit.GetComponentInParent<WildBoar>();
        if (target != null)
            return target;

        target = hit.GetComponentInParent<WildChicken>();
        if (target != null)
            return target;

        return hit.GetComponentInParent<Cow>();
    }

    public static bool ApplyDamage(Component target, PlayerMovement attacker, int damage)
    {
        if (target == null || attacker == null)
            return false;

        damage = Mathf.Max(1, damage);
        LanMultiplayerManager manager = LanMultiplayerManager.Instance;
        if (target is not KaelTorGuardian &&
            manager != null &&
            manager.TryHandleGameplayHit(target, attacker, ToolType.Axe, damage))
        {
            RegisterCombat(target, attacker);
            return true;
        }

        switch (target)
        {
            case KaelTorGuardian guardian:
                guardian.Hit(damage, attacker);
                break;
            case EarthGolem golem:
                golem.Hit(damage, attacker);
                break;
            case BossEnemy boss:
                if (!boss.CanBeChallengedBy(attacker))
                {
                    MessageSystem.Instance?.ShowMessage(boss.BuildMinimumLevelMessage());
                    return false;
                }
                boss.Hit(damage, attacker);
                break;
            case MiniKrug miniKrug:
                miniKrug.Hit(damage, attacker);
                break;
            case WildBoar boar:
                boar.Hit(damage, attacker);
                break;
            case WildChicken chicken:
                chicken.Hit(damage, attacker.transform.position);
                break;
            case Cow cow:
                cow.Hit(damage);
                break;
            default:
                return false;
        }

        RegisterCombat(target, attacker);
        return true;
    }

    public static int DamageRadius(
        Vector3 center,
        float radius,
        PlayerMovement attacker,
        int damage,
        float knockbackForce = 0f)
    {
        List<Component> targets = FindTargets(center, radius);
        int affected = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            Component target = targets[i];
            if (!ApplyDamage(target, attacker, damage))
                continue;

            affected++;
            if (knockbackForce > 0f)
            {
                Vector3 direction = target.transform.position - center;
                direction.y = 0f;
                ApplyImpulse(target, direction, knockbackForce, 0.42f);
            }
        }

        return affected;
    }

    public static void ApplyRoot(Component target, float duration)
    {
        BearerPowerStatusEffect status = EnsureStatus(target);
        status?.ApplyRoot(duration);
    }

    public static void ApplyPull(Component target, Vector3 destination, float duration, float speed)
    {
        BearerPowerStatusEffect status = EnsureStatus(target);
        status?.ApplyPull(destination, duration, speed);
    }

    public static void ApplyImpulse(Component target, Vector3 direction, float force, float duration)
    {
        BearerPowerStatusEffect status = EnsureStatus(target);
        status?.ApplyImpulse(direction, force, duration);
    }

    public static void SpawnPulse(Vector3 position, Color color, float radius, float duration = 0.55f)
    {
        GameObject pulse = new GameObject("BearerPowerPulse");
        pulse.transform.position = position;
        BearerPowerPulse effect = pulse.AddComponent<BearerPowerPulse>();
        effect.Configure(color, radius, duration);
    }

    static BearerPowerStatusEffect EnsureStatus(Component target)
    {
        if (target == null)
            return null;

        return target.GetComponent<BearerPowerStatusEffect>() ??
               target.gameObject.AddComponent<BearerPowerStatusEffect>();
    }

    static void RegisterCombat(Component target, PlayerMovement attacker)
    {
        if (target is BossEnemy || target is EarthGolem || target is KaelTorGuardian || target is MiniKrug)
            attacker.RegisterBossOrMiniBossCombat();
    }
}

public class BearerPowerStatusEffect : MonoBehaviour
{
    float rootEndTime;
    Vector3 rootPosition;
    float pullEndTime;
    Vector3 pullDestination;
    float pullSpeed;
    float impulseEndTime;
    Vector3 impulseVelocity;
    GameObject rootVisual;

    public void ApplyRoot(float duration)
    {
        rootPosition = transform.position;
        rootEndTime = Mathf.Max(rootEndTime, Time.time + Mathf.Max(0.1f, duration));
        EnsureRootVisual();
    }

    public void ApplyPull(Vector3 destination, float duration, float speed)
    {
        pullDestination = destination;
        pullEndTime = Mathf.Max(pullEndTime, Time.time + Mathf.Max(0.1f, duration));
        pullSpeed = Mathf.Max(pullSpeed, speed);
    }

    public void ApplyImpulse(Vector3 direction, float force, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        impulseVelocity = direction.normalized * Mathf.Max(0f, force);
        impulseEndTime = Time.time + Mathf.Max(0.05f, duration);
    }

    void LateUpdate()
    {
        if (Time.time < pullEndTime)
        {
            Vector3 current = transform.position;
            Vector3 destination = new Vector3(pullDestination.x, current.y, pullDestination.z);
            transform.position = Vector3.MoveTowards(current, destination, pullSpeed * Time.deltaTime);
        }

        if (Time.time < impulseEndTime)
        {
            transform.position += impulseVelocity * Time.deltaTime;
            impulseVelocity = Vector3.MoveTowards(impulseVelocity, Vector3.zero, 24f * Time.deltaTime);
        }

        if (Time.time < rootEndTime)
        {
            Vector3 current = transform.position;
            transform.position = new Vector3(rootPosition.x, current.y, rootPosition.z);
        }
        else if (rootVisual != null)
        {
            Destroy(rootVisual);
        }
    }

    void EnsureRootVisual()
    {
        if (rootVisual != null)
            return;

        rootVisual = new GameObject("EntanglingRoots");
        rootVisual.transform.SetParent(transform, false);
        rootVisual.transform.localPosition = Vector3.zero;
        Material material = CreateMaterial(new Color(0.2f, 0.72f, 0.12f, 1f));

        for (int i = 0; i < 5; i++)
        {
            float angle = i / 5f * Mathf.PI * 2f;
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = $"Root_{i}";
            root.transform.SetParent(rootVisual.transform, false);
            root.transform.localPosition = new Vector3(Mathf.Sin(angle) * 0.55f, 0.35f, Mathf.Cos(angle) * 0.55f);
            root.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 28f);
            root.transform.localScale = new Vector3(0.07f, 0.55f, 0.07f);
            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            Collider collider = root.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }
    }

    static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "BearerRootMaterial",
            color = color
        };
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.65f);
        }
        return material;
    }
}

public class BearerPowerPulse : MonoBehaviour
{
    Color color;
    float radius;
    float duration;
    float elapsed;
    Transform ring;
    Material material;

    public void Configure(Color configuredColor, float configuredRadius, float configuredDuration)
    {
        color = configuredColor;
        radius = Mathf.Max(0.2f, configuredRadius);
        duration = Mathf.Max(0.1f, configuredDuration);
    }

    void Start()
    {
        GameObject ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObject.name = "PulseRing";
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        ringObject.transform.localScale = new Vector3(0.1f, 0.025f, 0.1f);
        Collider collider = ringObject.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        material = CreateTransparentMaterial(color);
        Renderer renderer = ringObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        ring = ringObject.transform;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float scale = Mathf.Lerp(0.1f, radius * 2f, 1f - Mathf.Pow(1f - t, 2f));
        if (ring != null)
            ring.localScale = new Vector3(scale, 0.025f, scale);

        if (material != null)
        {
            Color faded = color;
            faded.a = 1f - t;
            material.color = faded;
        }

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    static Material CreateTransparentMaterial(Color value)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        Material result = new Material(shader)
        {
            name = "BearerPulseMaterial",
            color = value
        };
        if (result.HasProperty("_Surface"))
            result.SetFloat("_Surface", 1f);
        if (result.HasProperty("_ZWrite"))
            result.SetFloat("_ZWrite", 0f);
        result.renderQueue = 3000;
        return result;
    }
}
