using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCMove : MonoBehaviour
{
    [Header("追跡設定")]
    [Tooltip("追いかける対象。反撃時は使用しません。")]
    public Transform target;

    [Header("反撃設定")]
    [Tooltip("攻撃された後、敵を追いかけながら攻撃し続ける全体の時間（秒）")]
    public float retaliationDuration = 5f;
    [Tooltip("攻撃アニメーションを再生するトリガー名")]
    public string attackTriggerID = "Attack";

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
    private bool isRetaliating = false;

    private const float AnimationFlowSpeed = 4.5f;
    private Vector2 flowAxis;
    private float flowState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    // ▼▼▼ このStartメソッドを修正しました ▼▼▼
    private void Start()
    {
        // ターゲットが設定されていない場合（つまり、最初から自由探索モードの場合）
        if (target == null)
        {
            // NPCGeneratorが既にNavMesh上の有効な位置に配置してくれているため、
            // ここでWarpによるチェックは行わず、すぐに最初の目的地を設定する。
            SetNewPatrolDestination();
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    
    private void Update()
    {
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
            if (!isRetaliating && !agent.pathPending && agent.remainingDistance < 0.5f)
            {
                SetNewPatrolDestination();
            }
        }
        
        CalculateAndAnimate();
    }
    
    public void StartRetaliation(GameObject attacker)
    {
        if (isNottoried || isRetaliating) return;
        if (attacker.CompareTag("Player")) return;

        StartCoroutine(RetaliationRoutine(attacker.transform));
    }

    private IEnumerator RetaliationRoutine(Transform attackerTransform)
    {
        isRetaliating = true;
        target = attackerTransform;

        float retaliationEndTime = Time.time + retaliationDuration;

        while (Time.time < retaliationEndTime)
        {
            if (target == null) break;

            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;
            if(direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * agent.angularSpeed);
            }

            animator.SetTrigger(attackTriggerID);
            
            yield return null;
        }

        target = null;
        isRetaliating = false;
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
        
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, 1))
        {
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(hit.position);
            }
        }
    }
}