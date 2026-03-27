using System.Collections;
using UnityEngine;
using Unity.AI.Navigation;

public class NavMeshRuntime : MonoBehaviour
{
    public NavMeshSurface surface;

    void Start()
    {
        StartCoroutine(BuildNavMesh());
    }

    IEnumerator BuildNavMesh()
    {
        // espera o terreno nascer COMPLETO
        yield return new WaitForSeconds(0.5f);

        surface.RemoveData();
        surface.BuildNavMesh();

        Debug.Log("🔥 NavMesh GERADO em runtime!");
    }
}