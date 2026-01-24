using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("移動する地点のリスト（ローカル座標推奨）")]
    public Vector3[] waypoints;
    [Tooltip("移動速度")]
    public float speed = 3f;
    [Tooltip("待機時間")]
    public float waitTime = 1f;
    
    [Header("挙動設定")]
    [Tooltip("ループするか（falseなら往復）")]
    public bool loop = true;

    private int currentWaypointIndex = 0;
    private bool movingForward = true;
    private float waitTimer = 0f;
    
    // 元の位置（エディタでの配置位置を基準にするため）
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
        
        // ウェイポイントが設定されていない場合は動かない
        if (waypoints == null || waypoints.Length == 0)
        {
            waypoints = new Vector3[] { Vector3.zero };
        }
    }

    void FixedUpdate()
    {
        if (waitTimer > 0)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        MovePlatform();
    }

    private void MovePlatform()
    {
        // 目標地点（グローバル座標に変換）
        // ※ waypointsをローカル座標として扱い、初期位置(startPosition)を加算する簡易実装
        // より厳密にするなら、親オブジェクトからの相対座標などで管理する
        Vector3 targetPos = startPosition + waypoints[currentWaypointIndex];
        
        // 移動
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.fixedDeltaTime);

        // 到着判定
        if (Vector3.Distance(transform.position, targetPos) < 0.01f)
        {
            waitTimer = waitTime;
            UpdateNextWaypoint();
        }
    }

    private void UpdateNextWaypoint()
    {
        if (loop)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            if (movingForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = waypoints.Length - 2;
                    movingForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex < 0)
                {
                    currentWaypointIndex = 1;
                    movingForward = true;
                }
            }
        }
    }

    // エディタでウェイポイントを見やすくする
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        Vector3 basePos = Application.isPlaying ? startPosition : transform.position;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 globalPos = basePos + waypoints[i];
            Gizmos.DrawSphere(globalPos, 0.3f);

            if (i < waypoints.Length - 1)
            {
                Vector3 nextPos = basePos + waypoints[i + 1];
                Gizmos.DrawLine(globalPos, nextPos);
            }
            else if (loop && waypoints.Length > 1)
            {
                Gizmos.DrawLine(globalPos, basePos + waypoints[0]);
            }
        }
    }
}