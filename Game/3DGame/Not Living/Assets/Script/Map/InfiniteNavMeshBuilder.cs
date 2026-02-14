using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic;

// NavMeshComponents がインストールされている前提
// (Package Manager > AI Navigation)

public class InfiniteNavMeshBuilder : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("追跡する対象（プレイヤー）")]
    public Transform trackedTarget;
    
    [Tooltip("NavMeshを生成する範囲（X, Y, Z）")]
    // ViewDistance=3 (約60m) なら、余裕を持って 160x40x160 くらいが安全
    public Vector3 size = new Vector3(160.0f, 40.0f, 160.0f);

    [Tooltip("NavMeshの対象にするレイヤー（Ground, Obstacle, Wallなど）")]
    public LayerMask layerMask;

    [Tooltip("更新を行う移動距離の閾値（メートル）")] 
    // あまり頻繁すぎると負荷になるため、10m〜15m 程度推奨
    public float updateDistanceThreshold = 10.0f;

    [Header("Agent設定")]
    public string agentTypeName = "Humanoid";

    // 内部変数
    private NavMeshData m_NavMesh;
    private NavMeshDataInstance m_Instance;
    private List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();
    private AsyncOperation m_Operation;
    private NavMeshBuildSettings m_BuildSettings;
    
    private Vector3 lastUpdatePosition = new Vector3(-9999, -9999, -9999);

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
            // プレイヤーが閾値以上移動したかチェック
            if (Vector3.Distance(trackedTarget.position, lastUpdatePosition) > updateDistanceThreshold)
            {
                lastUpdatePosition = trackedTarget.position;
                
                // 非同期更新を開始
                yield return StartCoroutine(UpdateNavMeshAsync());
            }

            // 更新チェック頻度（0.5秒に1回など、少し間引く）
            yield return new WaitForSeconds(0.5f);
        }
    }

    // ★最適化の要：非同期ビルドプロセス
    IEnumerator UpdateNavMeshAsync()
    {
        // もし前の更新が終わっていなければスキップ（二重実行防止）
        if (m_Operation != null && !m_Operation.isDone) yield break;

        m_Sources.Clear();
        var bounds = new Bounds(trackedTarget.position, size);

        // --- A. 地面や壁（物理コライダー）の収集 ---
        // これはUnity標準機能で範囲内を高速に収集できる
        NavMeshBuilder.CollectSources(
            bounds, 
            layerMask, 
            NavMeshCollectGeometry.PhysicsColliders, 
            0, 
            new List<NavMeshBuildMarkup>(), 
            m_Sources
        );

        // --- B. NavMeshModifierの収集（ここが劇的に高速化） ---
        // 以前: FindObjectsByType でシーン全検索 (O(N)) -> 重い
        // 今回: Registry からアクティブなものだけ取得 (O(k)) -> 速い
        
        foreach (var mod in NavMeshModifierRegistry.ActiveModifiers)
        {
            if (mod == null) continue;

            // 簡易的な範囲チェック：ベイク範囲に入っているものだけ対象にする
            // (Modifierの中心座標が bounds に含まれているか、あるいは距離で判定)
            if (!bounds.Contains(mod.transform.position)) 
            {
                // 正確にはBounds同士の交差判定(Intersects)が良いが、
                // 計算コスト削減のため「中心点が含まれるか」程度でも十分実用的
                continue; 
            }
            
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

        // 収集が終わったら、ビルド処理の前に1フレーム休んでメインスレッドを解放
        yield return null; 

        // --- 非同期ビルド実行 ---
        m_Operation = NavMeshBuilder.UpdateNavMeshDataAsync(m_NavMesh, m_BuildSettings, m_Sources, bounds);
        
        yield return m_Operation;
        
        // デバッグ用: 更新完了ログ
        // Debug.Log($"NavMesh Updated at {trackedTarget.position}");
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