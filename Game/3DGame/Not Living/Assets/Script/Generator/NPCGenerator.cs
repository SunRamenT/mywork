using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPCをNavMesh上の高さ制限付きでランダム生成するクラス
/// </summary>
public class NPCGenerator : MonoBehaviour
{
    [Header("NPC Settings")]
    public GameObject npcPrefab;           // 生成するNPCのPrefab
    public int maxNPCCount = 10;           // 最大NPC数
    public float spawnRadius = 20f;        // NPC生成の水平範囲
    public float maxSpawnHeight = 5f;      // 高さ制限

    [Header("NavMesh Sampling")]
    public float sampleDistance = 15f;     // NavMeshサンプル距離
    public int maxAttempts = 10;           // 生成失敗時の最大再試行回数

    private List<GameObject> npcList = new List<GameObject>();

    void Update()
    {
        // 不要になったNPCをリストから削除（メモリリーク防止）
        npcList.RemoveAll(npc => npc == null);

        // NPCが足りない場合に生成
        if (npcList.Count < maxNPCCount)
        {
            SpawnNPC();
        }
    }

    /// <summary>
    /// NavMesh上の高さ制限付きでNPCを1体生成
    /// </summary>
    private void SpawnNPC()
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // ランダムな水平座標を決定
            Vector3 randomPos = transform.position + new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                10f, // 上空から落とすイメージ
                Random.Range(-spawnRadius, spawnRadius)
            );

            // NavMesh上の有効位置をサンプル
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            {
                // 高さ制限チェック
                if (hit.position.y <= maxSpawnHeight)
                {
                    GameObject newNPC = Instantiate(npcPrefab, hit.position, Quaternion.identity, transform);
                    npcList.Add(newNPC);
                    break; // 生成成功でループ終了
                }
            }
        }
    }
}
