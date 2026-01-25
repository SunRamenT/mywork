using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

public class InfiniteNavMeshBuilder : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("追跡する対象（プレイヤー）")]
    public Transform trackedTarget;
    
    [Tooltip("NavMeshを生成する範囲（X, Y, Z）")]
    public Vector3 size = new Vector3(80.0f, 30.0f, 80.0f);

    [Tooltip("NavMeshの対象にするレイヤー")]
    public LayerMask layerMask;

    [Tooltip("更新を行う移動距離の閾値（メートル）")] // ▼▼▼ 追加 ▼▼▼
    public float updateDistanceThreshold = 5.0f;

    [Header("Agent設定")]
    [Tooltip("このビルダーが担当するAgentの名前")]
    public string agentTypeName = "Humanoid";

    // 内部変数
    private NavMeshData m_NavMesh;
    private NavMeshDataInstance m_Instance;
    private List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();
    private AsyncOperation m_Operation;
    private NavMeshBuildSettings m_BuildSettings;
    
    private Vector3 lastUpdatePosition = new Vector3(-9999, -9999, -9999); // 前回の更新位置

    void OnEnable()
    {
        if (!GetBuildSettings(agentTypeName, out m_BuildSettings))
        {
            Debug.LogError($"AgentType '{agentTypeName}' が見つかりません。Navigationウィンドウの設定を確認してください。");
            this.enabled = false;
            return;
        }

        m_NavMesh = new NavMeshData();
        m_Instance = NavMesh.AddNavMeshData(m_NavMesh);
        
        if (trackedTarget == null) trackedTarget = transform;
        
        StartCoroutine(UpdateNavMeshCoroutine());
    }

    void OnDisable()
    {
        m_Instance.Remove();
    }

    IEnumerator UpdateNavMeshCoroutine()
    {
        while (true)
        {
            // 1. プレイヤーが十分に移動したかチェック
            if (Vector3.Distance(trackedTarget.position, lastUpdatePosition) > updateDistanceThreshold)
            {
                lastUpdatePosition = trackedTarget.position;
                
                // 2. NavMeshの更新処理を実行（ここを分割して軽くする）
                yield return StartCoroutine(UpdateNavMeshAsync());
            }

            // 更新が終わったら、または移動していなければ、少し待機して再チェック
            // 頻繁すぎるとチェック自体が無駄になるので0.2秒くらい空ける
            yield return new WaitForSeconds(0.2f);
        }
    }

    // 重い処理をフレーム分割して実行するコルーチン
    IEnumerator UpdateNavMeshAsync()
    {
        // --- ステップ1: データの収集 (CollectSources) ---
        // ここが重いので、メインスレッドで行うが、終わったら一旦休憩する
        
        m_Sources.Clear(); // リストを使い回す
        var bounds = new Bounds(trackedTarget.position, size);

        // A. 地面や障害物の収集
        NavMeshBuilder.CollectSources(
            bounds, 
            layerMask, 
            NavMeshCollectGeometry.PhysicsColliders, 
            0, 
            new List<NavMeshBuildMarkup>(), 
            m_Sources
        );

        // B. ModifierVolumeの収集
        // FindObjectsByTypeは重いので、キャッシュするか、頻度を落としたいが
        // 動的な生成に対応するため毎回呼ぶ。ただしSortMode.Noneで最速にする。
        var modifiers = FindObjectsByType<NavMeshModifierVolume>(FindObjectsSortMode.None);
        
        foreach (var mod in modifiers)
        {
            if (!mod.isActiveAndEnabled) continue;
            if (((1 << mod.gameObject.layer) & layerMask) == 0) continue;
            if (!mod.AffectsAgentType(m_BuildSettings.agentTypeID)) continue;

            var src = new NavMeshBuildSource();
            src.shape = NavMeshBuildSourceShape.ModifierBox;
            src.transform = mod.transform.localToWorldMatrix;
            src.size = mod.size;
            src.area = mod.area;
            m_Sources.Add(src);
        }

        // ★ここで1フレーム待つ！
        // これにより「収集」と「ビルド開始」の負荷が別々のフレームに分散される
        yield return null; 

        // --- ステップ2: 非同期ビルドの開始 ---
        m_Operation = NavMeshBuilder.UpdateNavMeshDataAsync(m_NavMesh, m_BuildSettings, m_Sources, bounds);
        
        // --- ステップ3: 完了待ち ---
        yield return m_Operation;
    }

    bool GetBuildSettings(string agentName, out NavMeshBuildSettings settings)
    {
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            var s = NavMesh.GetSettingsByIndex(i);
            string name = NavMesh.GetSettingsNameFromID(s.agentTypeID);
            if (name == agentName)
            {
                settings = s;
                return true;
            }
        }
        settings = default;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (trackedTarget != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawWireCube(trackedTarget.position, size);
        }
    }
}