using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class ChaserMove : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;

    [Header("Agent Type Settings")]
    [Tooltip("徘徊時に使用するAgentTypeのインデックス番号 (通常は0)")]
    public int humanoidAgentTypeIndex = 0;
    [Tooltip("追跡時に使用するAgentTypeのインデックス番号 (通常は1)")]
    public int chaserAgentTypeIndex = 1;
    private int humanoidAgentTypeID;
    private int chaserAgentTypeID;

    [Header("AI States")]
    [Tooltip("徘徊時の移動速度")]
    public float humanoidSpeed = 3.5f;
    [Tooltip("追跡時の移動速度")]
    public float chaserSpeed = 7.0f;
    [Tooltip("追跡時の加速度")]
    public float chaserAcceleration = 16f;
    private float initialAcceleration;

    [Header("AI Settings")]
    public float sightRadius = 15f;
    [Range(0, 180)]
    public float sightAngle = 60f;
    public float patrolRadius = 30f;
    public float losePlayerTime = 5.0f;

    [Header("遮蔽物チェック用")]
    public LayerMask obstacleMask;

    private enum AIState { Patrolling, Chasing }
    private AIState currentState;
    private float timeSinceLastSeenPlayer = 0f;

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
        }
        else
        {
            Debug.LogError("Playerタグが付いたオブジェクトが見つかりません！");
            enabled = false;
            return;
        }

        if (TryGetComponent<SphereCollider>(out var sphereCollider))
        {
            sphereCollider.enabled = false;
        }

        currentState = AIState.Patrolling;
        SetNewPatrolDestination();
    }

    void Update()
    {
        switch (currentState)
        {
            case AIState.Patrolling:
                LookForPlayer();
                if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    SetNewPatrolDestination();
                }
                break;

            case AIState.Chasing:
                if (agent.isOnNavMesh) agent.SetDestination(player.position);
                
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
                    }
                }
                else
                {
                    timeSinceLastSeenPlayer = 0f;
                }
                break;
        }
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
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, 1))
        {
            // ▼▼▼ ここに安全確認を追加 ▼▼▼
            // エージェントがNavMesh上に存在する場合にのみ、目的地を設定する
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(hit.position);
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.transform == player)
        {
            Debug.Log("プレイヤーが捕まった！ゲームオーバー！");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
    }
}