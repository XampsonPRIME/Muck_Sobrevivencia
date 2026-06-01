using UnityEngine;

[DisallowMultipleComponent]
public class VillageCraftingSetup : MonoBehaviour
{
    [SerializeField] Item gravetoItem;
    [SerializeField] Vector3 benchLocalPosition = new Vector3(19f, 1.45f, 72f);
    [SerializeField] Vector3 npcLocalPosition = new Vector3(16.8f, 1.65f, 72f);
    [SerializeField] Vector3 chestLocalPosition = new Vector3(21.8f, 1.55f, 72f);
    [SerializeField] Vector3 facingEulerAngles = new Vector3(0f, 90f, 0f);
    [SerializeField] string npcProfessionName = "Profissão";
    [SerializeField] string npcProximityMessage = "Nessa bancada voce pode criar os itens que esse mundo tem a oferecer.";

    void Awake()
    {
        EnsureCraftingBench();
        EnsureCraftingNpc();
        EnsureStarterChest();
    }

    void EnsureCraftingBench()
    {
        Transform existing = transform.Find("CraftingBench");
        if (existing != null)
        {
            CraftingBench existingBench = existing.GetComponent<CraftingBench>();
            if (existingBench != null)
                existingBench.Configure(gravetoItem);

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

        CreateBenchLeg(bench.transform, new Vector3(-0.85f, -0.85f, -0.35f));
        CreateBenchLeg(bench.transform, new Vector3(0.85f, -0.85f, -0.35f));
        CreateBenchLeg(bench.transform, new Vector3(-0.85f, -0.85f, 0.35f));
        CreateBenchLeg(bench.transform, new Vector3(0.85f, -0.85f, 0.35f));
    }

    void CreateBenchLeg(Transform parent, Vector3 localPosition)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = "Leg";
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = localPosition;
        leg.transform.localScale = new Vector3(0.12f, 1.6f, 0.12f);
    }

    void EnsureCraftingNpc()
    {
        if (transform.Find("CraftingNpc") != null)
            return;

        GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "CraftingNpc";
        npc.transform.SetParent(transform, false);
        npc.transform.localPosition = npcLocalPosition;
        npc.transform.localRotation = Quaternion.Euler(facingEulerAngles);
        npc.transform.localScale = new Vector3(0.85f, 1f, 0.85f);

        CraftingNpc craftingNpc = npc.AddComponent<CraftingNpc>();
        craftingNpc.Configure(npcProfessionName, npcProximityMessage);
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
}
