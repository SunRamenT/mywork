using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ChaserMove : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private ReikonManager reikonManager;

    [Header("コンポーネント参照")]
    [Tooltip("視界を可視化するためのSpotlight")]
    public Light sightLight;

    [Header("Light Settings")]
    [Tooltip("通常（徘徊）時のライトの色")]
    public Color patrolLightColor = Color.yellow;
    [Tooltip("プレイヤー発見（追跡）時のライトの色")]
    public Color chasingLightColor = Color.red;

    [Header("Agent Type Settings")]
    public int humanoidAgentTypeIndex = 0;
    public int chaserAgentTypeIndex = 1;
    private int humanoidAgentTypeID;
    private int chaserAgentTypeID;

    [Header("AI States")]
    public float humanoidSpeed = 3.5f;
    public float chaserSpeed = 7.0f;
    public float chaserAcceleration = 16f;
    private float initialAcceleration;

    [Header("AI Settings")]
    public float sightRadius = 15f;
    [Range(0, 90)]
    public float sightAngle = 60f;
    public float patrolRadius = 30f;
    public float losePlayerTime = 5.0f;
    public float dangerAuraRadius = 10f;

    [Header("遮蔽物チェック用")]
    public LayerMask obstacleMask;

    private enum AIState { Patrolling, Chasing }
    private AIState currentState;
    private float timeSinceLastSeenPlayer = 0f;
    private bool isPlayerInAura = false;

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
    }

    void Update()
    {
        // ▼▼▼ この安全確認を追加 ▼▼▼
        // エージェントが無効、またはNavMesh上にいない場合は、AIのロジックを実行しない
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            return; // このフレームの処理を中断
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        switch (currentState)
        {
            case AIState.Patrolling:
                LookForPlayer();
                // pathPending: パス計算中かどうか。計算中にremainingDistanceにアクセスするとエラーになることがある
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

                        if (sightLight != null)
                        {
                            sightLight.color = patrolLightColor;
                        }
                    }
                }
                else
                {
                    timeSinceLastSeenPlayer = 0f;
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
    
    private void OnDestroy()
    {
        if (isPlayerInAura && reikonManager != null)
        {
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