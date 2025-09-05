using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System; // Serializable属性のために必要

// ▼▼▼ NPCの種類と出現条件をまとめるための新しいクラスを追加 ▼▼▼
[Serializable]
public class SpawnableNPC
{
    public string description = "NPC Type";
    public GameObject npcPrefab;
    [Range(0, 50)]
    public int spawnCount = 10;
    [Tooltip("この時間（時）から出現を開始")]
    [Range(0, 23)]
    public int startHour = 7; // 朝7時
    [Tooltip("この時間（時）まで出現")]
    [Range(0, 23)]
    public int endHour = 19; // 夜19時
}

/// <summary>
/// 時間帯や種類に応じてNPCの出現数を変えるクラス
/// </summary>
public class NPCGenerator : MonoBehaviour
{
    [Header("NPC Settings")]
    [Tooltip("生成したいNPCの種類と条件をリストで設定")]
    public List<SpawnableNPC> spawnableNpcs; // ▼▼▼ 複数のNPCを登録できるリストに変更 ▼▼▼

    [Header("Generator Settings")]
    public float spawnRadius = 20f;
    public float maxSpawnHeight = 5f;
    
    [Header("NavMesh Sampling")]
    [Tooltip("どのAgent TypeのNavMeshに生成するか、インデックス番号を指定")]
    public int agentTypeIndex = 0;
    public float sampleDistance = 15f;
    public int maxAttempts = 10;

    private List<GameObject> npcList = new List<GameObject>();
    private int agentTypeID;

    void Start()
    {
        if (agentTypeIndex >= NavMesh.GetSettingsCount())
        {
            Debug.LogError($"'{agentTypeIndex}' というインデックスのAgentTypeは存在しません。");
            return;
        }
        agentTypeID = NavMesh.GetSettingsByIndex(agentTypeIndex).agentTypeID;
    }

    void Update()
    {
        // 不要になったNPCをリストから削除
        npcList.RemoveAll(npc => npc == null);

        // 現在の時刻を取得
        if (GameTimeManager.Instance == null) return;
        int currentHour = GameTimeManager.Instance.currentHour;

        // 現在の時間帯に適したNPCを探し、数を調整する
        foreach (var npcType in spawnableNpcs)
        {
            bool shouldBeActive = IsTimeInRange(currentHour, npcType.startHour, npcType.endHour);

            if (shouldBeActive)
            {
                // 現在の時間帯なので、指定された数になるまで補充する
                int currentCount = CountNpcsOfType(npcType.npcPrefab.name);
                if (currentCount < npcType.spawnCount)
                {
                    SpawnNPC(npcType.npcPrefab);
                }
            }
        }
    }

    // 指定されたプレハブのNPCを1体生成
    private void SpawnNPC(GameObject prefabToSpawn)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 randomPos = transform.position + new Vector3(
                UnityEngine.Random.Range(-spawnRadius, spawnRadius), 10f, UnityEngine.Random.Range(-spawnRadius, spawnRadius)
            );

            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    GameObject newNPC = Instantiate(prefabToSpawn, hit.position, Quaternion.identity, transform);
                    npcList.Add(newNPC);
                    break;
                }
            }
        }
    }

    // 特定の種類のNPCが現在何体いるか数える
    private int CountNpcsOfType(string prefabName)
    {
        int count = 0;
        foreach (var npc in npcList)
        {
            if (npc.name.StartsWith(prefabName))
            {
                count++;
            }
        }
        return count;
    }

    // 現在の時間が指定された範囲内にあるかチェックする
    private bool IsTimeInRange(int time, int startTime, int endTime)
    {
        // 夜をまたぐ時間帯（例: 22時～翌5時）に対応
        if (startTime > endTime)
        {
            return time >= startTime || time < endTime;
        }
        else
        {
            return time >= startTime && time < endTime;
        }
    }
}