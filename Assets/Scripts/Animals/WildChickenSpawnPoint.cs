using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildChickenSpawnPoint : MonoBehaviour
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

    public GameObject chickenPrefab;
    public int chickensPerGroup = 3;
    public float spawnRadius = 4.5f;
    public float respawnDelay = 35f;
    public float chickenWanderRadius = 7f;
    public Item featherItemData;
    public Item rawMeatItemData;

    readonly List<WildChicken> activeChickens = new List<WildChicken>();
    Coroutine respawnRoutine;

    void Start()
    {
        SpawnMissingChickens();
    }

    public void NotifyChickenDeath(WildChicken chicken)
    {
        activeChickens.Remove(chicken);

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnMissingChickens();
        respawnRoutine = null;
    }

    void SpawnMissingChickens()
    {
        CleanupDeadEntries();

        while (activeChickens.Count < chickensPerGroup)
        {
            int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
            int spawnIndex = activeChickens.Count;
            SpawnRandom rng = new SpawnRandom(BuildSpawnSeed(worldSeed, spawnIndex));
            Vector2 offset2D = rng.InsideUnitCircle() * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            GameObject chickenObject = chickenPrefab != null
                ? Instantiate(chickenPrefab, spawnPos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), transform)
                : CreateRuntimeChicken(spawnPos, rng.Range(0f, 360f));

            WildChicken chicken = chickenObject.GetComponent<WildChicken>();
            if (chicken == null)
                chicken = chickenObject.AddComponent<WildChicken>();

            chicken.featherItemData = featherItemData != null ? featherItemData : FeatherItemRegistry.GetOrCreate();
            chicken.rawMeatItemData = rawMeatItemData != null ? rawMeatItemData : RawChickenMeatItemRegistry.GetOrCreate();
            chicken.wanderRadius = chickenWanderRadius;
            chicken.SetSpawnData(this, transform.position);
            LanNetworkEntity.Ensure(chicken, BuildChickenEntityId(spawnIndex));

            activeChickens.Add(chicken);
        }
    }

    GameObject CreateRuntimeChicken(Vector3 position, float yaw)
    {
        GameObject chickenObject = new GameObject("Galinha Selvagem");
        chickenObject.transform.SetParent(transform, true);
        chickenObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return chickenObject;
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

    string BuildChickenEntityId(int spawnIndex)
    {
        return $"WildChickenSpawn|{Mathf.RoundToInt(transform.position.x * 100f)}|{Mathf.RoundToInt(transform.position.z * 100f)}|{spawnIndex}";
    }

    void CleanupDeadEntries()
    {
        activeChickens.RemoveAll(chicken => chicken == null);
    }
}
