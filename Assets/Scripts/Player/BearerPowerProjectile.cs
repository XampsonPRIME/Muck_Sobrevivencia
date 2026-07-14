using UnityEngine;

public class BearerPowerProjectile : MonoBehaviour
{
    PlayerMovement owner;
    Vector3 direction;
    float speed;
    float remainingDistance;
    float explosionRadius;
    int damage;
    Color color;
    bool exploded;

    public static BearerPowerProjectile Spawn(
        PlayerMovement owner,
        Vector3 position,
        Vector3 direction,
        float speed,
        float range,
        float explosionRadius,
        int damage,
        Color color)
    {
        GameObject projectileObject = new GameObject("BearerPowerProjectile");
        projectileObject.transform.position = position;
        BearerPowerProjectile projectile = projectileObject.AddComponent<BearerPowerProjectile>();
        projectile.owner = owner;
        projectile.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        projectile.speed = Mathf.Max(1f, speed);
        projectile.remainingDistance = Mathf.Max(1f, range);
        projectile.explosionRadius = Mathf.Max(0.2f, explosionRadius);
        projectile.damage = Mathf.Max(1, damage);
        projectile.color = color;
        projectile.BuildVisual();
        return projectile;
    }

    void Update()
    {
        if (exploded)
            return;

        float distance = Mathf.Min(remainingDistance, speed * Time.deltaTime);
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            0.18f,
            direction,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);
        bool foundHit = false;
        RaycastHit nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null ||
                (owner != null && (hitTransform == owner.transform || hitTransform.IsChildOf(owner.transform))))
            {
                continue;
            }

            if (hits[i].distance >= nearestDistance)
                continue;

            foundHit = true;
            nearestDistance = hits[i].distance;
            nearestHit = hits[i];
        }

        if (foundHit)
        {
            transform.position = nearestHit.point;
            Explode();
            return;
        }

        transform.position += direction * distance;
        transform.Rotate(95f * Time.deltaTime, 150f * Time.deltaTime, 0f, Space.Self);
        remainingDistance -= distance;

        if (remainingDistance <= 0.01f)
            Explode();
    }

    void BuildVisual()
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "ProjectileCore";
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * 0.42f;
        Collider collider = core.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "BearerProjectileMaterial",
            color = color
        };
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 3f);
        }

        Renderer renderer = core.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = material;

        Light light = gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 1.6f;
        light.range = 3.5f;
        light.shadows = LightShadows.None;
    }

    void Explode()
    {
        if (exploded)
            return;

        exploded = true;
        BearerPowerCombatUtility.SpawnPulse(transform.position, color, explosionRadius, 0.45f);
        BearerPowerCombatUtility.DamageRadius(transform.position, explosionRadius, owner, damage, 3.5f);
        Destroy(gameObject);
    }
}

public class BearerStoneWall : MonoBehaviour
{
    float expiresAt;

    public static void Spawn(Vector3 center, Vector3 forward, Color runeColor, float duration)
    {
        GameObject wallObject = new GameObject("BearerStoneWall");
        wallObject.transform.position = center;
        wallObject.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward);
        BearerStoneWall wall = wallObject.AddComponent<BearerStoneWall>();
        wall.expiresAt = Time.time + Mathf.Max(1f, duration);
        wall.Build(runeColor);
    }

    void Update()
    {
        if (Time.time >= expiresAt)
            Destroy(gameObject);
    }

    void Build(Color runeColor)
    {
        Material stone = CreateMaterial(new Color(0.25f, 0.21f, 0.17f, 1f), false);
        Material rune = CreateMaterial(runeColor, true);

        for (int i = -2; i <= 2; i++)
        {
            float height = i == 0 ? 2.7f : i == -1 || i == 1 ? 2.35f : 1.9f;
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = $"WallStone_{i + 2}";
            block.transform.SetParent(transform, false);
            block.transform.localPosition = new Vector3(i * 0.9f, height * 0.5f, 0f);
            block.transform.localRotation = Quaternion.Euler(0f, 0f, i * -3f);
            block.transform.localScale = new Vector3(1.05f, height, 0.7f);
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = stone;
        }

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "WallRune";
        mark.transform.SetParent(transform, false);
        mark.transform.localPosition = new Vector3(0f, 1.45f, -0.37f);
        mark.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        mark.transform.localScale = new Vector3(0.42f, 0.42f, 0.05f);
        Renderer markRenderer = mark.GetComponent<Renderer>();
        if (markRenderer != null)
            markRenderer.sharedMaterial = rune;
        Collider markCollider = mark.GetComponent<Collider>();
        if (markCollider != null)
            Destroy(markCollider);
    }

    static Material CreateMaterial(Color color, bool emissive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = emissive ? "StoneWallRuneMaterial" : "StoneWallMaterial",
            color = color
        };
        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }
        return material;
    }
}
