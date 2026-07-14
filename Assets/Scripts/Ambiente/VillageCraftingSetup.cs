using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class VillageCraftingSetup : MonoBehaviour
{
    [SerializeField] Item gravetoItem;
    [SerializeField] Vector3 benchLocalPosition = new Vector3(19f, 1.45f, 72f);
    [SerializeField] Vector3 npcLocalPosition = new Vector3(18.5f, 1.65f, 63f);
    [SerializeField] Vector3 vendorLocalPosition = new Vector3(23.2f, 1.65f, 63f);
    [SerializeField] Vector3 chestLocalPosition = new Vector3(21.8f, 1.55f, 72f);
    [SerializeField] Vector3 facingEulerAngles = new Vector3(0f, 90f, 0f);
    [SerializeField] string npcProfessionName = "Profissão";
    [SerializeField] string npcProximityMessage = "Nessa bancada voce pode criar os itens que esse mundo tem a oferecer.";
    [SerializeField] string vendorName = "Mercador da Vila";
    [SerializeField] float localGroundReferenceY = 1f;
    [SerializeField] float groundProbeHeight = 120f;
    [SerializeField] float groundProbeDistance = 260f;
    [SerializeField] float villageGroundPadding = 0.08f;
    [SerializeField] int groundAlignAttempts = 28;
    [SerializeField] float groundAlignInterval = 0.25f;

    Coroutine groundAlignRoutine;

    void Awake()
    {
        EnsureCraftingBench();
        EnsureCraftingNpc();
        EnsureVillageVendor();
        EnsureStarterChest();
    }

    void Start()
    {
        RequestGroundAlign();
    }

    void OnDisable()
    {
        if (groundAlignRoutine != null)
        {
            StopCoroutine(groundAlignRoutine);
            groundAlignRoutine = null;
        }
    }

    public void RequestGroundAlign()
    {
        if (groundAlignRoutine != null)
        {
            StopCoroutine(groundAlignRoutine);
            groundAlignRoutine = null;
        }

        if (TryAlignVillageToGround())
            return;

        if (!isActiveAndEnabled)
            return;

        groundAlignRoutine = StartCoroutine(AlignVillageToGroundRoutine());
    }

    IEnumerator AlignVillageToGroundRoutine()
    {
        int attempts = Mathf.Max(1, groundAlignAttempts);
        for (int i = 0; i < attempts; i++)
        {
            if (TryAlignVillageToGround())
            {
                groundAlignRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, groundAlignInterval));
        }

        groundAlignRoutine = null;
    }

    bool TryAlignVillageToGround()
    {
        if (!TryResolveVillageGroundY(out float groundY))
            return false;

        float referenceY = ResolveLocalGroundReferenceY();
        float desiredRootY = groundY - referenceY + villageGroundPadding;
        Vector3 position = transform.position;
        if (Mathf.Abs(position.y - desiredRootY) > 0.02f)
            transform.position = new Vector3(position.x, desiredRootY, position.z);

        return true;
    }

    bool TryResolveVillageGroundY(out float groundY)
    {
        Vector3[] localSamples =
        {
            new Vector3(11f, 0f, 58f),
            new Vector3(28.25f, 0f, 58f),
            new Vector3(44f, 0f, 64f),
            new Vector3(25.3f, 0f, 81.25f),
            new Vector3(0f, 0f, 75f),
            benchLocalPosition,
            npcLocalPosition,
            vendorLocalPosition,
            chestLocalPosition
        };

        bool foundTerrain = false;
        float highestTerrainY = float.MinValue;
        bool foundFallback = false;
        float highestFallbackY = float.MinValue;

        for (int i = 0; i < localSamples.Length; i++)
        {
            Vector3 sample = localSamples[i];
            Vector3 worldSample = transform.TransformPoint(new Vector3(sample.x, 0f, sample.z));
            if (!TryResolveGroundAt(worldSample, out float sampleGroundY, out bool isTerrain))
                continue;

            if (isTerrain)
            {
                foundTerrain = true;
                highestTerrainY = Mathf.Max(highestTerrainY, sampleGroundY);
            }
            else
            {
                foundFallback = true;
                highestFallbackY = Mathf.Max(highestFallbackY, sampleGroundY);
            }
        }

        if (foundTerrain)
        {
            groundY = highestTerrainY;
            return true;
        }

        groundY = highestFallbackY;
        return foundFallback;
    }

    bool TryResolveGroundAt(Vector3 worldSample, out float groundY, out bool isTerrain)
    {
        float originY = Mathf.Max(worldSample.y + groundProbeHeight, transform.position.y + groundProbeHeight, 80f);
        Vector3 rayOrigin = new Vector3(worldSample.x, originY, worldSample.z);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, groundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
        groundY = 0f;
        isTerrain = false;

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool foundFallback = false;
        float fallbackY = 0f;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null || hit.normal.y < 0.35f)
                continue;

            Transform hitTransform = collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (collider.GetComponentInParent<DistantMountains>() != null ||
                collider.GetComponentInParent<ResourceNode>() != null ||
                collider.GetComponentInParent<CraftingNpc>() != null ||
                collider.GetComponentInParent<VillageChest>() != null ||
                collider.GetComponentInParent<CraftingBench>() != null)
                continue;

            if (collider.GetComponentInParent<TerrainChunk>() != null ||
                collider.GetComponentInParent<ProceduralTerrain>() != null)
            {
                groundY = hit.point.y;
                isTerrain = true;
                return true;
            }

            if (!foundFallback)
            {
                foundFallback = true;
                fallbackY = hit.point.y;
            }
        }

        if (!foundFallback)
            return false;

        groundY = fallbackY;
        return true;
    }

    float ResolveLocalGroundReferenceY()
    {
        Transform groundMarker = transform.Find("Cylinder");
        if (groundMarker != null)
            return groundMarker.localPosition.y;

        return localGroundReferenceY;
    }

    void EnsureCraftingBench()
    {
        Transform existing = transform.Find("CraftingBench");
        if (existing != null)
        {
            CraftingBench existingBench = existing.GetComponent<CraftingBench>();
            if (existingBench != null)
                existingBench.Configure(gravetoItem);

            ColorCraftingBench(existing.gameObject);
            EnsureBenchAccent(existing);
            return;
        }

        GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bench.name = "CraftingBench";
        bench.transform.SetParent(transform, false);
        bench.transform.localPosition = benchLocalPosition;
        bench.transform.localRotation = Quaternion.Euler(facingEulerAngles);
        bench.transform.localScale = new Vector3(2.2f, 0.85f, 1.15f);

        CraftingBench craftingBench = bench.AddComponent<CraftingBench>();
        craftingBench.Configure(gravetoItem);

        ColorCraftingBench(bench);
        CreateBenchLeg(bench.transform, new Vector3(-0.85f, -0.85f, -0.35f));
        CreateBenchLeg(bench.transform, new Vector3(0.85f, -0.85f, -0.35f));
        CreateBenchLeg(bench.transform, new Vector3(-0.85f, -0.85f, 0.35f));
        CreateBenchLeg(bench.transform, new Vector3(0.85f, -0.85f, 0.35f));
        EnsureBenchAccent(bench.transform);
    }

    void CreateBenchLeg(Transform parent, Vector3 localPosition)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = "Leg";
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = localPosition;
        leg.transform.localScale = new Vector3(0.12f, 1.6f, 0.12f);
        ApplyColor(leg, new Color(0.23f, 0.11f, 0.035f, 1f));
    }

    void EnsureCraftingNpc()
    {
        Vector3 resolvedNpcPosition = ResolveNpcOpenPosition(npcLocalPosition, new Vector3(18.5f, 1.65f, 63f));
        Transform existing = transform.Find("CraftingNpc");
        if (existing != null)
        {
            PrepareNpcRoot(existing, resolvedNpcPosition);
            CraftingNpc existingCraftingNpc = existing.GetComponent<CraftingNpc>();
            if (existingCraftingNpc == null)
                existingCraftingNpc = existing.gameObject.AddComponent<CraftingNpc>();
            existingCraftingNpc.Configure(npcProfessionName, npcProximityMessage);

            EnsureNpcVisibleBody(existing, new Color(0.72f, 0.44f, 0.2f, 1f), new Color(0.24f, 0.5f, 0.68f, 1f), false);
            ColorNpc(existing, new Color(0.72f, 0.44f, 0.2f, 1f), new Color(0.24f, 0.5f, 0.68f, 1f));
            EnsureNpcDetails(existing, new Color(0.24f, 0.5f, 0.68f, 1f), "CraftingApron");
            return;
        }

        GameObject npc = new GameObject("CraftingNpc");
        npc.name = "CraftingNpc";
        npc.transform.SetParent(transform, false);
        PrepareNpcRoot(npc.transform, resolvedNpcPosition);

        CraftingNpc craftingNpc = npc.AddComponent<CraftingNpc>();
        craftingNpc.Configure(npcProfessionName, npcProximityMessage);
        EnsureNpcVisibleBody(npc.transform, new Color(0.72f, 0.44f, 0.2f, 1f), new Color(0.24f, 0.5f, 0.68f, 1f), false);
        ColorNpc(npc.transform, new Color(0.72f, 0.44f, 0.2f, 1f), new Color(0.24f, 0.5f, 0.68f, 1f));
        EnsureNpcDetails(npc.transform, new Color(0.24f, 0.5f, 0.68f, 1f), "CraftingApron");
    }

    void EnsureVillageVendor()
    {
        Vector3 resolvedVendorPosition = ResolveNpcOpenPosition(vendorLocalPosition, new Vector3(23.2f, 1.65f, 63f));
        Transform existing = transform.Find("VillageVendor");
        if (existing != null)
        {
            PrepareNpcRoot(existing, resolvedVendorPosition);
            ConfigureVillageVendor(existing.gameObject);
            return;
        }

        GameObject vendor = new GameObject("VillageVendor");
        vendor.name = "VillageVendor";
        vendor.transform.SetParent(transform, false);
        PrepareNpcRoot(vendor.transform, resolvedVendorPosition);

        ConfigureVillageVendor(vendor);
    }

    void ConfigureVillageVendor(GameObject vendor)
    {
        if (vendor == null)
            return;

        VendorShop shop = vendor.GetComponent<VendorShop>();
        if (shop == null)
            shop = vendor.AddComponent<VendorShop>();

        shop.vendorName = vendorName;
        shop.loadOffersFromResources = true;
        shop.resourcesFolder = "VendorItems";
        shop.defaultStock = 3;
        shop.defaultInfiniteStock = false;
        shop.offers.Clear();

        CraftingNpc greeter = vendor.GetComponent<CraftingNpc>();
        if (greeter == null)
            greeter = vendor.AddComponent<CraftingNpc>();
        greeter.Configure("Mercador", "Compre suprimentos e venda achados antes de explorar.");

        EnsureNpcVisibleBody(vendor.transform, new Color(0.68f, 0.48f, 0.22f, 1f), new Color(0.52f, 0.18f, 0.62f, 1f), true);
        ColorNpc(vendor.transform, new Color(0.68f, 0.48f, 0.22f, 1f), new Color(0.52f, 0.18f, 0.62f, 1f));
        EnsureNpcDetails(vendor.transform, new Color(0.52f, 0.18f, 0.62f, 1f), "MerchantCoat");
    }

    void EnsureStarterChest()
    {
        Transform existing = transform.Find("StarterRustyMetalChest");
        if (existing != null)
        {
            VillageChest existingChest = existing.GetComponent<VillageChest>();
            if (existingChest != null)
                existingChest.Configure(RustyMetalItemRegistry.GetOrCreate());

            return;
        }

        GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chest.name = "StarterRustyMetalChest";
        chest.transform.SetParent(transform, false);
        chest.transform.localPosition = chestLocalPosition;
        chest.transform.localRotation = Quaternion.Euler(facingEulerAngles);
        chest.transform.localScale = new Vector3(1.45f, 0.75f, 0.95f);

        Renderer bodyRenderer = chest.GetComponent<Renderer>();
        if (bodyRenderer != null)
            bodyRenderer.material.color = new Color(0.42f, 0.22f, 0.08f, 1f);

        VillageChest villageChest = chest.AddComponent<VillageChest>();
        villageChest.Configure(RustyMetalItemRegistry.GetOrCreate());

        CreateChestLid(chest.transform);
        CreateChestBand(chest.transform, new Vector3(0f, 0.08f, -0.53f));
        CreateChestBand(chest.transform, new Vector3(0f, 0.08f, 0.53f));
    }

    void CreateChestLid(Transform parent)
    {
        GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(parent, false);
        lid.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        lid.transform.localScale = new Vector3(1.08f, 0.18f, 1.08f);

        Renderer renderer = lid.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.5f, 0.28f, 0.1f, 1f);
    }

    void CreateChestBand(Transform parent, Vector3 localPosition)
    {
        GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        band.name = "MetalBand";
        band.transform.SetParent(parent, false);
        band.transform.localPosition = localPosition;
        band.transform.localScale = new Vector3(1.08f, 0.12f, 0.08f);

        Renderer renderer = band.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.17f, 0.14f, 0.11f, 1f);
    }

    void ColorCraftingBench(GameObject bench)
    {
        ApplyColor(bench, new Color(0.54f, 0.29f, 0.08f, 1f));

        if (bench == null)
            return;

        foreach (Transform child in bench.transform)
        {
            if (child.name == "Leg")
                ApplyColor(child.gameObject, new Color(0.23f, 0.11f, 0.035f, 1f));
        }
    }

    void EnsureBenchAccent(Transform bench)
    {
        if (bench == null || bench.Find("WorkbenchCloth") != null)
            return;

        CreateVisualCube(
            "WorkbenchCloth",
            bench,
            new Vector3(0f, 0.54f, 0f),
            new Vector3(0.95f, 0.035f, 0.72f),
            new Color(0.13f, 0.42f, 0.58f, 1f));

        CreateVisualCube(
            "WorkbenchMetalStrip",
            bench,
            new Vector3(0f, 0.58f, -0.45f),
            new Vector3(1.05f, 0.04f, 0.06f),
            new Color(0.8f, 0.66f, 0.35f, 1f));
    }

    void ColorNpc(Transform npc, Color bodyColor, Color detailColor)
    {
        if (npc == null)
            return;

        ApplyColor(npc.gameObject, bodyColor);
        foreach (Transform child in npc)
        {
            if (child.name.Contains("Apron") || child.name.Contains("Coat") || child.name.Contains("Hat"))
                ApplyColor(child.gameObject, detailColor);
            else if (child.name.Contains("Npc") || child.name.Contains("Merchant") || child.name.Contains("Arm") || child.name.Contains("Head") || child.name.Contains("Hat"))
                ApplyColor(child.gameObject, bodyColor);
        }
    }

    Vector3 ResolveNpcOpenPosition(Vector3 configuredPosition, Vector3 fallbackOpenPosition)
    {
        return configuredPosition.z > 68f ? fallbackOpenPosition : configuredPosition;
    }

    void PrepareNpcRoot(Transform npc, Vector3 localPosition)
    {
        if (npc == null)
            return;

        npc.localPosition = localPosition;
        npc.localRotation = Quaternion.Euler(facingEulerAngles);
        npc.localScale = Vector3.one;

        Renderer rootRenderer = npc.GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
    }

    void EnsureNpcVisibleBody(Transform npc, Color bodyColor, Color detailColor, bool merchant)
    {
        if (npc == null)
            return;

        EnsureNpcCollider(npc);

        CreateOrUpdateVisualPrimitive(
            "NpcBodyVisual",
            PrimitiveType.Capsule,
            npc,
            new Vector3(0f, 0.62f, 0f),
            new Vector3(0.82f, 0.82f, 0.82f),
            bodyColor);

        CreateOrUpdateVisualPrimitive(
            "NpcHeadVisual",
            PrimitiveType.Sphere,
            npc,
            new Vector3(0f, 1.62f, 0f),
            new Vector3(0.48f, 0.48f, 0.48f),
            bodyColor);

        CreateOrUpdateVisualPrimitive(
            "NpcLeftArmVisual",
            PrimitiveType.Cube,
            npc,
            new Vector3(-0.55f, 0.74f, -0.04f),
            new Vector3(0.16f, 0.7f, 0.16f),
            bodyColor);

        CreateOrUpdateVisualPrimitive(
            "NpcRightArmVisual",
            PrimitiveType.Cube,
            npc,
            new Vector3(0.55f, 0.74f, -0.04f),
            new Vector3(0.16f, 0.7f, 0.16f),
            bodyColor);

        if (merchant)
        {
            CreateOrUpdateVisualPrimitive(
                "MerchantHatVisual",
                PrimitiveType.Cylinder,
                npc,
                new Vector3(0f, 1.96f, 0f),
                new Vector3(0.55f, 0.16f, 0.55f),
                detailColor);
        }
    }

    void EnsureNpcCollider(Transform npc)
    {
        if (npc == null || npc.GetComponent<Collider>() != null)
            return;

        CapsuleCollider collider = npc.gameObject.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.radius = 0.52f;
        collider.height = 1.9f;
    }

    void EnsureNpcDetails(Transform npc, Color detailColor, string detailName)
    {
        if (npc == null || npc.Find(detailName) != null)
            return;

        CreateVisualCube(
            detailName,
            npc,
            new Vector3(0f, 0.05f, -0.44f),
            new Vector3(0.72f, 0.62f, 0.08f),
            detailColor);

        CreateVisualCube(
            detailName + "Belt",
            npc,
            new Vector3(0f, -0.28f, -0.46f),
            new Vector3(0.82f, 0.08f, 0.09f),
            new Color(0.12f, 0.075f, 0.035f, 1f));
    }

    GameObject CreateVisualCube(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = objectName;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = localScale;
        ApplyColor(visual, color);

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return visual;
    }

    GameObject CreateVisualPrimitive(string objectName, PrimitiveType primitive, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject visual = GameObject.CreatePrimitive(primitive);
        visual.name = objectName;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = localScale;
        ApplyColor(visual, color);

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return visual;
    }

    GameObject CreateOrUpdateVisualPrimitive(string objectName, PrimitiveType primitive, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        GameObject visual = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitive);
        visual.name = objectName;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = localScale;

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = true;

        ApplyColor(visual, color);

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return visual;
    }

    void ApplyColor(GameObject target, Color color)
    {
        Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
        if (renderer != null)
            renderer.sharedMaterial = RuntimeMaterialUtility.Create($"{target.name}RuntimeMaterial", color);
    }
}
