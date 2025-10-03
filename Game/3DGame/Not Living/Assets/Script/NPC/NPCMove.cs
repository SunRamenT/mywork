using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.Animations;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCMove : MonoBehaviour
{
    [Header("追跡設定")]
    public Transform target;

    [Header("反撃設定")]
    public float retaliationDuration = 5f;
    public string attackTriggerID = "Attack";
    [Header("攻撃関連")]
    [Tooltip("攻撃を開始する距離")]
    public float attackRange = 2.0f;


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
                //isRetaliating = false;
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
    [Tooltip("逃走アニメーション用のboolパラメータ名")] // ▼▼▼ 追加 ▼▼▼
    public string fleeFloatID = "State";
    [Tooltip("逃走する距離")] // ▼▼▼ 追加 ▼▼▼
    public float fleeDistance = 15f;
    private float socialCheckTimer = 0f;
    private const float SOCIAL_CHECK_INTERVAL = 2f; // 2秒ごとに周囲を確認

    private NavMeshAgent agent;
    private Animator animator;
    //private bool isRetaliating = false;
    private StatusManager statusManager; // 自分自身のStatusManager

    private const float AnimationFlowSpeed = 4.5f;
    private Vector2 flowAxis;
    private float flowState;

    [Tooltip("目的地に到達できない場合に、諦めるまでの時間（秒）")] // ▼▼▼ 追加 ▼▼▼
    public float pathfindingTimeout = 10f;
    private float pathTimer = 0f; // ▼▼▼ 目的地到達タイマーを追加 ▼▼▼

    // ▼▼▼ AIの状態に「逃走中」を追加 ▼▼▼
    private enum AIState { Patrolling, Retaliating, Greeting, Fleeing }
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
            case AIState.Fleeing:
                break;
        }
        CalculateAndAnimate();
    }

    private void CheckForSocialInteractions()
    {
        // 自分の評判に応じて行動を決定
        if (statusManager.reputation < 40)
        {
            // --- 評判が低いNPCの行動 ---
            // 周囲のNPCを全て見つける
            Collider[] colliders = Physics.OverlapSphere(transform.position, socialCheckRadius);
            List<StatusManager> nearbyNpcs = new List<StatusManager>();
            foreach (var col in colliders)
            {
                if (col.transform == this.transform) continue; // 自分自身はスキップ

                if (col.TryGetComponent<StatusManager>(out StatusManager other))
                {
                    nearbyNpcs.Add(other);
                }
            }

            // 優先度1: 評判60以上の相手がいないか探す
            var scaryNpc = nearbyNpcs.FirstOrDefault(npc => npc.reputation >= 60);
            if (scaryNpc != null)
            {
                StartCoroutine(FleeingRoutine(scaryNpc.transform));
                return; // 逃げるのが最優先
            }

            // 優先度2: 評判40~59の相手がいないか探す
            var targetNpc = nearbyNpcs.FirstOrDefault(npc => npc.reputation >= 40 && npc.reputation < 60);
            if (targetNpc != null)
            {
                // 攻撃を開始する
                Debug.Log($"{gameObject.name}が{targetNpc.name}に喧嘩を売りに行きます。");
                StartRetaliation(targetNpc.gameObject);
                return;
            }
        }
        else if (statusManager.reputation >= 60)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, socialCheckRadius);
            List<StatusManager> nearbyNpcs = new List<StatusManager>();
            //Debug.Log(nearbyNpcs.Count);
            foreach (var col in colliders)
            {
                if (col.transform == this.transform) continue; // 自分自身はスキップ

                if (col.TryGetComponent<StatusManager>(out StatusManager other))
                {
                    nearbyNpcs.Add(other);
                }
            }
            // 優先度2: 評判30の相手がいないか探す
            var targetNpc = nearbyNpcs.FirstOrDefault(npc => npc.reputation < 35);
            if (targetNpc != null)
            {
                // 攻撃を開始する
                Debug.Log($"{gameObject.name}が{targetNpc.name}に喧嘩を売りに行きます。");
                StartRetaliation(targetNpc.gameObject);
                return;
            }
            // --- 評判が普通のNPCの行動 ---
            CheckForGreeting();
        }
        else if (statusManager.reputation >= 40)
        {
            //Debug.Log("評判が普通のNPCの行動をチェックします。");
            // --- 評判が普通のNPCの行動 ---
            CheckForGreeting();
        }
    }

    // ▼▼▼ 逃走を実行する新しいコルーチン ▼▼▼
    private IEnumerator FleeingRoutine(Transform scaryPerson)
    {
        currentState = AIState.Fleeing;
        agent.speed = 5.5f;

        Debug.Log($"{gameObject.name}が{scaryPerson.name}から逃げ始めました。");

        // NavMeshAgent が有効になるまで待つ（生成直後などで isOnNavMesh が false の可能性があるため）
        yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh);

        agent.isStopped = false;

        // 1. 相手から離れる方向を計算
        Vector3 fleeDirection = (transform.position - scaryPerson.position).normalized;

        // 2. 逃げる先の目標地点を計算
        Vector3 destination = transform.position + fleeDirection * fleeDistance;

        // 3. NavMesh上の有効な地点を探して、そこへ向かう
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            // 有効な地点が見つからなければ、とりあえずランダムな場所へ
            SetNewPatrolDestination();
        }

        // 目的地に到着するまで待機
        while (true)
        {
            // NavMesh から外れていたら安全のため抜ける
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"{gameObject.name} は NavMesh 上にいないため逃走を中断しました。");
                break;
            }

            // 経路計算中なら待つ
            if (agent.pathPending)
            {
                yield return null;
                continue;
            }

            // 残り距離が到達距離以下なら完了
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                break;
            }

            // 途中で攻撃されたら中断
            yield return null;
        }

        // 目的地に着いたら、状態を徘徊に戻す
        agent.speed = 1f;
        currentState = AIState.Patrolling;
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
                //isRetaliating = false;
                SetNewPatrolDestination();
            }
        }

        // 定期的に挨拶相手を探す
        socialCheckTimer += Time.deltaTime;
        if (socialCheckTimer > SOCIAL_CHECK_INTERVAL)
        {
            socialCheckTimer = 0f;
            CheckForSocialInteractions();
        }
    }

    // ▼▼▼ 挨拶相手を探す新しいメソッド ▼▼▼
    private void CheckForGreeting()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, socialCheckRadius);
        foreach (var col in colliders)
        {
            if (col.transform == this.transform) continue; // 自分自身はスキップ

            // 親を含めて StatusManager を探す
            StatusManager otherStatus = col.GetComponentInParent<StatusManager>();

            // null または自分自身のステータスならスキップ
            if (otherStatus == null || otherStatus == statusManager) continue;

            // 評判が高い NPC に挨拶
            if (otherStatus.reputation >= 60)
            {
                StartCoroutine(GreetingRoutine(otherStatus.transform));
                break;
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
        //Debug.Log($"{gameObject.name}が{personToGreet.name}に挨拶しました。");

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
        //isRetaliating = true;
        target = attackerTransform;
        float retaliationEndTime = Time.time + retaliationDuration;
        pathTimer = 0f; // 追跡開始時にタイマーをリセット
        agent.speed = 5.5f;

        // NavMeshAgent が有効になるまで待機（生成直後でも安全にする）
        yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh);

        while (Time.time < retaliationEndTime)
        {
            if (target == null) break;

            // ▼ ダメージモーション中は攻撃しない
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag(flinchingTagName))
            {
                yield return null;
                continue;
            }

            // 距離判定（Yを無視）
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance > attackRange)
            {
                // 射程外 → 追いかける
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(target.position);

                    // 回転も補正
                    Vector3 dir = toTarget.normalized;
                    if (dir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            Quaternion.LookRotation(dir),
                            Time.deltaTime * agent.angularSpeed
                        );
                    }
                }
            }
            else
            {
                // 射程内 → 移動停止して攻撃
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                animator.SetTrigger(attackTriggerID);

                // 相手を向く
                Vector3 dir = toTarget.normalized;
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * agent.angularSpeed
                    );
                }
            }

            // タイムアウト処理
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.hasPath)
                {
                    pathTimer += Time.deltaTime;
                    if (pathTimer > pathfindingTimeout)
                    {
                        // 追跡失敗
                        break;
                    }
                }
                else
                {
                    // パスがない場合はタイマーをリセット
                    pathTimer = 0f;
                }
            }

            yield return null;
        }

        // 終了処理
        target = null;
        //isRetaliating = false;
        agent.speed = 1f;
        currentState = AIState.Patrolling;

        // 徘徊先を再設定
        SetNewPatrolDestination();
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