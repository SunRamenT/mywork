using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

[Serializable]
public class ChaserSpawnRule
{
    public string ruleDescription = "新規ルール";
    [Tooltip("この善悪値『以上』になったら条件を満たす")]
    public float maxGoodEvilValue = -10f;
    [Tooltip("この日数『以上』になったら条件を満たす")]
    public int minDaysSurvived = 3;
    [Tooltip("条件を満たした時に出現させるChaserの数")]
    public int chaserCount = 1;
}

public class ChaserGenerator : MonoBehaviour
{
    [Header("Chaser Settings")]
    [Tooltip("生成するChaserのプレハブ")]
    public GameObject chaserPrefab;

    [Header("Spawn Rules")]
    [Tooltip("Chaserの出現条件と数のルール。条件が厳しいものをリストの下に配置してください。")]
    public List<ChaserSpawnRule> spawnRules;

    [Header("Generator Settings")]
    public float spawnRadius = 50f;
    [Tooltip("このY座標（高さ）以下にのみChaserを生成します")]
    public float maxSpawnHeight = 10f;

    [Header("NavMesh Sampling")]
    [Tooltip("Chaser用のAgentTypeのインデックス番号 (ChaserMoveの設定と合わせる)")]
    public int agentTypeIndex = 1;
    public float sampleDistance = 20f;
    public int maxAttempts = 20;

    private List<GameObject> chaserList = new List<GameObject>();
    private int agentTypeID;

    [Tooltip("壁を検知するためのレイヤー")] // ▼▼▼ 追加 ▼▼▼
    public LayerMask wallLayer; 

    private void Start()
    {
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
        if (AlignmentManager.Instance == null || GameTimeManager.Instance == null) return;

        chaserList.RemoveAll(item => item == null);

        float currentGoodEvil = AlignmentManager.Instance.CurrentAlignment.y;
        int currentDays = GameTimeManager.Instance.daysSurvived;
        
        int targetChaserCount = 0;
        foreach (var rule in spawnRules)
        {
            if (currentGoodEvil >= rule.maxGoodEvilValue && currentDays >= rule.minDaysSurvived)
            {
                targetChaserCount = rule.chaserCount;
            }
        }

        int currentChaserCount = chaserList.Count;

        if (currentChaserCount < targetChaserCount)
        {
            for (int i = 0; i < targetChaserCount - currentChaserCount; i++)
            {
                SpawnChaser();
            }
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
            // ▼▼▼ この行を修正 ▼▼▼
            Vector3 randomPos = transform.position + UnityEngine.Random.insideUnitSphere * spawnRadius;
            // ▲▲▲▲▲▲▲▲▲▲▲▲
            
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    // ▼▼▼ 壁の中かどうかを追加でチェック ▼▼▼
                    // 生成地点の真上から真下に向けてレイキャストを飛ばす
                    if (!Physics.Raycast(hit.position + Vector3.up * 10f, Vector3.down, 20f, wallLayer))
                    {
                        // レイキャストがWallレイヤーに当たらなければ、そこは壁の中ではない
                        GameObject newNPC = Instantiate(chaserPrefab, hit.position, Quaternion.identity, transform);
                        chaserList.Add(newNPC);
                        break; // 生成に成功したのでループを抜ける
                    }
                    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
                }
            }
        }
        Debug.LogWarning("Chaserの生成に失敗しました。有効なNavMeshの範囲または高さを確認してください。");
    }

    private void DestroyChasers(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (chaserList.Count > 0)
            {
                GameObject chaserToDestroy = chaserList[0];
                chaserList.RemoveAt(0);
                Destroy(chaserToDestroy);
            }
        }
    }
}