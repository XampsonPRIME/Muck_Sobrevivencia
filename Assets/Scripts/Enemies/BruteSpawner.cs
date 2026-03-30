using UnityEngine;
using UnityEngine.AI;

public class BruteSpawner : MonoBehaviour
{
    public GameObject brutePrefab;
    public Transform spawnPoint;
    public Transform player;

    void Start()
    {
        StartCoroutine(SpawnAfterNavMesh());
    }

    System.Collections.IEnumerator SpawnAfterNavMesh()
    {
        NavMeshHit hit;

        // 🔥 espera NavMesh existir
        while (!NavMesh.SamplePosition(spawnPoint.position, out hit, 50f, NavMesh.AllAreas))
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 🔥 spawn garantido no chão
        GameObject brute = Instantiate(brutePrefab, hit.position + Vector3.up * 1f, Quaternion.identity);

        BruteAI ai = brute.GetComponent<BruteAI>();

        if (ai != null)
        {
            ai.player = player;
        }

        Debug.Log("🧟 Brute spawnado em posição fixa!");
    }

    void Awake()
{
    if (player == null)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");

        if (p != null)
        {
            player = p.transform;
            Debug.Log("🎯 Player encontrado automaticamente");
        }
        else
        {
            Debug.LogError("❌ Player NÃO encontrado!");
        }
    }
}
}