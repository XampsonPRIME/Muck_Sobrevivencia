using System.Collections.Generic;
using UnityEngine;

public class VillageSpawner : MonoBehaviour
{
    readonly Dictionary<string, GameObject> spawnedVillages = new Dictionary<string, GameObject>();
    int lastWorldSeed = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<VillageSpawner>() != null)
            return;

        GameObject spawnerObject = new GameObject("VillageSpawner");
        spawnerObject.AddComponent<VillageSpawner>();
    }

    void Update()
    {
        if (GameState.IsInLobby)
            return;

        if (LanMultiplayerManager.Instance != null &&
            LanMultiplayerManager.Instance.Mode == LanMultiplayerManager.SessionMode.Client &&
            !LanMultiplayerManager.Instance.IsSessionReady)
            return;

        int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
        if (worldSeed != lastWorldSeed)
        {
            ClearSpawnedVillages();
            lastWorldSeed = worldSeed;
        }

        IReadOnlyList<VillageSystem.VillageSite> sites = VillageSystem.GetSites(worldSeed);

        for (int i = 0; i < sites.Count; i++)
        {
            VillageSystem.VillageSite site = sites[i];
            if (site == null || !site.hasFlattenHeight || spawnedVillages.ContainsKey(site.siteId))
                continue;

            GameObject villageRoot = site.definition != null
                ? Instantiate(site.definition.gameObject)
                : CreateFallbackVillage(site);

            villageRoot.name = $"Village_{site.siteId}";
            villageRoot.transform.SetParent(transform, true);
            villageRoot.transform.position = new Vector3(site.centerXZ.x, site.flattenHeight, site.centerXZ.y);
            villageRoot.transform.rotation = Quaternion.Euler(0f, site.rotationY, 0f);

            spawnedVillages.Add(site.siteId, villageRoot);
        }
    }

    GameObject CreateFallbackVillage(VillageSystem.VillageSite site)
    {
        GameObject root = new GameObject("FallbackVillage");

        CreatePlaza(root.transform);
        CreateHouse(root.transform, new Vector3(-8f, 1.75f, -5f), new Vector3(6f, 3.5f, 5f));
        CreateHouse(root.transform, new Vector3(9f, 1.75f, -4f), new Vector3(5f, 3.5f, 5f));
        CreateHouse(root.transform, new Vector3(-7f, 1.75f, 8f), new Vector3(5f, 3.5f, 4.5f));
        CreateHouse(root.transform, new Vector3(8f, 1.75f, 9f), new Vector3(6f, 3.5f, 4.5f));
        CreateVendorNpc(root.transform);

        SphereCollider bounds = root.AddComponent<SphereCollider>();
        bounds.radius = site.reservationRadius;
        bounds.center = Vector3.zero;
        bounds.isTrigger = true;

        return root;
    }

    static void CreatePlaza(Transform parent)
    {
        GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.name = "Plaza";
        plaza.transform.SetParent(parent, false);
        plaza.transform.localScale = new Vector3(10f, 0.1f, 10f);
        plaza.transform.localPosition = new Vector3(0f, 0.05f, 0f);
    }

    static void CreateHouse(Transform parent, Vector3 localPosition, Vector3 scale)
    {
        GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
        house.name = "House";
        house.transform.SetParent(parent, false);
        house.transform.localPosition = localPosition;
        house.transform.localScale = scale;

        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(house.transform, false);
        roof.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        roof.transform.localScale = new Vector3(1.15f, 0.18f, 1.15f);
        roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
    }

    static void CreateVendorNpc(Transform parent)
    {
        GameObject vendor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        vendor.name = "VendorNPC";
        vendor.transform.SetParent(parent, false);
        vendor.transform.localPosition = new Vector3(0f, 1f, 2.5f);

        VendorShop vendorShop = vendor.AddComponent<VendorShop>();
        vendorShop.vendorName = "Mercador da Vila";
        vendorShop.loadOffersFromResources = true;
        vendorShop.resourcesFolder = "VendorItems/Village";
        vendorShop.defaultStock = 3;
    }

    void ClearSpawnedVillages()
    {
        foreach (KeyValuePair<string, GameObject> entry in spawnedVillages)
        {
            if (entry.Value != null)
                Destroy(entry.Value);
        }

        spawnedVillages.Clear();
    }
}
