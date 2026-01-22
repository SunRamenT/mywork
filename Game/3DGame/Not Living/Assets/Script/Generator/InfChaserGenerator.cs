using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

[Serializable]
public class InfChaserSpawnRule
{
    public string ruleDescription = "新規ルール";
    [Tooltip("この善悪値『以上』になったら条件を満たす")]
    public float maxGoodEvilValue = -10f;
    [Tooltip("この日数『以上』になったら条件を満たす")]
    public int minDaysSurvived = 3;
    [Tooltip("条件を満たした時に出現させるChaserの数")]
    public int chaserCount = 1;
}

public class InfChaserGenerator : MonoBehaviour
{
    [Header("Chaser Settings")]
    [Tooltip("生成するChaserのプレハブ")]
    public GameObject chaserPrefab;

    [Header("Spawn Rules")]
    [Tooltip("Chaserの出現条件と数のルール")]
    public List<InfChaserSpawnRule> spawnRules;

    [Header("Generator Settings")]
    public float spawnRadius = 50f;
    [Tooltip("プレイヤーからこの距離以上離れたら削除する")]
    public float despawnRadius = 60f; // 削除距離
    [Tooltip("このY座標（高さ）以下にのみChaserを生成します")]
    public float maxSpawnHeight = 10f;

    [Header("NavMesh Sampling")]
    [Tooltip("Chaser用のAgentTypeのインデックス番号")]
    public int agentTypeIndex = 1;
    public float sampleDistance = 20f;
    public int maxAttempts = 20;

    [Header("Performance")]
    [Tooltip("生成チェックを行う間隔（秒）")]
    public float checkInterval = 1.0f;
    private float checkTimer = 0f;

    private List<GameObject> chaserList = new List<GameObject>();
    private int agentTypeID;

    [Tooltip("壁を検知するためのレイヤー")]
    public LayerMask wallLayer; 
    
    // プレイヤーの参照をキャッシュ
    private Transform playerTransform;

    private void Start()
    {
        // プレイヤーを探す
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
        else Debug.LogError("Playerタグがついたオブジェクトが見つかりません！");

        if (agentTypeIndex >= NavMesh.GetSettingsCount())
        {
            Debug.LogError($"'{agentTypeIndex}' というインデックスのAgentTypeは存在しません。");
            this.enabled = false;
            return;
        }
        agentTypeID = NavMesh.GetSettingsByIndex(agentTypeIndex).agentTypeID;
    }

    private void Update()
    {
        if (playerTransform == null) return;
        if (AlignmentManager.Instance == null || GameTimeManager.Instance == null) return;

        // ▼ 距離による削除とリスト掃除 ▼
        // リストを逆順に回して、遠いものを削除する
        for (int i = chaserList.Count - 1; i >= 0; i--)
        {
            if (chaserList[i] == null)
            {
                chaserList.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(chaserList[i].transform.position, playerTransform.position);
            if (dist > despawnRadius)
            {
                Destroy(chaserList[i]);
                chaserList.RemoveAt(i);
            }
        }

        // タイマー処理（毎フレーム実行を防ぐ）
        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        float currentGoodEvil = AlignmentManager.Instance.CurrentAlignment.y;
        int currentDays = GameTimeManager.Instance.daysSurvived;
        
        int targetChaserCount = 0;
        foreach (var rule in spawnRules)
        {
            if (currentGoodEvil >= rule.maxGoodEvilValue && currentDays >= rule.minDaysSurvived)
            {
                targetChaserCount = Mathf.Max(targetChaserCount, rule.chaserCount);
            }
        }

        int currentChaserCount = chaserList.Count;

        if (currentChaserCount < targetChaserCount)
        {
            SpawnChaser();
        }
        else if (currentChaserCount > targetChaserCount)
        {
            DestroyChasers(currentChaserCount - targetChaserCount);
        }
    }

    private void SpawnChaser()
    {
        if (chaserPrefab == null) return;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // プレイヤーを中心にランダム座標を決める
            Vector3 randomPoint = UnityEngine.Random.insideUnitSphere * spawnRadius;
            randomPoint.y = 0; 
            Vector3 randomPos = playerTransform.position + randomPoint;
            
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            // NavMeshがない場所（まだ生成されていない場所）は除外される
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    if (!Physics.Raycast(hit.position + Vector3.up * 10f, Vector3.down, 20f, wallLayer))
                    {
                        // 親を指定せず生成（プレイヤーについていかないようにする）
                        GameObject newNPC = Instantiate(chaserPrefab, hit.position, Quaternion.identity, null); 
                        chaserList.Add(newNPC);
                        break; 
                    }
                }
            }
        }
    }

    private void DestroyChasers(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (chaserList.Count > 0)
            {
                GameObject chaserToDestroy = chaserList[0];
                chaserList.RemoveAt(0);
                if (chaserToDestroy != null) Destroy(chaserToDestroy);
            }
        }
    }
}