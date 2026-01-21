using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

[Serializable]
public class SpawnableNPC
{
    public string description = "NPC Type";
    public GameObject npcPrefab;
    [Tooltip("このNPCタイプを識別するためのユニークなタグ名")]
    public string npcTag;
    [Range(0, 50)]
    public int spawnCount = 10;
    [Tooltip("この時間（時）から出現を開始")]
    [Range(0, 23)]
    public int startHour = 7;
    [Tooltip("この時間（時）まで出現")]
    [Range(0, 23)]
    public int endHour = 19;
}

public class NPCGenerator : MonoBehaviour
{
    [Header("NPC Settings")]
    [Tooltip("生成したいNPCの種類と条件をリストで設定")]
    public List<SpawnableNPC> spawnableNpcs;

    [Header("Generator Settings")]
    public float spawnRadius = 20f;
    public float maxSpawnHeight = 5f;
    
    [Header("NavMesh Sampling")]
    [Tooltip("どのAgent TypeのNavMeshに生成するか、インデックス番号を指定")]
    public int agentTypeIndex = 0;
    public float sampleDistance = 15f;
    public int maxAttempts = 10;

    [Header("Performance")] // ▼▼▼ 追加: 負荷対策 ▼▼▼
    [Tooltip("生成判定を行う間隔（秒）。毎フレーム処理を防ぐ")]
    public float checkInterval = 1.0f;
    private float checkTimer = 0f;

    private List<GameObject> npcList = new List<GameObject>();
    private int agentTypeID;

    [Tooltip("壁を検知するためのレイヤー")]
    public LayerMask wallLayer; 

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
        // Destroyされたオブジェクトへの参照をリストから掃除する
        npcList.RemoveAll(item => item == null);

        // ▼▼▼ 追加: タイマー処理 ▼▼▼
        // NavMeshがまだ生成されていない時に毎フレームSamplePositionすると重いため、
        // 1秒に1回だけチェックするように制限する。
        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        if (GameTimeManager.Instance == null) return;
        int currentHour = GameTimeManager.Instance.currentHour;

        foreach (var npcType in spawnableNpcs)
        {
            ManageNpcCount(npcType, currentHour);
        }
    }

    private void ManageNpcCount(SpawnableNPC npcType, int currentHour)
    {
        bool shouldBeActive = IsTimeInRange(currentHour, npcType.startHour, npcType.endHour);
        int currentCount = CountNpcsByTag(npcType.npcTag);

        if (shouldBeActive)
        {
            if (currentCount < npcType.spawnCount)
            {
                SpawnNPC(npcType.npcPrefab);
            }
            else if (currentCount > npcType.spawnCount)
            {
                DestroyNpcsByTag(npcType.npcTag, npcType.spawnCount);
            }
        }
        else
        {
            if (currentCount > 0)
            {
                DestroyNpcsByTag(npcType.npcTag, 0);
            }
        }
    }

    private void SpawnNPC(GameObject prefabToSpawn)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 randomPos = transform.position + new Vector3(
                UnityEngine.Random.Range(-spawnRadius, spawnRadius), 10f, UnityEngine.Random.Range(-spawnRadius, spawnRadius)
            );

            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            // ここが重要：NavMeshがまだビルドされていないエリアでは false が返るため、
            // 生成されずにループを抜ける。checkIntervalのおかげで、失敗しても負荷にならない。
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    if (!Physics.Raycast(hit.position + Vector3.up * 10f, Vector3.down, 20f, wallLayer))
                    {
                        GameObject newNPC = Instantiate(prefabToSpawn, hit.position, Quaternion.identity, transform);
                        npcList.Add(newNPC);
                        break; 
                    }
                }
            }
        }
    }

    // (以下のメソッドは変更なし: CountNpcsByTag, DestroyNpcsByTag, IsTimeInRange)
    private int CountNpcsByTag(string tag)
    {
        int count = 0;
        foreach (var npc in npcList)
        {
            if (npc != null && npc.CompareTag(tag)) count++;
        }
        return count;
    }

    private void DestroyNpcsByTag(string tag, int targetCount)
    {
        List<GameObject> candidates = new List<GameObject>();
        foreach (var npc in npcList)
        {
            if (npc != null && npc.CompareTag(tag)) candidates.Add(npc);
        }

        int amountToDestroy = candidates.Count - targetCount;
        for (int i = 0; i < amountToDestroy; i++)
        {
            GameObject npcToDestroy = candidates[i];
            if (npcToDestroy != null)
            {
                npcList.Remove(npcToDestroy);
                Destroy(npcToDestroy);
            }
        }
    }

    private bool IsTimeInRange(int time, int startTime, int endTime)
    {
        if (startTime > endTime) return time >= startTime || time < endTime;
        else return time >= startTime && time < endTime;
    }
}