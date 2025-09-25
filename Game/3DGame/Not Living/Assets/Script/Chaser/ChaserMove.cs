using UnityEngine;
using UnityEngine.AI;
using UniRx; // ▼▼▼ この一行を追加 ▼▼▼

[RequireComponent(typeof(NavMeshAgent))]
public class ChaserMove : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private ReikonManager reikonManager;

    [Header("コンポーネント参照")]
    public Light sightLight;

    [Header("Light Settings")]
    public Color patrolLightColor = Color.yellow;
    public Color chasingLightColor = Color.red;

    [Header("Agent Type Settings")]
    public int humanoidAgentTypeIndex = 0;
    public int chaserAgentTypeIndex = 1;
    private int humanoidAgentTypeID;
    private int chaserAgentTypeID;

    [Header("AI States")]
    public float humanoidSpeed = 3.5f;
    public float investigatingSpeed = 5.0f;
    public float chaserSpeed = 7.0f;
    public float chaserAcceleration = 16f;
    private float initialAcceleration;

    [Header("AI Settings")]
    public float hearingSensitivity = 1.0f;
    public float sightRadius = 15f;
    [Range(0, 90)]
    public float sightAngle = 60f;
    public float patrolRadius = 30f;
    public float losePlayerTime = 5.0f;
    [Tooltip("音の調査を諦めるまでの時間（秒）")] // ▼▼▼ 追加 ▼▼▼
    public float investigationTimeout = 8.0f;
    public float dangerAuraRadius = 10f;

    [Header("遮蔽物チェック用")]
    public LayerMask obstacleMask;

    private enum AIState { Patrolling, Chasing, Investigating }
    private AIState currentState;
    private float timeSinceLastSeenPlayer = 0f;
    private float investigationTimer = 0f;
    private bool isPlayerInAura = false;

    // MessageBrokerの購読を管理するための変数
    private CompositeDisposable disposables = new CompositeDisposable();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        humanoidAgentTypeID = NavMesh.GetSettingsByIndex(humanoidAgentTypeIndex).agentTypeID;
        chaserAgentTypeID = NavMesh.GetSettingsByIndex(chaserAgentTypeIndex).agentTypeID;

        initialAcceleration = agent.acceleration;
        agent.agentTypeID = humanoidAgentTypeID;
        agent.speed = humanoidSpeed;
        
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            reikonManager = playerObject.GetComponent<ReikonManager>();
        }
        else
        {
            Debug.LogError("Playerタグが付いたオブジェクトが見つかりません！");
            enabled = false;
            return;
        }
        
        currentState = AIState.Patrolling;
        SetNewPatrolDestination();
        UpdateSightLight();
        
        if (sightLight != null)
        {
            sightLight.color = patrolLightColor;
        }

        MessageBroker.Default
            .Receive<SoundPacket>()
            .Subscribe(packet => OnSoundHeard(packet))
            .AddTo(disposables);
    }


    private void OnDestroy()
    {
        // このオブジェクトが破棄される際に、監視を確実に終了させる
        disposables.Dispose();

        if (isPlayerInAura && reikonManager != null)
        {
            reikonManager.OnChaserExitAura();
        }
    }

    private void OnSoundHeard(SoundPacket packet)
    {
        if (packet.Type == SoundType.EnemyNoise) return;

        float distanceToSound = Vector3.Distance(transform.position, packet.Position);
        float audibleRange = packet.Volume * hearingSensitivity;
        if (distanceToSound > audibleRange) return;

        if (currentState != AIState.Chasing)
        {
            currentState = AIState.Investigating;
            agent.speed = investigatingSpeed;
            agent.SetDestination(packet.Position);
            investigationTimer = 0f; // ▼▼▼ 調査タイマーをリセット ▼▼▼
        }
    }


    void Update()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        switch (currentState)
        {
            case AIState.Patrolling:
                agent.speed = humanoidSpeed;
                LookForPlayer();
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    SetNewPatrolDestination();
                }
                break;

            case AIState.Chasing:
                agent.SetDestination(player.position);
                if (!IsPlayerInSight())
                {
                    timeSinceLastSeenPlayer += Time.deltaTime;
                    if (timeSinceLastSeenPlayer > losePlayerTime)
                    {
                        currentState = AIState.Patrolling;
                        agent.agentTypeID = humanoidAgentTypeID;
                        agent.speed = humanoidSpeed;
                        agent.acceleration = initialAcceleration;
                        SetNewPatrolDestination();
                        if (sightLight != null) sightLight.color = patrolLightColor;
                    }
                }
                else
                {
                    timeSinceLastSeenPlayer = 0f;
                }
                break;
                
            // ▼▼▼ 調査ステートのロジックを修正 ▼▼▼
            case AIState.Investigating:
                LookForPlayer(); // 調査中もプレイヤーを探し続ける
                investigationTimer += Time.deltaTime; // タイマーを進める

                // 目的地に到着した、または調査時間がタイムアウトした場合
                if ((!agent.pathPending && agent.remainingDistance < 0.5f) || investigationTimer > investigationTimeout)
                {
                    // 徘徊モードに戻る
                    currentState = AIState.Patrolling;
                }
                break;
        }
        CheckDangerAura();
    }
    
    void LookForPlayer()
    {
        if (IsPlayerInSight())
        {
            currentState = AIState.Chasing;
            agent.agentTypeID = chaserAgentTypeID;
            agent.speed = chaserSpeed;
            agent.acceleration = chaserAcceleration;
            timeSinceLastSeenPlayer = 0f;
            if (sightLight != null)
            {
                sightLight.color = chasingLightColor;
            }
        }
    }

    private void UpdateSightLight()
    {
        if (sightLight != null)
        {
            sightLight.range = sightRadius;
            sightLight.spotAngle = sightAngle * 2;
        }
    }

    private void CheckDangerAura()
    {
        if (player == null || reikonManager == null) return;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= dangerAuraRadius && !isPlayerInAura)
        {
            isPlayerInAura = true;
            reikonManager.OnChaserEnterAura();
        }
        else if (distanceToPlayer > dangerAuraRadius && isPlayerInAura)
        {
            isPlayerInAura = false;
            reikonManager.OnChaserExitAura();
        }
    }
    
    bool IsPlayerInSight()
    {
        if (player == null) return false;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer > sightRadius) return false;
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > sightAngle) return false;
        Vector3 rayStartPos = transform.position + Vector3.up * 1.5f;
        Vector3 playerTargetPos = player.position + Vector3.up * 1.5f;
        if (Physics.Raycast(rayStartPos, (playerTargetPos - rayStartPos).normalized, distanceToPlayer, obstacleMask)) return false;
        return true;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Gizmos.color = Color.red;
        Vector3 rightDir = Quaternion.Euler(0, sightAngle, 0) * transform.forward;
        Vector3 leftDir = Quaternion.Euler(0, -sightAngle, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, rightDir * sightRadius);
        Gizmos.DrawRay(transform.position, leftDir * sightRadius);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, dangerAuraRadius);

        if (Application.isEditor && !Application.isPlaying)
        {
            UpdateSightLight();
        }
    }
}