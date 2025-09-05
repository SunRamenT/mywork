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
    [Tooltip("Animatorの水平方向パラメータ名")]
    public string horizontalID = "Hor";
    [Tooltip("Animatorの垂直方向パラメータ名")]
    public string verticalID = "Vert";
    [Tooltip("Animatorの歩行/走行切り替えパラメータ名")]
    public string stateID = "State";

    [Header("乗っ取り判定")]
    public bool isNottoried = false;

    private NavMeshAgent agent;
    private Animator animator;

    // 滑らかなアニメーション用の変数
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
            SetNewPatrolDestination();
        }
    }

    private void Update()
    {
        // 乗っ取られている間は動きとアニメーションを止める
        if (isNottoried)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            // アニメーションも完全に停止させる
            UpdateAnimation(Vector2.zero, 0f);
            return;
        }
        else
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }

        // AIの行動ロジック
        if (target != null)
        {
            // 【追跡モード】ターゲットが設定されていれば、追いかける
            agent.SetDestination(target.position);
        }
        else
        {
            // 【自由探索モード】目的地に到着したら、次の目的地を探す
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                SetNewPatrolDestination();
            }
        }
        
        // AIの現在の速度と状態からアニメーションを更新
        CalculateAndAnimate();
    }
    
    /// <summary>
    /// AIの移動情報からアニメーションパラメータの目標値を計算し、更新処理を呼び出す
    /// </summary>
    private void CalculateAndAnimate()
    {
        // NavMeshAgentの進行方向（ワールド座標）を、NPCのローカル座標に変換
        Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
        
        // ローカルのX(横)とZ(前)の速度を、HorとVertの目標値とする
        // agent.speedで割ることで、値を-1～1の範囲に正規化
        Vector2 targetAxis = new Vector2(localVelocity.x / agent.speed, localVelocity.z / agent.speed);

        // ターゲットがいれば走り(1)、いなければ歩き(0)を目標値とする
        float targetState = (target != null) ? 1.0f : 0.0f;
        
        // 計算した目標値に向かって、現在の値を滑らかに変化させながらAnimatorにセットする
        UpdateAnimation(targetAxis, targetState);
    }
    
    /// <summary>
    /// アニメーションパラメータを滑らかに更新し、Animatorにセットする
    /// </summary>
    private void UpdateAnimation(Vector2 axis, float state)
    {
        float deltaTime = Time.deltaTime;

        // 現在のアニメーション値を目標値に近づける
        flowAxis = Vector2.MoveTowards(flowAxis, axis, AnimationFlowSpeed * deltaTime);
        flowState = Mathf.MoveTowards(flowState, state, AnimationFlowSpeed * deltaTime);
        
        // Animatorにパラメータをセット
        animator.SetFloat(horizontalID, flowAxis.x);
        animator.SetFloat(verticalID, flowAxis.y);
        animator.SetFloat(stateID, flowState);
    }

    /// <summary>
    /// 新しい徘徊の目的地を設定する
    /// </summary>
    void SetNewPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        
        NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, 1);
        
        // 有効な場所が見つかった場合のみ目的地を設定
        if(hit.position != Vector3.zero)
        {
            agent.SetDestination(hit.position);
        }
    }
}