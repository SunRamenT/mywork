using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

[Serializable]
public class InfSpawnableNPC
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

public class InfNPCGenerator : MonoBehaviour
{
    [Header("NPC Settings")]
    public List<InfSpawnableNPC> spawnableNpcs;

    [Header("Generator Settings")]
    public float spawnRadius = 40f; 
    public float despawnRadius = 50f; // 削除距離
    public float maxSpawnHeight = 5f;
    
    [Header("NavMesh Sampling")]
    public int agentTypeIndex = 0;
    public float sampleDistance = 15f;
    public int maxAttempts = 10;

    [Header("Performance")]
    public float checkInterval = 1.0f;
    private float checkTimer = 0f;

    private List<GameObject> npcList = new List<GameObject>();
    private int agentTypeID;

    [Tooltip("壁を検知するためのレイヤー")]
    public LayerMask wallLayer; 

    private Transform playerTransform; // プレイヤー参照

    [Header("初期エリア設定")]
    public float safeZoneRadiusMeter = 30f; // 3x3マスなら 20m*1.5 = 30m くらい

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
        else Debug.LogError("Playerタグがついたオブジェクトが見つかりません！");

        if (agentTypeIndex < NavMesh.GetSettingsCount())
        {
            agentTypeID = NavMesh.GetSettingsByIndex(agentTypeIndex).agentTypeID;
        }
        else
        {
            Debug.LogError($"'{agentTypeIndex}' というインデックスのAgentTypeは存在しません。");
            this.enabled = false;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        // ▼ 距離による削除とリスト掃除 ▼
        for (int i = npcList.Count - 1; i >= 0; i--)
        {
            if (npcList[i] == null)
            {
                npcList.RemoveAt(i);
                continue;
            }
            // プレイヤーから離れすぎたNPCは削除
            if (Vector3.Distance(npcList[i].transform.position, playerTransform.position) > despawnRadius)
            {
                Destroy(npcList[i]);
                npcList.RemoveAt(i);
            }
        }

        // タイマー処理
        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        if (GameTimeManager.Instance == null) return;
        int currentHour = GameTimeManager.Instance.currentHour;

        foreach (var npcType in spawnableNpcs)
        {
            ManageNpcCount(npcType, currentHour);
        }
    }

    private void ManageNpcCount(InfSpawnableNPC npcType, int currentHour)
    {
        bool shouldBeActive = IsTimeInRange(currentHour, npcType.startHour, npcType.endHour);
        int currentCount = CountNpcsByTag(npcType.npcTag);

        if (shouldBeActive)
        {
            if (currentCount < npcType.spawnCount)
            {
                SpawnNPC(npcType.npcPrefab);
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
            // プレイヤー中心に生成
            Vector3 randomPoint = UnityEngine.Random.insideUnitSphere * spawnRadius;
            randomPoint.y = 0;
            Vector3 randomPos = playerTransform.position + randomPoint;

            // 原点付近なら生成キャンセル
            // XとZの距離が一定以内なら、そこは初期エリアなので生成しない
            if (Mathf.Abs(randomPos.x) < safeZoneRadiusMeter && Mathf.Abs(randomPos.z) < safeZoneRadiusMeter)
            {
                continue;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            // NavMeshがある場所のみ生成（NavMesh生成待ち対策）
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    if (!Physics.Raycast(hit.position + Vector3.up * 10f, Vector3.down, 20f, wallLayer))
                    {
                        // 親を指定せず生成（プレイヤーについていかないようにする）
                        GameObject newNPC = Instantiate(prefabToSpawn, hit.position, Quaternion.identity, null);
                        npcList.Add(newNPC);
                        break; 
                    }
                }
            }
        }
    }

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