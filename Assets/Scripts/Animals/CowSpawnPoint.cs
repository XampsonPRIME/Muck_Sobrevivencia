using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CowSpawnPoint : MonoBehaviour
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

    public GameObject cowPrefab;
    public int cowsPerGroup = 2;
    public float spawnRadius = 5f;
    public float respawnDelay = 25f;
    public float cowWanderRadius = 8f;
    public Item meatItemData;
    public GameObject meatDropPrefab;
    public Material bodyMaterial;
    public Material spotMaterial;
    public Material hoofMaterial;

    readonly List<Cow> activeCows = new List<Cow>();
    Coroutine respawnRoutine;

    void Start()
    {
        SpawnMissingCows();
    }

    public void NotifyCowDeath(Cow cow)
    {
        activeCows.Remove(cow);

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnMissingCows();
        respawnRoutine = null;
    }

    void SpawnMissingCows()
    {
        CleanupDeadEntries();

        while (activeCows.Count < cowsPerGroup)
        {
            int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
            int spawnIndex = activeCows.Count;
            int seed = BuildSpawnSeed(worldSeed, spawnIndex);
            SpawnRandom rng = new SpawnRandom(seed);
            Vector2 offset2D = rng.InsideUnitCircle() * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            GameObject cowObject = Instantiate(cowPrefab, spawnPos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), transform);
            Cow cow = cowObject.GetComponent<Cow>();

            if (cow == null)
                cow = cowObject.AddComponent<Cow>();

            cow.meatItemData = meatItemData != null ? meatItemData : cow.meatItemData;
            cow.meatDropPrefab = meatDropPrefab != null ? meatDropPrefab : cow.meatDropPrefab;
            cow.bodyMaterial = bodyMaterial != null ? bodyMaterial : cow.bodyMaterial;
            cow.spotMaterial = spotMaterial != null ? spotMaterial : cow.spotMaterial;
            cow.hoofMaterial = hoofMaterial != null ? hoofMaterial : cow.hoofMaterial;
            cow.wanderRadius = cowWanderRadius;
            cow.SetSpawnData(this, transform.position);
            LanNetworkEntity.Ensure(cow, BuildCowEntityId(spawnIndex));

            activeCows.Add(cow);
        }
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

    string BuildCowEntityId(int spawnIndex)
    {
        return $"CowSpawn|{Mathf.RoundToInt(transform.position.x * 100f)}|{Mathf.RoundToInt(transform.position.z * 100f)}|{spawnIndex}";
    }

    void CleanupDeadEntries()
    {
        activeCows.RemoveAll(cow => cow == null);
    }
}
