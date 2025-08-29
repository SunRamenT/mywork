using UnityEngine;
using UnityEngine.AI;


public class NPCMove : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _navMeshAgent; //AIがつくエージェント

    [SerializeField] private Transform target;//追いかける対象

    // Start is called once before the first execution of Update after the MonoBehaviour is created


    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        _navMeshAgent.SetDestination(target.position);

    }
}
