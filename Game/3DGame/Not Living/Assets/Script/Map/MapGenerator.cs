using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.AI.Navigation;

[System.Serializable]
public class StructureItem
{
    public GameObject prefab;
    [Range(1, 100)]
    public int weight = 10;
    public bool allowRandomRotation = true; 
}

// : _modifiersをHashSet→Listに変更。
// HashSetはforeach時にEnumeratorがインターフェース経由でボックス化されGCAllocが発生する。
// Listはforeach・インデックスアクセスともにボックス化が起きないため、
// InfiniteNavMeshBuilder側でのコピーが不要になる。
// トレードオフ: Register/UnregisterがO(n)になるが、
// 登録・解除の頻度はチャンクの切り替え時のみであり実用上問題ない。
public static class NavMeshModifierRegistry
{
    private static readonly List<NavMeshModifierVolume> _modifiers = new List<NavMeshModifierVolume>();
    
    public static List<NavMeshModifierVolume> ActiveModifiers => _modifiers;

    public static void Register(NavMeshModifierVolume mod)
    {
        if (mod != null && !_modifiers.Contains(mod)) _modifiers.Add(mod);
    }

    public static void Unregister(NavMeshModifierVolume mod)
    {
        if (mod != null) _modifiers.Remove(mod);
    }
}

public class MapGenerator : MonoBehaviour
{
    [Header("設定")]
    public int tileSize = 20;
    [Range(1, 5)] public int viewDistance = 3; 
    public Transform player;

    [Header("シード値")]
    public int worldSeed = 12345;

    [Header("タイルデータ")]
    public List<MapTileData> allTiles;

    [Header("構造物データ")]
    public List<StructureItem> grassStructures;
    public List<StructureItem> concreteStructures;

    [Header("初期エリア設定")]
    public int safeZoneRadius = 1;

    [Header("最適化設定")]
    public int chunkSize = 5;

    // --- データ管理 ---
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    private Dictionary<Vector2Int, StructureItem> spawnedStructureData = new Dictionary<Vector2Int, StructureItem>();
    private Dictionary<Vector2Int, Quaternion> spawnedStructureRotations = new Dictionary<Vector2Int, Quaternion>();
    private HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
    private Dictionary<Vector2Int, List<GameObject>> chunkObjects = new Dictionary<Vector2Int, List<GameObject>>();
    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

    // ★修正: プールの最大容量。これを超えた返却分はDestroyする
    private const int MAX_POOL_SIZE = 50;

    private Vector2Int currentChunkCoord;
    private bool isUpdatingMap = false;

    // ★最適化: GC Alloc回避のためのキャッシュリスト（毎回 new しない）
    private List<MapTileData> m_CachedValidTiles = new List<MapTileData>();
    private List<MapTileData> m_CachedStrictTiles = new List<MapTileData>();
    private List<int> m_CachedTileWeights = new List<int>();
    private List<Vector2Int> m_CachedChunksToSpawn = new List<Vector2Int>();
    private List<Vector2Int> m_CachedChunksToRemove = new List<Vector2Int>();

    int GetCoordinateSeed(Vector2Int coord)
    {
        unchecked 
        {
            int hash = 17;
            hash = hash * 31 + coord.x;
            hash = hash * 31 + coord.y;
            hash = hash * 31 + worldSeed;
            return hash;
        }
    }

    void Start() 
    { 
        if (player != null)
        {
            currentChunkCoord = GetChunkCoordFromTileCoord(GetPlayerTileCoord());
            StartCoroutine(UpdateMapCoroutine(true));
        }
    }

    void Update()
    {
        if (player == null) return;
        
        Vector2Int playerTileCoord = GetPlayerTileCoord();
        Vector2Int playerChunkCoord = GetChunkCoordFromTileCoord(playerTileCoord);

        if (playerChunkCoord != currentChunkCoord && !isUpdatingMap)
        {
            currentChunkCoord = playerChunkCoord;
            StartCoroutine(UpdateMapCoroutine(false));
        }
    }
    
    void OnValidate()
    {
        if (viewDistance > 5) viewDistance = 5;
        if (chunkSize < 1) chunkSize = 1;
        if (chunkSize > 10) chunkSize = 10;
    }

    Vector2Int GetPlayerTileCoord()
    {
        return new Vector2Int(
            Mathf.RoundToInt(player.position.x / tileSize),
            Mathf.RoundToInt(player.position.z / tileSize)
        );
    }

