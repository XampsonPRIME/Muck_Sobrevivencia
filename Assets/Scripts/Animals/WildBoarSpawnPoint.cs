using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildBoarSpawnPoint : MonoBehaviour
{
    class SpawnRandom
    {
        readonly System.Random random;

        public SpawnRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public float Value()
        {
            return (float)random.NextDouble();
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, Value());
        }

        public Vector2 InsideUnitCircle()
        {
            float angle = Value() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Value());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }

    public GameObject boarPrefab;
    public int boarsPerGroup = 1;
    public float spawnRadius = 4.5f;
    public float respawnDelay = 55f;
    public float boarPatrolRadius = 10f;
    public Item thickLeatherItemData;
    public Item sharpTuskItemData;
    public Item boarMeatItemData;
    public Item trophyItemData;

    readonly List<WildBoar> activeBoars = new List<WildBoar>();
    Coroutine respawnRoutine;

    void Start()
    {
        SpawnMissingBoars();
    }

    public void NotifyBoarDeath(WildBoar boar)
    {
        activeBoars.Remove(boar);

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnMissingBoars();
        respawnRoutine = null;
    }

    void SpawnMissingBoars()
    {
        CleanupDeadEntries();

        while (activeBoars.Count < Mathf.Max(1, boarsPerGroup))
        {
            int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
            int spawnIndex = activeBoars.Count;
            SpawnRandom rng = new SpawnRandom(BuildSpawnSeed(worldSeed, spawnIndex));
            Vector2 offset2D = rng.InsideUnitCircle() * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            GameObject boarObject = boarPrefab != null
                ? Instantiate(boarPrefab, spawnPos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), transform)
                : CreateRuntimeBoar(spawnPos, rng.Range(0f, 360f));

            WildBoar boar = boarObject.GetComponent<WildBoar>();
            if (boar == null)
                boar = boarObject.AddComponent<WildBoar>();

            boar.thickLeatherItemData = thickLeatherItemData != null ? thickLeatherItemData : ThickLeatherItemRegistry.GetOrCreate();
            boar.sharpTuskItemData = sharpTuskItemData != null ? sharpTuskItemData : SharpTuskItemRegistry.GetOrCreate();
            boar.boarMeatItemData = boarMeatItemData != null ? boarMeatItemData : BoarMeatItemRegistry.GetOrCreate();
            boar.trophyItemData = trophyItemData != null ? trophyItemData : BoarTrophyItemRegistry.GetOrCreate();
            boar.patrolRadius = boarPatrolRadius;
            boar.SetSpawnData(this, transform.position);
            LanNetworkEntity.Ensure(boar, BuildBoarEntityId(spawnIndex));

            activeBoars.Add(boar);
        }
    }

    GameObject CreateRuntimeBoar(Vector3 position, float yaw)
    {
        GameObject boarObject = new GameObject("Javali Selvagem");
        boarObject.transform.SetParent(transform, true);
        boarObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return boarObject;
    }

    int BuildSpawnSeed(int worldSeed, int spawnIndex)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + worldSeed;
            hash = (hash * 31) + Mathf.RoundToInt(transform.position.x * 100f);
            hash = (hash * 31) + Mathf.RoundToInt(transform.position.z * 100f);
            hash = (hash * 31) + spawnIndex;
            return hash;
        }
    }

    string BuildBoarEntityId(int spawnIndex)
    {
        return $"WildBoarSpawn|{Mathf.RoundToInt(transform.position.x * 100f)}|{Mathf.RoundToInt(transform.position.z * 100f)}|{spawnIndex}";
    }

    void CleanupDeadEntries()
    {
        activeBoars.RemoveAll(boar => boar == null);
    }
}
