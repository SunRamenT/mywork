using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCMove : MonoBehaviour
{
    [Header("追跡設定")]
    [Tooltip("追いかける対象。反撃時にも使用されます。")]
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

    private void Start()
    {
        if (target == null)
        {
            if (agent.Warp(transform.position))
            {
                SetNewPatrolDestination();
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} をNavMesh上に配置できませんでした。", this);
            }
        }
    }
    
    private void Update()
    {
        // 乗っ取られている場合は、全ての動作を停止
        if (isNottoried)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            return;
        }

        // 通常時、反撃中ともにエージェントは常に動ける状態にする
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        // ターゲットがいれば、そこへ向かう（反撃中もこの処理が使われる）
        if (target != null)
        {
            agent.SetDestination(target.position);
        }
        else
        {
            // ターゲットがおらず、反撃中でもなく、目的地に到着していたら、次の徘徊場所を探す
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

    /// <summary>
    /// 敵を追いかけながら、一定時間攻撃を繰り返すコルーチン
    /// </summary>
    private IEnumerator RetaliationRoutine(Transform attackerTransform)
    {
        isRetaliating = true;
        
        // ★★★ ターゲットを設定して、Updateループに追跡させる ★★★
        target = attackerTransform;

        float retaliationEndTime = Time.time + retaliationDuration;

        // 設定された時間、攻撃アニメーションを繰り返し再生
        while (Time.time < retaliationEndTime)
        {
            // ターゲットが途中でいなくなったら反撃を終了
            if (target == null) break;

            // ★★★ 移動しながらでも、常に相手の方向を向く ★★★
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;
            if(direction != Vector3.zero)
            {
                // NavMeshAgentの回転とケンカしないように、Slerpで滑らかに回転させる
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * agent.angularSpeed);
            }

            // ★★★ 距離に関係なく攻撃トリガーをセット ★★★
            animator.SetTrigger(attackTriggerID);
            
            // 次のフレームまで待機
            yield return null;
        }

        // --- 後片付け ---
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