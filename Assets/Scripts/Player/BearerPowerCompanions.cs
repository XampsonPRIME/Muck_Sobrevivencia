using System.Collections.Generic;
using UnityEngine;

public class BearerWolfCompanion : MonoBehaviour
{
    PlayerMovement owner;
    Component target;
    float nextTargetSearchTime;
    float nextAttackTime;
    float spiritBuffEndTime;
    Transform visualRoot;

    public static BearerWolfCompanion Spawn(PlayerMovement owner)
    {
        GameObject wolfObject = new GameObject("LoboDoPortador");
        wolfObject.transform.position = owner.transform.position - owner.transform.right * 1.5f;
        BearerWolfCompanion wolf = wolfObject.AddComponent<BearerWolfCompanion>();
        wolf.owner = owner;
        wolf.BuildVisual();
        return wolf;
    }

    public void ActivateSpiritBuff(float duration)
    {
        spiritBuffEndTime = Mathf.Max(spiritBuffEndTime, Time.time + Mathf.Max(1f, duration));
        BearerPowerCombatUtility.SpawnPulse(transform.position, new Color(0.35f, 1f, 0.18f, 1f), 2.5f);
    }

    public void Recall()
    {
        if (owner == null)
            return;

        transform.position = owner.transform.position - owner.transform.right * 1.5f;
        target = null;
    }

    void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (GameState.IsPlayerDead)
            return;

        if (Time.time >= nextTargetSearchTime)
        {
            nextTargetSearchTime = Time.time + 0.45f;
            target = BearerPowerCombatUtility.FindNearestTarget(transform.position, 13f);
        }

        Vector3 destination = target != null
            ? target.transform.position
            : owner.transform.position - owner.transform.right * 1.4f - owner.transform.forward * 0.8f;

        float distance = Vector3.Distance(transform.position, destination);
        float attackRange = 1.7f;
        if (target != null && distance <= attackRange)
        {
            Face(destination);
            TryAttack();
        }
        else if (distance > 0.75f)
        {
            MoveTowards(destination, Time.time < spiritBuffEndTime ? 8.4f : 6.4f);
        }

        if (Vector3.Distance(transform.position, owner.transform.position) > 28f)
            Recall();

