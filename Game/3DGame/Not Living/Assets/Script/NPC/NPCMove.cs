using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCMove : MonoBehaviour
{
    [Header("追跡設定")]
    [Tooltip("追いかける対象。空にすると自由探索モードになります。")]
    public Transform target;

    [Header("自由探索用の設定")]
    [Tooltip("徘徊モードの時に目的地を探す範囲")]
    public float patrolRadius = 20f;
    
    [Header("Animator パラメータ名")]
    public string horizontalID = "Hor";
    public string verticalID = "Vert";
    public string stateID = "State";

    [Header("乗っ取り判定")]
    public bool isNottoried = false;

    private NavMeshAgent agent;
    private Animator animator;

    private const float AnimationFlowSpeed = 4.5f;
    private Vector2 flowAxis;
    private float flowState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // ゲーム開始時にターゲットがいなければ、最初の徘徊を開始する
        if (target == null)
        {
            // AgentをまずNavMesh上の有効な位置に配置（ワープ）させる
            if (agent.Warp(transform.position))
            {
                // 配置に成功したら、最初の目的地を設定する
                SetNewPatrolDestination();
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} をNavMesh上に配置できませんでした。開始位置を確認してください。", this);
            }
        }
    }

    private void Update()
    {
        // 乗っ取られている間は、このスクリプトは何もしない
        if (isNottoried)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            return;
        }

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        if (target != null)
        {
            agent.SetDestination(target.position);
        }
        else
        {
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                SetNewPatrolDestination();
            }
        }
        
        CalculateAndAnimate();
    }
    
    private void CalculateAndAnimate()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
        Vector2 targetAxis = new Vector2(localVelocity.x / agent.speed, localVelocity.z / agent.speed);
        float targetState = (target != null) ? 1.0f : 0.0f;
        UpdateAnimation(targetAxis, targetState);
    }
    
    private void UpdateAnimation(Vector2 axis, float state)
    {
        float deltaTime = Time.deltaTime;
        flowAxis = Vector2.MoveTowards(flowAxis, axis, AnimationFlowSpeed * deltaTime);
        flowState = Mathf.MoveTowards(flowState, state, AnimationFlowSpeed * deltaTime);
        
        animator.SetFloat(horizontalID, flowAxis.x);
        animator.SetFloat(verticalID, flowAxis.y);
        animator.SetFloat(stateID, flowState);
    }

    void SetNewPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        
        NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, 1);
        
        // 有効な場所が見つかり、かつAgentがNavMesh上にいる場合のみ目的地を設定
        if(hit.position != Vector3.zero && agent.isOnNavMesh)
        {
            agent.SetDestination(hit.position);
        }
    }
}