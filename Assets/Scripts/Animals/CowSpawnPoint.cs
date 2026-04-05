using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CowSpawnPoint : MonoBehaviour
{
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
            Vector2 offset2D = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            GameObject cowObject = Instantiate(cowPrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
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

            activeCows.Add(cow);
        }
    }

    void CleanupDeadEntries()
    {
        activeCows.RemoveAll(cow => cow == null);
    }
}
