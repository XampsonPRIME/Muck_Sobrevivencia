using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [HideInInspector] public Transform player;

    protected NavMeshAgent agent;

    public virtual void Init(Transform target)
    {
        player = target;
        agent = GetComponent<NavMeshAgent>();
    }
}