    Vector2Int GetChunkCoordFromTileCoord(Vector2Int tileCoord)
    {
        return new Vector2Int(
            Mathf.FloorToInt((float)tileCoord.x / chunkSize),
            Mathf.FloorToInt((float)tileCoord.y / chunkSize)
        );
    }

    IEnumerator UpdateMapCoroutine(bool isImmediate)
    {
        isUpdatingMap = true;

        m_CachedChunksToSpawn.Clear();
        int safeViewDistance = Mathf.Min(viewDistance, 5);

        for (int x = currentChunkCoord.x - safeViewDistance; x <= currentChunkCoord.x + safeViewDistance; x++)
        {
            for (int y = currentChunkCoord.y - safeViewDistance; y <= currentChunkCoord.y + safeViewDistance; y++)
            {
                Vector2Int targetChunkCoord = new Vector2Int(x, y);
                if (!loadedChunks.Contains(targetChunkCoord))
                {
                    m_CachedChunksToSpawn.Add(targetChunkCoord);
                }
            }
        }

        m_CachedChunksToSpawn.Sort((a, b) => 
            Vector2Int.Distance(a, currentChunkCoord).CompareTo(Vector2Int.Distance(b, currentChunkCoord))
        );

        foreach (var chunkCoord in m_CachedChunksToSpawn)
        {
            CreateChunk(chunkCoord);
            loadedChunks.Add(chunkCoord);
            if (!isImmediate) yield return null;
        }

        CleanupChunks();
        isUpdatingMap = false;
    }

