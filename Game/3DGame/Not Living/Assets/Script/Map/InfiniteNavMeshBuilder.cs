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

    [Tooltip("NavMeshの対象にするレイヤー（RoadやGroundを指定）")]
    public LayerMask layerMask;

    [Header("Agent設定")]
    [Tooltip("このビルダーが担当するAgentの名前")]
    public string agentTypeName = "Humanoid";

    // 内部変数
    private NavMeshData m_NavMesh;
    private NavMeshDataInstance m_Instance;
    private List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();
    private AsyncOperation m_Operation;
    private NavMeshBuildSettings m_BuildSettings;

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
            UpdateNavMesh(true);
            yield return m_Operation;
            yield return new WaitForSeconds(0.5f);
        }
    }

    void UpdateNavMesh(bool asyncUpdate = false)
    {
        m_Sources.Clear();
        var bounds = new Bounds(trackedTarget.position, size);
        
        // 1. 通常のコライダー（地面や壁）を収集
        NavMeshBuilder.CollectSources(
            bounds, 
            layerMask, 
            NavMeshCollectGeometry.PhysicsColliders, 
            0, 
            new List<NavMeshBuildMarkup>(), 
            m_Sources
        );

        // 2. NavMeshModifierVolume を手動で収集
        var modifiers = FindObjectsByType<NavMeshModifierVolume>(FindObjectsSortMode.None);
        
        foreach (var mod in modifiers)
        {
            if (!mod.isActiveAndEnabled) continue;
            if (((1 << mod.gameObject.layer) & layerMask) == 0) continue;

            // ▼▼▼ 追加: Agent Typeの判定 ▼▼▼
            // 現在ビルド中のAgent IDが、Modifierの対象に含まれていなければ無視する
            if (!mod.AffectsAgentType(m_BuildSettings.agentTypeID)) continue;
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            var src = new NavMeshBuildSource();
            src.shape = NavMeshBuildSourceShape.ModifierBox;
            src.transform = mod.transform.localToWorldMatrix;
            src.size = mod.size;
            src.area = mod.area;
            
            m_Sources.Add(src);
        }

        // 更新処理
        if (asyncUpdate)
            m_Operation = NavMeshBuilder.UpdateNavMeshDataAsync(m_NavMesh, m_BuildSettings, m_Sources, bounds);
        else
            NavMeshBuilder.UpdateNavMeshData(m_NavMesh, m_BuildSettings, m_Sources, bounds);
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