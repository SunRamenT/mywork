using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMove : MonoBehaviour
{
    [Header("追跡設定")]
    [Tooltip("追いかける対象。空にすると自由探索モードになります。")]
    public Transform target;

    [Header("自由探索用の設定")]
    [Tooltip("徘徊モードの時に目的地を探す範囲")]
    public float patrolRadius = 20f;

    [Header("乗っ取り判定")]
    public bool isNottoried = false;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // ゲーム開始時にターゲットがいなければ、最初の徘徊を開始する
        if (target == null)
        {
            SetNewPatrolDestination();
        }
    }

    private void Update()
    {
        // 乗っ取られている間は何もしない
        if (isNottoried)
        {
            return;
        }

        // ▼▼▼ AIの行動ロジック ▼▼▼
        if (target != null)
        {
            // 【追跡モード】ターゲットが設定されていれば、追いかける
            agent.SetDestination(target.position);
        }
        else
        {
            // 【自由探索モード】ターゲットがいなければ、徘徊する
            // 目的地に到着したら、次の目的地を探す
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                SetNewPatrolDestination();
            }
        }
    }

    // 新しい徘徊の目的地を設定する関数
    void SetNewPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        
        NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, 1);
        
        agent.SetDestination(hit.position);
    }
}