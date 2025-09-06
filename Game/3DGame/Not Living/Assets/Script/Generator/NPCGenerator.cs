using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

[Serializable]
public class SpawnableNPC
{
    public string description = "NPC Type";
    public GameObject npcPrefab;
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
        npcList.RemoveAll(npc => npc == null);

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
        int currentCount = CountNpcsOfType(npcType.npcPrefab.name);

        if (shouldBeActive)
        {
            // 【補充】
            if (currentCount < npcType.spawnCount)
            {
                SpawnNPC(npcType.npcPrefab);
            }
            // 【超過分を消去】
            else if (currentCount > npcType.spawnCount)
            {
                DestroyNpcsOfType(npcType.npcPrefab.name, npcType.spawnCount);
            }
        }
        else
        {
            // 【時間外は全員消去】
            if (currentCount > 0)
            {
                DestroyNpcsOfType(npcType.npcPrefab.name, 0);
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

    private int CountNpcsOfType(string prefabName)
    {
        int count = 0;
        foreach (var npc in npcList)
        {
            if (npc != null && npc.name.StartsWith(prefabName))
            {
                count++;
            }
        }
        return count;
    }

    private void DestroyNpcsOfType(string prefabName, int targetCount)
    {
        List<GameObject> candidates = new List<GameObject>();
        foreach (var npc in npcList)
        {
            if (npc != null && npc.name.StartsWith(prefabName))
            {
                candidates.Add(npc);
            }
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