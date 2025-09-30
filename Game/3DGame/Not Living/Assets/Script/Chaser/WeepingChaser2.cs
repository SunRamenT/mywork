using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class WeepingChaser2 : MonoBehaviour
{
    [Header("ターゲット設定")]
    [Tooltip("追いかける対象（プレイヤー）")]
    public Transform playerTarget;

    // --- 内部で使うコンポーネント ---
    private NavMeshAgent agent;
    private Renderer modelRenderer;
    private Camera mainCamera;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // 子オブジェクトも含めて、表示/非表示を判定するためのRendererを取得
        modelRenderer = GetComponentInChildren<Renderer>();
        mainCamera = Camera.main;
    }

    private void Start()
    {
        // ゲーム開始時にプレイヤーを自動で探す
        if (playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTarget = playerObject.transform;
            }
            else
            {
                Debug.LogError("Playerタグが付いたオブジェクトが見つかりません！");
                this.enabled = false; // ターゲットがいないなら動作を停止
            }
        }
    }

    private void Update()
    {
        if (playerTarget == null || modelRenderer == null) return;

        // --- 視認判定 ---
        // Rendererがカメラの視野内にあり、かつ壁などで遮られていないかをチェック
        if (IsVisibleByCamera())
        {
            // 見られている間は、移動を完全に停止する
            agent.isStopped = true;
        }
        else
        {
            // 見られていない間は、プレイヤーを追いかける
            agent.isStopped = false;
            agent.SetDestination(playerTarget.position);
        }
    }

    /// <summary>
    /// オブジェクトがカメラから見えているかを判定する
    /// </summary>
    private bool IsVisibleByCamera()
    {
        // 1. Unityの簡易判定：カメラの視野角（Frustum）の外にいるか？
        //    GeometryUtility.TestPlanesAABBを使うことで、Renderer.isVisibleよりも確実な判定が可能
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        if (!GeometryUtility.TestPlanesAABB(planes, modelRenderer.bounds))
        {
            return false; // 視野角の外なので「見えていない」
        }

        // 2. 詳細判定：壁などの障害物で隠れていないか？
        //    カメラからオブジェクトの中心へ向かって光線（Ray）を飛ばす
        Vector3 viewPoint = mainCamera.transform.position;
        Vector3 targetPoint = modelRenderer.bounds.center;
        
        // Raycastが何か（障害物）に当たった場合
        if (Physics.Raycast(viewPoint, (targetPoint - viewPoint).normalized, out RaycastHit hit, Vector3.Distance(viewPoint, targetPoint)))
        {
            // 当たったのが自分自身（または自分の子オブジェクト）でなければ、それは障害物
            if (hit.transform.root != this.transform.root)
            {
                return false; // 障害物があるので「見えていない」
            }
        }
        
        // 上記のチェックを全て通過した場合のみ「見えている」と判断
        return true;
    }
}