using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    public GameObject wolfPrefab;
    public Transform player;

    [Header("Spawn")]
    public int maxEnemies = 10;
    public float spawnRadius = 80f;
    public float spawnInterval = 3f;
    public BoxCollider spawnArea;

    private int currentEnemies = 0;

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            if (player == null) continue;

            PlayerHealth ph = player.GetComponent<PlayerHealth>();

            if (ph != null && ph.isDead)
            {
                yield break; // 🔥 PARA TUDO
            }
            if (currentEnemies < maxEnemies)
            {
                SpawnWolf();
            }
        }
    }

    void SpawnWolf()
    {
        if (player == null) return;

        // 🔥 pega posição aleatória dentro da área
        Vector3 randomPos = GetRandomPointInBounds(spawnArea.bounds);

        NavMeshHit hit;

        if (NavMesh.SamplePosition(randomPos, out hit, 30f, NavMesh.AllAreas))
        {
            GameObject wolf = Instantiate(wolfPrefab, hit.position, Quaternion.identity);

            WolfAI ai = wolf.GetComponent<WolfAI>();

            if (ai != null)
            {
                ai.player = player;
            }

            currentEnemies++;
        }
    }

    Vector3 GetRandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    public void EnemyDied()
    {
        currentEnemies--;
    }
}