using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthGolemSpawnPoint : MonoBehaviour
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

    public GameObject golemPrefab;
    public int golemsPerGroup = 1;
    public float spawnRadius = 3f;
    public float respawnDelay = 180f;
    public float golemPatrolRadius = 8f;
    public Item stoneFragmentItemData;
    public Item resilientMossItemData;
    public Item ironOreItemData;
    public Item earthCoreItemData;

    readonly List<EarthGolem> activeGolems = new List<EarthGolem>();
    Coroutine respawnRoutine;

    void Start()
    {
        SpawnMissingGolems();
    }

    public void NotifyGolemDeath(EarthGolem golem)
    {
        activeGolems.Remove(golem);

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnMissingGolems();
        respawnRoutine = null;
    }

    void SpawnMissingGolems()
    {
        CleanupDeadEntries();

        while (activeGolems.Count < Mathf.Max(1, golemsPerGroup))
        {
            int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
            int spawnIndex = activeGolems.Count;
            SpawnRandom rng = new SpawnRandom(BuildSpawnSeed(worldSeed, spawnIndex));
            Vector2 offset2D = rng.InsideUnitCircle() * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            GameObject golemObject = golemPrefab != null
                ? Instantiate(golemPrefab, spawnPos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), transform)
                : CreateRuntimeGolem(spawnPos, rng.Range(0f, 360f));

            EarthGolem golem = golemObject.GetComponent<EarthGolem>();
            if (golem == null)
                golem = golemObject.AddComponent<EarthGolem>();

            golem.stoneFragmentItemData = stoneFragmentItemData != null ? stoneFragmentItemData : StoneFragmentItemRegistry.GetOrCreate();
            golem.resilientMossItemData = resilientMossItemData != null ? resilientMossItemData : ResilientMossItemRegistry.GetOrCreate();
            golem.ironOreItemData = ironOreItemData != null ? ironOreItemData : IronItemRegistry.GetOrCreate();
            golem.earthCoreItemData = earthCoreItemData != null ? earthCoreItemData : EarthCoreItemRegistry.GetOrCreate();
            golem.patrolRadius = golemPatrolRadius;
            golem.SetSpawnData(this, transform.position);
            LanNetworkEntity.Ensure(golem, BuildGolemEntityId(spawnIndex));

            activeGolems.Add(golem);
        }
    }

    GameObject CreateRuntimeGolem(Vector3 position, float yaw)
    {
        GameObject golemObject = new GameObject("Golem de Terra");
        golemObject.transform.SetParent(transform, true);
        golemObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return golemObject;
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

    string BuildGolemEntityId(int spawnIndex)
    {
        return $"EarthGolemSpawn|{Mathf.RoundToInt(transform.position.x * 100f)}|{Mathf.RoundToInt(transform.position.z * 100f)}|{spawnIndex}";
    }

    void CleanupDeadEntries()
    {
        activeGolems.RemoveAll(golem => golem == null);
    }
}
