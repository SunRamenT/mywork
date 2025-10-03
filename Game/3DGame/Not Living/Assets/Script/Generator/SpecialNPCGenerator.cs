using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

// SpecialNPCGenerator.cs の先頭にこのクラスを追加
[System.Serializable]
public class SpawnableSpecialNPC
{
    public string description = "特殊NPC";
    public GameObject npcPrefab;
    [Tooltip("このNPCが出現する確率（パーセント）")]
    [Range(0, 100)]
    public float spawnChance = 50f;
    [Tooltip("このNPCを何体出現させるか")]
    [Range(1, 10)]
    public int spawnCount = 1;
}
// このファイルの上部に SpawnableSpecialNPC クラスを記述

public class SpecialNPCGenerator : MonoBehaviour
{
    [Header("Special NPC Settings")]
    [Tooltip("確率で生成したい特殊NPCのリスト")]
    public List<SpawnableSpecialNPC> specialNpcs;

    [Header("Generator Settings")]
    public float spawnRadius = 50f;
    public float maxSpawnHeight = 5f;
    public LayerMask wallLayer;

    [Header("NavMesh Sampling")]
    [Tooltip("どのAgent TypeのNavMeshに生成するか")]
    public int agentTypeIndex = 0;
    public float sampleDistance = 20f;
    public int maxAttemptsPerSpawn = 15;

    private int agentTypeID;
    private int lastCheckHour = -1; // 前回チェックした時間を記録

    // 一度出現判定を行ったNPCタイプを記録しておくリスト
    private HashSet<SpawnableSpecialNPC> alreadyAttemptedSpawns = new HashSet<SpawnableSpecialNPC>();

    void Start()
    {
        // NavMeshのAgentType IDを取得
        if (agentTypeIndex >= NavMesh.GetSettingsCount())
        {
            Debug.LogError($"'{agentTypeIndex}' というインデックスのAgentTypeは存在しません。");
            this.enabled = false;
            return;
        }
        agentTypeID = NavMesh.GetSettingsByIndex(agentTypeIndex).agentTypeID;
    }

    void Update()
    {
        if (GameTimeManager.Instance == null) return;

        int currentHour = GameTimeManager.Instance.currentHour;

        // まだ同じ時間（時）なら、何もしない（1時間に1回だけ判定するため）
        if (currentHour == lastCheckHour)
        {
            return;
        }

        // 現在の時間を記録
        lastCheckHour = currentHour;
        
        // 深夜0時か正午12時になったら、出現判定を行う
        if (currentHour == 0 || currentHour == 12)
        {
            AttemptToSpawnAll();
        }
    }

    private void AttemptToSpawnAll()
    {
        Debug.Log("半日経過。特殊NPCの出現判定を開始します...");

        // 設定リストにある各特殊NPCについてループ
        foreach (var npcType in specialNpcs)
        {
            // まだ一度も出現判定を行っていないNPCか？
            if (!alreadyAttemptedSpawns.Contains(npcType))
            {
                // 0から100までのランダムな数値を生成
                float randomValue = UnityEngine.Random.Range(0f, 100f);

                // ランダムな数値が、設定した出現確率よりも低い場合のみ生成
                if (randomValue < npcType.spawnChance)
                {
                    Debug.Log($"<color=green>{npcType.description} の出現が決定！ {npcType.spawnCount}体生成します。</color>");
                    // 指定された数だけNPCを生成
                    for (int i = 0; i < npcType.spawnCount; i++)
                    {
                        SpawnSingleNPC(npcType.npcPrefab);
                    }

                    // 一度出現が決定したら、このタイプはもう判定しない
                    alreadyAttemptedSpawns.Add(npcType);
                }
                else
                {
                    Debug.Log($"{npcType.description} は今回出現しませんでした。");
                }
            }
        }
    }

    private void SpawnSingleNPC(GameObject prefabToSpawn)
    {
        for (int attempt = 0; attempt < maxAttemptsPerSpawn; attempt++)
        {
            Vector3 randomPos = transform.position + UnityEngine.Random.insideUnitSphere * spawnRadius;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = this.agentTypeID, areaMask = NavMesh.AllAreas };

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, filter))
            {
                if (hit.position.y <= maxSpawnHeight)
                {
                    if (!Physics.Raycast(hit.position + Vector3.up * 10f, Vector3.down, 20f, wallLayer))
                    {
                        Instantiate(prefabToSpawn, hit.position, Quaternion.identity, transform);
                        return; // 1体生成したら終了
                    }
                }
            }
        }
        Debug.LogWarning($"{prefabToSpawn.name} の生成に失敗しました。有効なNavMeshの範囲を確認してください。");
    }
}