        AnimateVisual(distance > 0.75f);
    }

    void TryAttack()
    {
        if (target == null || Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + (Time.time < spiritBuffEndTime ? 0.65f : 1.05f);
        int damage = Time.time < spiritBuffEndTime ? 26 : 14;
        if (!BearerPowerCombatUtility.ApplyDamage(target, owner, damage))
            target = null;
        else
            BearerPowerCombatUtility.SpawnPulse(target.transform.position, new Color(0.34f, 0.9f, 0.16f, 1f), 0.8f, 0.22f);
    }

    void MoveTowards(Vector3 destination, float speed)
    {
        Vector3 flatDestination = new Vector3(destination.x, transform.position.y, destination.z);
        transform.position = Vector3.MoveTowards(transform.position, flatDestination, speed * Time.deltaTime);
        Face(flatDestination);
        SnapToGround();
    }

    void Face(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
    }

    void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 4f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.35f, transform.position.z);
    }

    void AnimateVisual(bool moving)
    {
        if (visualRoot == null)
            return;

        float bob = moving ? Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.09f : Mathf.Sin(Time.time * 2f) * 0.025f;
        visualRoot.localPosition = Vector3.up * bob;
    }

    void BuildVisual()
    {
        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);
        Material fur = CreateMaterial(new Color(0.18f, 0.24f, 0.13f, 1f));
        Material glow = CreateMaterial(new Color(0.35f, 1f, 0.18f, 1f), true);

        CreatePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.65f, 0f), new Vector3(0.48f, 0.62f, 0.75f), Quaternion.Euler(90f, 0f, 0f), fur);
        CreatePart("Head", PrimitiveType.Cube, new Vector3(0f, 0.83f, 0.72f), new Vector3(0.55f, 0.48f, 0.65f), Quaternion.identity, fur);
        CreatePart("Snout", PrimitiveType.Cube, new Vector3(0f, 0.72f, 1.08f), new Vector3(0.3f, 0.25f, 0.36f), Quaternion.identity, fur);
        CreatePart("EarL", PrimitiveType.Cube, new Vector3(-0.2f, 1.15f, 0.68f), new Vector3(0.16f, 0.34f, 0.16f), Quaternion.Euler(0f, 0f, -12f), fur);
        CreatePart("EarR", PrimitiveType.Cube, new Vector3(0.2f, 1.15f, 0.68f), new Vector3(0.16f, 0.34f, 0.16f), Quaternion.Euler(0f, 0f, 12f), fur);
        CreatePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.91f, 1.04f), Vector3.one * 0.09f, Quaternion.identity, glow);
        CreatePart("EyeR", PrimitiveType.Sphere, new Vector3(0.18f, 0.91f, 1.04f), Vector3.one * 0.09f, Quaternion.identity, glow);

        CreatePart("LegFL", PrimitiveType.Cylinder, new Vector3(-0.3f, 0.28f, 0.42f), new Vector3(0.11f, 0.36f, 0.11f), Quaternion.identity, fur);
        CreatePart("LegFR", PrimitiveType.Cylinder, new Vector3(0.3f, 0.28f, 0.42f), new Vector3(0.11f, 0.36f, 0.11f), Quaternion.identity, fur);
        CreatePart("LegBL", PrimitiveType.Cylinder, new Vector3(-0.3f, 0.28f, -0.42f), new Vector3(0.11f, 0.36f, 0.11f), Quaternion.identity, fur);
        CreatePart("LegBR", PrimitiveType.Cylinder, new Vector3(0.3f, 0.28f, -0.42f), new Vector3(0.11f, 0.36f, 0.11f), Quaternion.identity, fur);
        CreatePart("Tail", PrimitiveType.Cylinder, new Vector3(0f, 0.78f, -0.78f), new Vector3(0.11f, 0.48f, 0.11f), Quaternion.Euler(55f, 0f, 0f), fur);
    }

    GameObject CreatePart(string partName, PrimitiveType primitive, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = partName;
        part.transform.SetParent(visualRoot, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        return part;
    }

    static Material CreateMaterial(Color color, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "BearerWolfMaterial",
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

public class BearerShadowClone : MonoBehaviour
{
    static readonly List<BearerShadowClone> activeClones = new List<BearerShadowClone>();

    PlayerMovement owner;
    Component target;
    float expiresAt;
    float nextAttackTime;
    float nextTargetSearchTime;
    Transform visualRoot;

    public static BearerShadowClone Spawn(PlayerMovement owner, float duration)
    {
        if (owner == null)
            return null;

        PlayerProgression progression = owner.GetComponent<PlayerProgression>();
        if (progression != null && progression.currentLevel < 10)
        {
            Debug.LogWarning("CloneSombrio bloqueado antes do nivel 10.");
            return null;
        }

        DestroyAllForOwner(owner);

        GameObject cloneObject = new GameObject("CloneSombrio");
        cloneObject.transform.position = owner.transform.position + owner.transform.right * 1.2f;
        BearerShadowClone clone = cloneObject.AddComponent<BearerShadowClone>();
        clone.owner = owner;
        clone.expiresAt = Time.time + Mathf.Max(1f, duration);
        clone.BuildVisual();
        activeClones.Add(clone);
        BearerPowerCombatUtility.SpawnPulse(cloneObject.transform.position, new Color(0.58f, 0.12f, 1f, 1f), 2f);
        return clone;
    }

    public static void DestroyAllForOwner(PlayerMovement owner = null)
    {
        for (int i = activeClones.Count - 1; i >= 0; i--)
        {
            BearerShadowClone clone = activeClones[i];
            if (clone == null)
            {
                activeClones.RemoveAt(i);
                continue;
            }

            if (owner != null && clone.owner != owner)
                continue;

            activeClones.RemoveAt(i);
            UnityEngine.Object.Destroy(clone.gameObject);
        }
    }

    void OnDestroy()
    {
        activeClones.Remove(this);
    }

    void Update()
    {
        if (owner == null || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        PlayerMovement currentPlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (currentPlayer != null && currentPlayer != owner)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time >= nextTargetSearchTime)
        {
            nextTargetSearchTime = Time.time + 0.35f;
            target = BearerPowerCombatUtility.FindNearestTarget(transform.position, 12f);
        }

        Vector3 destination = target != null
            ? target.transform.position
            : owner.transform.position + owner.transform.right * 1.3f;
        float distance = Vector3.Distance(transform.position, destination);

        if (target != null && distance <= 2f)
        {
            Face(destination);
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + 0.82f;
                BearerPowerCombatUtility.ApplyDamage(target, owner, 18);
                BearerPowerCombatUtility.SpawnPulse(target.transform.position, new Color(0.5f, 0.08f, 0.9f, 1f), 0.75f, 0.2f);
            }
        }
        else
        {
            Vector3 flatDestination = new Vector3(destination.x, transform.position.y, destination.z);
            transform.position = Vector3.MoveTowards(transform.position, flatDestination, 7.5f * Time.deltaTime);
            Face(flatDestination);
            SnapToGround();
        }

        if (visualRoot != null)
            visualRoot.localPosition = Vector3.up * (0.08f + Mathf.Sin(Time.time * 5f) * 0.06f);
    }

    void Face(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 14f * Time.deltaTime);
    }

    void SnapToGround()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.05f, transform.position.z);
    }

    void BuildVisual()
    {
        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);
        Material shadow = CreateMaterial(new Color(0.12f, 0.02f, 0.18f, 0.82f));
        Material glow = CreateMaterial(new Color(0.65f, 0.15f, 1f, 1f), true);

        CreatePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.95f, 0.5f), Quaternion.identity, shadow);
        CreatePart("Head", PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.48f, Quaternion.identity, shadow);
        CreatePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.16f, 2f, 0.4f), Vector3.one * 0.07f, Quaternion.identity, glow);
        CreatePart("EyeR", PrimitiveType.Sphere, new Vector3(0.16f, 2f, 0.4f), Vector3.one * 0.07f, Quaternion.identity, glow);
        CreatePart("ArmL", PrimitiveType.Capsule, new Vector3(-0.52f, 1.1f, 0f), new Vector3(0.2f, 0.72f, 0.2f), Quaternion.Euler(0f, 0f, -8f), shadow);
        CreatePart("ArmR", PrimitiveType.Capsule, new Vector3(0.52f, 1.1f, 0f), new Vector3(0.2f, 0.72f, 0.2f), Quaternion.Euler(0f, 0f, 8f), shadow);
    }

    void CreatePart(string partName, PrimitiveType primitive, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = partName;
        part.transform.SetParent(visualRoot, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    static Material CreateMaterial(Color color, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "ShadowCloneMaterial",
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
