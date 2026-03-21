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
    public Vector3 size = new Vector3(160.0f, 40.0f, 160.0f);

    [Tooltip("NavMeshの対象にするレイヤー（Ground, Obstacle, Wallなど）")]
    public LayerMask layerMask;

    [Tooltip("更新を行う移動距離の閾値（メートル）")] 
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

    private WaitForSeconds m_NavUpdateWait;
    private List<NavMeshBuildMarkup> m_EmptyMarkups = new List<NavMeshBuildMarkup>();

    // ★: 平方根計算を避けるための、閾値の二乗キャッシュ
    private float m_SqrUpdateDistanceThreshold;

    // ★修正: HashSetのforeach によるボックス化GCを回避するため、Registryの巡回にListを使う
    // NavMeshModifierRegistry.ActiveModifiers(HashSet)を直接foreachするとEnumeratorがボックス化される。
    // そのためModifierをListにコピーしてから巡回する専用キャッシュを用意する。
    // ※ CollectSources内部のUnityネイティブアロケーション（約22KB）はAPI側の制約で回避不可。
    private List<NavMeshModifierVolume> m_CachedModifiers = new List<NavMeshModifierVolume>();

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
        m_NavUpdateWait = new WaitForSeconds(0.5f);
        
        m_SqrUpdateDistanceThreshold = updateDistanceThreshold * updateDistanceThreshold;
        
        StartCoroutine(UpdateNavMeshCoroutine());
    }

    // ★修正: OnDisableでコルーチンを明示的に停止する
    // 停止しないと trackedTarget や m_NavMesh への参照が残り、
    // NullReferenceException が発生する可能性がある。
    void OnDisable()
    {
        StopAllCoroutines();
        m_Instance.Remove();
    }

    // ★修正: OnValidateで閾値の二乗キャッシュを再計算する
    // OnEnableのみでは、Inspector上でupdateDistanceThresholdを変更した際に
    // m_SqrUpdateDistanceThresholdが再計算されず、意図した距離で更新されない。
    void OnValidate()
    {
        m_SqrUpdateDistanceThreshold = updateDistanceThreshold * updateDistanceThreshold;
    }
    
    IEnumerator UpdateNavMeshCoroutine()
    {
        while (true)
        {
            if ((trackedTarget.position - lastUpdatePosition).sqrMagnitude > m_SqrUpdateDistanceThreshold)
            {
                lastUpdatePosition = trackedTarget.position;
                yield return StartCoroutine(UpdateNavMeshAsync());
            }
            yield return m_NavUpdateWait; 
        }
    }

    IEnumerator UpdateNavMeshAsync()
    {
        if (m_Operation != null && !m_Operation.isDone) yield break;

        m_Sources.Clear();
        var bounds = new Bounds(trackedTarget.position, size);

        // ※ CollectSources はUnity内部でネイティブアロケーションが発生するため、
        //   m_Sourcesをキャッシュしていても約22KBのGCAllocは回避できない。
        NavMeshBuilder.CollectSources(
            bounds, 
            layerMask, 
            NavMeshCollectGeometry.PhysicsColliders, 
            0, 
            m_EmptyMarkups, 
            m_Sources
        );

        // ★修正: HashSet<NavMeshModifierVolume>をそのままforeachするとEnumeratorが
        //   インターフェース経由になりボックス化GCが発生する。
        //   一度Listにコピーしてforeachすることでボックス化を回避する。
        m_CachedModifiers.Clear();
        m_CachedModifiers.AddRange(NavMeshModifierRegistry.ActiveModifiers);

        for (int i = 0; i < m_CachedModifiers.Count; i++)
        {
            var mod = m_CachedModifiers[i];
            if (mod == null) continue;
            if (!bounds.Contains(mod.transform.position)) continue;
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

        yield return null;

        m_Operation = NavMeshBuilder.UpdateNavMeshDataAsync(m_NavMesh, m_BuildSettings, m_Sources, bounds);
        
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