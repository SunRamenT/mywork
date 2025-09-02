using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMove : MonoBehaviour
{
    public Transform target;            // 通常追跡対象
    private NavMeshAgent agent;

    [Header("乗っ取り判定")]
    public bool isNottoried = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (isNottoried)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }
        else
        {
            if (target != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
        }
    }
}
