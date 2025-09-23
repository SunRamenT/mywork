using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCMove : MonoBehaviour
{
    [Header("追跡設定")]
    public Transform target;

    [Header("反撃設定")]
    public float retaliationDuration = 5f;
    public string attackTriggerID = "Attack";

    [Header("自由探索用の設定")]
    public float patrolRadius = 20f;
    
    [Header("Animator パラメータ名")]
    public string horizontalID = "Hor";
    public string verticalID = "Vert";
    public string stateID = "State";

    [Header("乗っ取り判定")]
    private bool _isNottoried = false;
    public bool isNottoried
    {
        get { return _isNottoried; }
        set
        {
            _isNottoried = value;
            if (_isNottoried)
            {
                StopAllCoroutines();
                target = null;
                isRetaliating = false;
            }
        }
    }

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

    private void Start()
    {
        if (target == null)
        {
            SetNewPatrolDestination();
        }
    }
    
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
        
        // ▼▼▼ この安全確認を強化 ▼▼▼
        // エージェントが無効、またはNavMesh上にいない場合は、AIのロジックを実行しない
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            // アニメーションだけは更新しておく
            CalculateAndAnimate();
            return;
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        agent.isStopped = false;

        if (target != null)
        {
            agent.SetDestination(target.position);
        }
        else
        {
            // パス計算中か、目的地に到着した場合
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
        // エージェントが無効な場合は、速度を0としてアニメーションを更新
        Vector3 velocity = (agent.enabled && agent.isOnNavMesh) ? agent.velocity : Vector3.zero;
        
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
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
        if (!agent.isOnNavMesh) return;
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