    void CreateChunk(Vector2Int chunkCoord)
    {
        if (!chunkObjects.ContainsKey(chunkCoord))
        {
            chunkObjects[chunkCoord] = new List<GameObject>();
        }

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                Vector2Int tileCoord = new Vector2Int(
                    chunkCoord.x * chunkSize + x,
                    chunkCoord.y * chunkSize + y
                );

                if (Mathf.Abs(tileCoord.x) <= safeZoneRadius && Mathf.Abs(tileCoord.y) <= safeZoneRadius)
                    continue;

                SpawnTileAt(tileCoord, chunkCoord);
                
                if (spawnedTileData.ContainsKey(tileCoord))
                {
                     SpawnStructureAt(tileCoord, spawnedTileData[tileCoord], chunkCoord);
                }
            }
        }
    }

    void SpawnTileAt(Vector2Int coord, Vector2Int chunkCoord)
    {
        MapTileData selectedData;
        
        if (spawnedTileData.ContainsKey(coord)) 
        {
            selectedData = spawnedTileData[coord];
        }
        else
        {
            System.Random tileRng = new System.Random(GetCoordinateSeed(coord));

            int reqTop    = GetNeighborConnection(coord, Vector2Int.up,    "bottom");
            int reqBottom = GetNeighborConnection(coord, Vector2Int.down,  "top");
            int reqLeft   = GetNeighborConnection(coord, Vector2Int.left,  "right");
            int reqRight  = GetNeighborConnection(coord, Vector2Int.right, "left");

            m_CachedValidTiles.Clear();
            m_CachedStrictTiles.Clear();

            for (int i = 0; i < allTiles.Count; i++)
            {
                var t = allTiles[i];
                if ((reqTop    == -1 || (int)t.top    == reqTop) &&
                    (reqBottom == -1 || (int)t.bottom == reqBottom) &&
                    (reqLeft   == -1 || (int)t.left   == reqLeft) &&
                    (reqRight  == -1 || (int)t.right  == reqRight))
                {
                    m_CachedValidTiles.Add(t);
                    if (IsPlacementValid(t, coord)) m_CachedStrictTiles.Add(t);
                }
            }

            var finalCandidates = m_CachedStrictTiles.Count > 0 ? m_CachedStrictTiles : m_CachedValidTiles;

            if (finalCandidates.Count == 0) 
            { 
                if (allTiles.Count > 0) finalCandidates.Add(allTiles[0]); 
                else return; 
            }

            selectedData = GetWeightedRandomTile(finalCandidates, coord, tileRng);
            spawnedTileData.Add(coord, selectedData);
        }

        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        GameObject obj = GetPooledObject(selectedData.prefab, pos, selectedData.prefab.transform.rotation);
        chunkObjects[chunkCoord].Add(obj);
    }

    void SpawnStructureAt(Vector2Int coord, MapTileData tileData, Vector2Int chunkCoord)
    {
        if (tileData.tileType != TileType.Grass && tileData.tileType != TileType.Concrete) return;

        StructureItem selectedStructure = null;
        Quaternion rotation = Quaternion.identity;

        if (spawnedStructureData.ContainsKey(coord))
        {
            selectedStructure = spawnedStructureData[coord];
            if (selectedStructure == null) return;
            if (spawnedStructureRotations.ContainsKey(coord)) rotation = spawnedStructureRotations[coord];
        }
        else
        {
            System.Random structRng = new System.Random(GetCoordinateSeed(coord));
            List<StructureItem> candidates = null;

            if (tileData.tileType == TileType.Grass) candidates = grassStructures;
            else if (tileData.tileType == TileType.Concrete) candidates = concreteStructures;

            if (candidates == null || candidates.Count == 0) return;

            selectedStructure = GetWeightedRandomStructure(candidates, structRng);
            spawnedStructureData.Add(coord, selectedStructure);
            
            if (selectedStructure != null)
            {
                if (selectedStructure.allowRandomRotation)
                {
                    float yAngle = structRng.Next(0, 4) * 90f;
                    rotation = Quaternion.Euler(0, yAngle, 0);
                }
                spawnedStructureRotations.Add(coord, rotation);
            }
        }

        if (selectedStructure != null && selectedStructure.prefab != null)
        {
            Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
            GameObject obj = GetPooledObject(selectedStructure.prefab, pos, rotation);
            chunkObjects[chunkCoord].Add(obj);
        }
    }

    void CleanupChunks()
    {
        m_CachedChunksToRemove.Clear();
        int keepThreshold = viewDistance + 1;

        foreach (var chunkCoord in loadedChunks)
        {
            int dx = Mathf.Abs(chunkCoord.x - currentChunkCoord.x);
            int dy = Mathf.Abs(chunkCoord.y - currentChunkCoord.y);
            int chebyshevDistance = Mathf.Max(dx, dy);

            if (chebyshevDistance > keepThreshold)
            {
                m_CachedChunksToRemove.Add(chunkCoord);
            }
        }

        foreach (var coord in m_CachedChunksToRemove)
        {
            if (chunkObjects.ContainsKey(coord))
            {
                foreach (var obj in chunkObjects[coord])
                {
                    if (obj == null) continue;
                    var mod = obj.GetComponent<NavMeshModifierVolume>();
                    if (mod != null) NavMeshModifierRegistry.Unregister(mod);
                    ReturnToPool(obj); 
                }
                chunkObjects[coord].Clear();
                chunkObjects.Remove(coord);
            }

            for (int x = 0; x < chunkSize; x++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    Vector2Int tileCoord = new Vector2Int(
                        coord.x * chunkSize + x,
                        coord.y * chunkSize + y
                    );
                    
                    if (spawnedTileData.ContainsKey(tileCoord)) spawnedTileData.Remove(tileCoord);
                    if (spawnedStructureData.ContainsKey(tileCoord)) spawnedStructureData.Remove(tileCoord);
                    if (spawnedStructureRotations.ContainsKey(tileCoord)) spawnedStructureRotations.Remove(tileCoord);
                }
            }
            loadedChunks.Remove(coord);
        }
    }

    GameObject GetPooledObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(prefab)) 
        {
            poolDictionary.Add(prefab, new Queue<GameObject>());
        }

        GameObject obj = null;

        if (poolDictionary[prefab].Count > 0) 
        { 
            obj = poolDictionary[prefab].Dequeue();
            while (obj == null && poolDictionary[prefab].Count > 0)
            {
                obj = poolDictionary[prefab].Dequeue();
            }
        }

        if (obj == null)
        { 
            obj = Instantiate(prefab, position, rotation, transform); 
            instanceToPrefabMap[obj] = prefab;
        }
        else
        {
            obj.transform.position = position; 
            obj.transform.rotation = rotation; 
            obj.SetActive(true); 
        }

        var mod = obj.GetComponent<NavMeshModifierVolume>();
        if (mod != null) NavMeshModifierRegistry.Register(mod);

        return obj; 
    }

    void ReturnToPool(GameObject obj) 
    { 
        if (obj == null) return;
        
        if (instanceToPrefabMap.ContainsKey(obj))
        {
            GameObject prefabKey = instanceToPrefabMap[obj];
            obj.SetActive(false);

            if (poolDictionary.ContainsKey(prefabKey) && poolDictionary[prefabKey].Count < MAX_POOL_SIZE)
            {
                poolDictionary[prefabKey].Enqueue(obj);
            }
            else
            {
                instanceToPrefabMap.Remove(obj);
                Destroy(obj);
                return;
            }
        }
        else
        {
            Destroy(obj);
        }
    }

    bool IsPlacementValid(MapTileData candidate, Vector2Int coord)
    {
        MapTileData top    = GetNeighborTileData(coord + Vector2Int.up);
        MapTileData bottom = GetNeighborTileData(coord + Vector2Int.down);
        MapTileData left   = GetNeighborTileData(coord + Vector2Int.left);
        MapTileData right  = GetNeighborTileData(coord + Vector2Int.right);
        if (candidate.tileType == TileType.Cross || candidate.tileType == TileType.T_Junction) { 
            if (IsBusyJunction(top) || IsBusyJunction(bottom) || IsBusyJunction(left) || IsBusyJunction(right)) return false;
         }
        if (candidate.tileType == TileType.DeadEnd) { if (IsType(top, TileType.DeadEnd) || IsType(bottom, TileType.DeadEnd) || IsType(left, TileType.DeadEnd) || IsType(right, TileType.DeadEnd)) return false; }
        return true;
    }
    bool IsBusyJunction(MapTileData data) { return data != null && (data.tileType == TileType.Cross || data.tileType == TileType.T_Junction); }
    bool IsType(MapTileData data, TileType type) { return data != null && data.tileType == type; }
    MapTileData GetNeighborTileData(Vector2Int coord) { return spawnedTileData.ContainsKey(coord) ? spawnedTileData[coord] : null; }

    MapTileData GetWeightedRandomTile(List<MapTileData> candidates, Vector2Int coord, System.Random rng)
    {
        int grassNeighbors = 0; int concreteNeighbors = 0;
        CheckNeighborType(coord + Vector2Int.up,    ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.down,  ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.left,  ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.right, ref grassNeighbors, ref concreteNeighbors);
        
        m_CachedTileWeights.Clear();
        int totalWeight = 0;

        for (int i = 0; i < candidates.Count; i++) 
        {
            var tile = candidates[i];
            int currentWeight = tile.weight;
            if (tile.tileType == TileType.Grass && grassNeighbors > 0) currentWeight *= (grassNeighbors * 4);
            else if (tile.tileType == TileType.Concrete && concreteNeighbors > 0) currentWeight *= (concreteNeighbors * 4);
            if (tile.tileType == TileType.Grass && concreteNeighbors > grassNeighbors) 
            { 
                currentWeight /= 2;
                if (currentWeight < 1) currentWeight = 1;
             }
            if (tile.tileType == TileType.Concrete && grassNeighbors > concreteNeighbors) 
            { 
                currentWeight /= 2; 
                if (currentWeight < 1) currentWeight = 1;
            }
            
            m_CachedTileWeights.Add(currentWeight); 
            totalWeight += currentWeight;
        }

        int randomValue = rng.Next(0, totalWeight);
        int currentSum = 0; 
        for (int i = 0; i < candidates.Count; i++) 
        { 
            currentSum += m_CachedTileWeights[i]; 
            if (randomValue < currentSum) return candidates[i]; 
        }
        return candidates[0];
    }

    void CheckNeighborType(Vector2Int targetCoord, ref int grassCount, ref int concreteCount)
    {
        MapTileData tile = GetNeighborTileData(targetCoord);
        if (tile == null) return;
        if (tile.tileType == TileType.Grass) grassCount++;
        else if (tile.tileType == TileType.Concrete) concreteCount++;
    }

    StructureItem GetWeightedRandomStructure(List<StructureItem> candidates, System.Random rng)
    {
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].weight;
        }

        int randomValue = rng.Next(0, totalWeight);
        int currentSum = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            currentSum += candidates[i].weight;
            if (randomValue < currentSum) return candidates[i];
        }
        return candidates[0];
    }

    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;
        if (!spawnedTileData.ContainsKey(targetCoord)) return -1;
        MapTileData neighbor = spawnedTileData[targetCoord];
        switch (requiredSide) 
        { 
            case "top": return (int)neighbor.top;
            case "bottom": return (int)neighbor.bottom;
            case "left": return (int)neighbor.left;
            case "right": return (int)neighbor.right;
            default: return -1; 
        }
    }
}