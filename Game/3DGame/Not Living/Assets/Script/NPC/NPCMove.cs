using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.Animations;

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
    [Tooltip("ダメージモーションのステートに設定したタグ名")] // ▼▼▼ 追加 ▼▼▼
    public string flinchingTagName = "Flinching";

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

    [Header("挨拶AI設定")] // ▼▼▼ 追加 ▼▼▼
    [Tooltip("他のNPCを探す半径")]
    public float socialCheckRadius = 5f;
    [Tooltip("挨拶アニメーションのトリガー名")]
    public string greetingTriggerID = "Greet";
    [Tooltip("挨拶アニメーションのおおよその長さ（秒）")]
    public float greetingDuration = 2f;
    private float socialCheckTimer = 0f;
    private const float SOCIAL_CHECK_INTERVAL = 2f; // 2秒ごとに周囲を確認

    private NavMeshAgent agent;
    private Animator animator;
    private bool isRetaliating = false;
    private StatusManager statusManager; // 自分自身のStatusManager

    private const float AnimationFlowSpeed = 4.5f;
    private Vector2 flowAxis;
    private float flowState;

    [Tooltip("目的地に到達できない場合に、諦めるまでの時間（秒）")] // ▼▼▼ 追加 ▼▼▼
    public float pathfindingTimeout = 8f;
    private float pathTimer = 0f; // ▼▼▼ 目的地到達タイマーを追加 ▼▼▼

    // ▼▼▼ AIの状態を定義 ▼▼▼
    private enum AIState { Patrolling, Retaliating, Greeting }
    private AIState currentState;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        statusManager = GetComponent<StatusManager>(); // 自身のStatusManagerを取得
    }

    private void Start()
    {
        currentState = AIState.Patrolling; // 初期状態を「徘徊」に設定
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

        // 現在のアニメーションステートを確認
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        // もしFlinchingタグが付いたステートを再生中なら、AIの思考を停止する
        if (stateInfo.IsTag(flinchingTagName))
        {
            // 移動を停止し、このフレームの処理を中断
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // エージェントが無効、またはNavMesh上にいない場合は、AIのロジックを実行しない
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            // アニメーションだけは更新しておく
            CalculateAndAnimate();
            return;
        }

        // ▼▼▼ ステートマシンによる処理の分岐 ▼▼▼
        switch (currentState)
        {
            case AIState.Patrolling:
                HandlePatrolling();
                break;
            case AIState.Retaliating:
                // 反撃はコルーチンが管理するため、Updateでは何もしない
                break;
            case AIState.Greeting:
                // 挨拶中はコルーチンが管理するため、Updateでは何もしない
                break;
        }
        CalculateAndAnimate();
    }

    private void HandlePatrolling()
    {
        agent.isStopped = false;

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

        // タイムアウト処理
        if (agent.hasPath)
        {
            pathTimer += Time.deltaTime;
            if (pathTimer > pathfindingTimeout)
            {
                target = null;
                isRetaliating = false;
                SetNewPatrolDestination();
            }
        }

        // 定期的に挨拶相手を探す
        socialCheckTimer += Time.deltaTime;
        if (socialCheckTimer > SOCIAL_CHECK_INTERVAL)
        {
            socialCheckTimer = 0f;
            CheckForGreeting();
        }
    }

    // ▼▼▼ 挨拶相手を探す新しいメソッド ▼▼▼
    private void CheckForGreeting()
    {
        // 自分の評判が40未満なら挨拶しない
        if (statusManager.reputation < 40) return;

        // 周囲のコライダーを検出
        Collider[] colliders = Physics.OverlapSphere(transform.position, socialCheckRadius);
        foreach (var col in colliders)
        {
            // 相手が自分自身ではないことを確認
            if (col.transform.root == this.transform.root) continue;

            // 相手がStatusManagerを持っていて、かつ評判が60以上か確認
            if (col.TryGetComponent<StatusManager>(out StatusManager otherStatus) && otherStatus.reputation >= 60)
            {
                // 条件に合う相手が見つかったら、挨拶コルーチンを開始
                StartCoroutine(GreetingRoutine(otherStatus.transform));
                break; // 最初の1人を見つけたらループを抜ける
            }
        }
    }

    // ▼▼▼ 挨拶を実行する新しいコルーチン ▼▼▼
    private IEnumerator GreetingRoutine(Transform personToGreet)
    {
        // 1. 状態を「挨拶中」に切り替え
        currentState = AIState.Greeting;
        agent.isStopped = true;

        // 2. 相手の方を向く
        Vector3 direction = (personToGreet.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // 3. 挨拶アニメーションを再生
        animator.SetTrigger(greetingTriggerID);
        Debug.Log($"{gameObject.name}が{personToGreet.name}に挨拶しました。");

        // 4. アニメーションが終わるまで待機
        yield return new WaitForSeconds(greetingDuration);

        // 5. 状態を「徘徊」に戻し、新しい目的地を設定
        currentState = AIState.Patrolling;
        SetNewPatrolDestination();
    }

    
    public void StartRetaliation(GameObject attacker)
    {
        if (isNottoried || currentState == AIState.Retaliating) return;
        if (attacker.CompareTag("Player")) return;

        // 実行中の他のコルーチン（挨拶など）を全て停止
        StopAllCoroutines();
        StartCoroutine(RetaliationRoutine(attacker.transform));
    }

    private IEnumerator RetaliationRoutine(Transform attackerTransform)
    {
        currentState = AIState.Retaliating;
        isRetaliating = true;
        target = attackerTransform;
        float retaliationEndTime = Time.time + retaliationDuration;
        pathTimer = 0f; // 追跡開始時にタイマーをリセット
        agent.speed = 3f;

        while (Time.time < retaliationEndTime)
        {
            if (target == null) break;

            // ▼▼▼ ダメージモーション中は攻撃しないように、ここでもチェックを追加 ▼▼▼
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag(flinchingTagName))
            {
                // Flinching中は待機
                yield return null;
                continue; // ループの最初に戻る
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * agent.angularSpeed);
            }
            animator.SetTrigger(attackTriggerID);
            yield return null;
        }
        
        target = null;
        isRetaliating = false;
        agent.speed = 1f;
        currentState = AIState.Patrolling;
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
        pathTimer = 0f; // 追跡開始時にタイマーをリセット
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