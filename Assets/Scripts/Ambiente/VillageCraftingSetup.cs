using UnityEngine;

[DisallowMultipleComponent]
public class VillageCraftingSetup : MonoBehaviour
{
    [SerializeField] Item gravetoItem;
    [SerializeField] Vector3 benchLocalPosition = new Vector3(19f, 1.45f, 72f);
    [SerializeField] Vector3 npcLocalPosition = new Vector3(16.8f, 1.65f, 72f);
    [SerializeField] Vector3 facingEulerAngles = new Vector3(0f, 90f, 0f);
    [SerializeField] string npcProfessionName = "Profissão";
    [SerializeField] string npcProximityMessage = "Nessa bancada voce pode criar os itens que esse mundo tem a oferecer.";

    void Awake()
    {
        EnsureCraftingBench();
        EnsureCraftingNpc();
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
}
