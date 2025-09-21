// ChaserGenerator.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


// ChaserGenerator.cs の先頭にこのクラスを追加
[System.Serializable]
public class ChaserSpawnRule
{
    public string ruleDescription = "新規ルール"; // Inspectorでの見出し
    [Tooltip("この善悪値『以下』になったら条件を満たす")]
    public float maxGoodEvilValue = -10f;
    [Tooltip("この日数『以上』になったら条件を満たす")]
    public int minDaysSurvived = 3;
    [Tooltip("条件を満たした時に出現させるChaserの数")]
    public int chaserCount = 1;
}
// このファイルの上部に ChaserSpawnRule クラスを記述

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

    [Header("NavMesh Sampling")]
    [Tooltip("Chaser用のAgentTypeのインデックス番号 (ChaserMoveの設定と合わせる)")]
    public int agentTypeIndex = 1;
    public float sampleDistance = 20f;
    public int maxAttempts = 20;

    private List<GameObject> chaserList = new List<GameObject>();
    private int agentTypeID;

    private void Start()
    {
        // NavMeshのAgentType IDを取得
        if (agentTypeIndex >= NavMesh.GetSettingsCount())
        {
            Debug.LogError($"'{agentTypeIndex}' というインデックスのAgentTypeは存在しません。");
            this.enabled = false; // エラー時はスクリプトを無効化
            return;
        }
        agentTypeID = NavMesh.GetSettingsByIndex(agentTypeIndex).agentTypeID;
    }

    private void Update()
    {
        // 必要なマネージャーが存在しない場合は処理を中断
        if (AlignmentManager.Instance == null || GameTimeManager.Instance == null) return;

        // 破壊されたChaserをリストから除去
        chaserList.RemoveAll(item => item == null);

        // 現在の善悪値と経過日数を取得
        float currentGoodEvil = AlignmentManager.Instance.CurrentAlignment.y;
        int currentDays = GameTimeManager.Instance.daysSurvived;

        // --- ルールに基づいて、現在のフレームで目標となるChaserの数を決定 ---
        int targetChaserCount = 0;
        foreach (var rule in spawnRules)
        {
            // 条件（善悪値と経過日数）を満たしているかチェック
            if (currentGoodEvil <= rule.maxGoodEvilValue && currentDays >= rule.minDaysSurvived)
            {
                // 条件を満たした場合、目標数を更新
                // リストの下にある、より厳しい条件のルールで上書きされていく
                targetChaserCount = rule.chaserCount;
            }
        }

        // --- 現在の数と目標数を比較し、生成または破壊を行う ---
        int currentChaserCount = chaserList.Count;

        if (currentChaserCount < targetChaserCount)
        {
            // 足りない分だけChaserを生成
            for (int i = 0; i < targetChaserCount - currentChaserCount; i++)
            {
                SpawnChaser();
            }
        }
        else if (currentChaserCount > targetChaserCount)
        {
            // 多すぎる分だけChaserを破壊
            DestroyChasers(currentChaserCount - targetChaserCount);
        }
    }

    private void SpawnChaser()
    {
        if (chaserPrefab == null) return;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                GameObject newChaser = Instantiate(chaserPrefab, hit.position, Quaternion.identity);
                chaserList.Add(newChaser);
                return; // 1体生成したら終了
            }
        }
        Debug.LogWarning("Chaserの生成に失敗しました。有効なNavMeshの範囲を確認してください。");
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