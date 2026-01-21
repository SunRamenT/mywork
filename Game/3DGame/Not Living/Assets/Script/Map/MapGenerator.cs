using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class StructureItem
{
    public GameObject prefab;
    [Range(1, 100)]
    public int weight = 10;
    // 建物をランダム回転させるか？（木などはtrue、ビルはfalse推奨だがお好みで）
    public bool allowRandomRotation = true; 
}

public class MapGenerator : MonoBehaviour
{
    [Header("設定")]
    public int tileSize = 20;
    public int viewDistance = 3;
    public int destroyDistance = 4;
    public Transform player;

    [Header("タイルデータ")]
    public List<MapTileData> allTiles;

    [Header("構造物データ")]
    [Tooltip("芝生の上に生成されるもの（木、岩、公園遊具など）")]
    public List<StructureItem> grassStructures;
    [Tooltip("コンクリートの上に生成されるもの（ビル、家、コンビニなど）")]
    public List<StructureItem> concreteStructures;

    // --- データ管理 ---
    // 地面データ
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    private Dictionary<Vector2Int, GameObject> spawnedObjects = new Dictionary<Vector2Int, GameObject>();

    // ▼ 建物データ（追加）: 座標に対して「どの建物データを選んだか」を記憶
    private Dictionary<Vector2Int, StructureItem> spawnedStructureData = new Dictionary<Vector2Int, StructureItem>();
    // ▼ 建物オブジェクト（追加）: 表示中の建物
    private Dictionary<Vector2Int, GameObject> spawnedStructureObjects = new Dictionary<Vector2Int, GameObject>();
    // ▼ 建物回転データ（追加）: 建物の向きも記憶しないと、戻ってきたときに回転が変わってしまう
    private Dictionary<Vector2Int, Quaternion> spawnedStructureRotations = new Dictionary<Vector2Int, Quaternion>();

    // プール管理
    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();

    private Vector2Int currentChunkCoord;

    void Start() { UpdateMap(); }

    void Update()
    {
        if (player == null) return;
        Vector2Int playerCoord = new Vector2Int(
            Mathf.RoundToInt(player.position.x / tileSize),
            Mathf.RoundToInt(player.position.z / tileSize)
        );

        if (playerCoord != currentChunkCoord)
        {
            currentChunkCoord = playerCoord;
            UpdateMap();
        }
    }

    void UpdateMap()
    {
        for (int x = currentChunkCoord.x - viewDistance; x <= currentChunkCoord.x + viewDistance; x++)
        {
            for (int y = currentChunkCoord.y - viewDistance; y <= currentChunkCoord.y + viewDistance; y++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                
                // 地面の生成
                if (!spawnedObjects.ContainsKey(coord))
                {
                    SpawnTileAt(coord);
                }
                
                // 建物の生成（地面があり、かつ建物がまだ表示されていない場合）
                if (spawnedObjects.ContainsKey(coord) && !spawnedStructureObjects.ContainsKey(coord))
                {
                    SpawnStructureAt(coord, spawnedTileData[coord]);
                }
            }
        }
        CleanupTiles();
    }

    // --- 地面の生成（変更なし） ---
    void SpawnTileAt(Vector2Int coord)
    {
        MapTileData selectedData;
        if (spawnedTileData.ContainsKey(coord)) selectedData = spawnedTileData[coord];
        else
        {
            int reqTop    = GetNeighborConnection(coord, Vector2Int.up,    "bottom");
            int reqBottom = GetNeighborConnection(coord, Vector2Int.down,  "top");
            int reqLeft   = GetNeighborConnection(coord, Vector2Int.left,  "right");
            int reqRight  = GetNeighborConnection(coord, Vector2Int.right, "left");

            var validTiles = allTiles.Where(t => 
                (reqTop    == -1 || (int)t.top    == reqTop) &&
                (reqBottom == -1 || (int)t.bottom == reqBottom) &&
                (reqLeft   == -1 || (int)t.left   == reqLeft) &&
                (reqRight  == -1 || (int)t.right  == reqRight)
            ).ToList();

            var strictTiles = validTiles.Where(t => IsPlacementValid(t, coord)).ToList();
            if (strictTiles.Count > 0) validTiles = strictTiles;
            if (validTiles.Count == 0) { if (allTiles.Count > 0) validTiles.Add(allTiles[0]); else return; }

            selectedData = GetWeightedRandomTile(validTiles, coord);
            spawnedTileData.Add(coord, selectedData);
        }

        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        GameObject obj = GetPooledObject(selectedData.prefab, pos, selectedData.prefab.transform.rotation);
        spawnedObjects.Add(coord, obj);
    }

    // --- ▼ 建物の生成（新規追加） ---
    void SpawnStructureAt(Vector2Int coord, MapTileData tileData)
    {
        // 道路には建物を置かない
        if (tileData.tileType != TileType.Grass && tileData.tileType != TileType.Concrete) return;

        StructureItem selectedStructure = null;
        Quaternion rotation = Quaternion.identity;

        // 1. データがあるか確認（再訪時）
        if (spawnedStructureData.ContainsKey(coord))
        {
            selectedStructure = spawnedStructureData[coord];
            // 何も置かないことになっていれば終了
            if (selectedStructure == null) return; 
            
            // 回転情報も復元
            if (spawnedStructureRotations.ContainsKey(coord)) rotation = spawnedStructureRotations[coord];
        }
        else
        {
            // 2. 新規抽選
            List<StructureItem> candidates = null;

            if (tileData.tileType == TileType.Grass) candidates = grassStructures;
            else if (tileData.tileType == TileType.Concrete) candidates = concreteStructures;

            // 候補がなければ何もしない
            if (candidates == null || candidates.Count == 0) return;

            // 重み抽選
            selectedStructure = GetWeightedRandomStructure(candidates);
            
            // 抽選結果を保存（nullなら「空き地」として保存）
            spawnedStructureData.Add(coord, selectedStructure);
            
            if (selectedStructure != null)
            {
                // 回転を決める（90度刻み）
                if (selectedStructure.allowRandomRotation)
                {
                    float yAngle = Random.Range(0, 4) * 90f;
                    rotation = Quaternion.Euler(0, yAngle, 0);
                }
                spawnedStructureRotations.Add(coord, rotation);
            }
        }

        // 3. オブジェクト生成（プールから）
        if (selectedStructure != null && selectedStructure.prefab != null)
        {
            Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
            GameObject obj = GetPooledObject(selectedStructure.prefab, pos, rotation);
            spawnedStructureObjects.Add(coord, obj);
        }
    }

    // --- クリーンアップ（修正） ---
    void CleanupTiles()
    {
        List<Vector2Int> tilesToRemove = new List<Vector2Int>();

        // 1. 地面の削除
        foreach (var item in spawnedObjects)
        {
            if (Vector2.Distance(item.Key, currentChunkCoord) > destroyDistance)
            {
                GameObject prefabKey = spawnedTileData[item.Key].prefab;
                ReturnToPool(item.Value, prefabKey);
                tilesToRemove.Add(item.Key);
            }
        }
        foreach (var coord in tilesToRemove) spawnedObjects.Remove(coord);

        // 2. 建物の削除（地面と同じロジック）
        List<Vector2Int> structuresToRemove = new List<Vector2Int>();
        foreach (var item in spawnedStructureObjects)
        {
            if (Vector2.Distance(item.Key, currentChunkCoord) > destroyDistance)
            {
                // 建物のPrefabキーを取得してプールに戻す
                if (spawnedStructureData.TryGetValue(item.Key, out StructureItem data) && data != null)
                {
                    ReturnToPool(item.Value, data.prefab);
                }
                structuresToRemove.Add(item.Key);
            }
        }
        foreach (var coord in structuresToRemove) spawnedStructureObjects.Remove(coord);
    }

    // --- ヘルパーメソッド ---

    StructureItem GetWeightedRandomStructure(List<StructureItem> candidates)
    {
        int totalWeight = candidates.Sum(x => x.weight);
        int randomValue = Random.Range(0, totalWeight);
        int currentSum = 0;
        foreach (var item in candidates)
        {
            currentSum += item.weight;
            if (randomValue < currentSum) return item;
        }
        return candidates[0];
    }
    
    // (以下のメソッドは前回と同じ: IsPlacementValid, GetNeighborTileData, IsType, GetWeightedRandomTile, GetPooledObject, ReturnToPool, GetNeighborConnection)
    // ※長くなるので省略していませんが、前回までのコードを維持してください。
    // ※GetWeightedRandomTile内で使っているCheckNeighborTypeなども必要です。
    // ※GetNeighborTileDataなども必要です。

    // ↓↓↓ 前回から変更なしのメソッド群（コピペ用） ↓↓↓
    // ▼▼▼ 修正版: 交差点の連続を防ぐルール ▼▼▼
    bool IsPlacementValid(MapTileData candidate, Vector2Int coord)
    {
        MapTileData top    = GetNeighborTileData(coord + Vector2Int.up);
        MapTileData bottom = GetNeighborTileData(coord + Vector2Int.down);
        MapTileData left   = GetNeighborTileData(coord + Vector2Int.left);
        MapTileData right  = GetNeighborTileData(coord + Vector2Int.right);

        // --- ルールA: 交差点（十字・T字）は連続させない ---
        // 候補が「十字路」または「T字路」の場合...
        if (candidate.tileType == TileType.Cross || candidate.tileType == TileType.T_Junction)
        {
            // 上下左右のいずれかが「十字路」または「T字路」なら配置NG（＝ここは直線かカーブにする）
            if (IsBusyJunction(top) || IsBusyJunction(bottom) || IsBusyJunction(left) || IsBusyJunction(right))
            {
                return false;
            }
        }

        // --- ルールB: 行き止まりの連続禁止（変更なし） ---
        if (candidate.tileType == TileType.DeadEnd)
        {
             if (IsType(top, TileType.DeadEnd) || IsType(bottom, TileType.DeadEnd) ||
                 IsType(left, TileType.DeadEnd) || IsType(right, TileType.DeadEnd))
             {
                 return false;
             }
        }

        return true;
    }

    // ヘルパー: 交差点かどうか判定
    bool IsBusyJunction(MapTileData data)
    {
        return data != null && (data.tileType == TileType.Cross || data.tileType == TileType.T_Junction);
    }
    bool IsType(MapTileData data, TileType type) { return data != null && data.tileType == type; }
    MapTileData GetNeighborTileData(Vector2Int coord) 
    { 
        if (spawnedTileData.ContainsKey(coord)) 
            return spawnedTileData[coord]; 
        return null; 
    }
    MapTileData GetWeightedRandomTile(List<MapTileData> candidates, Vector2Int coord)
    {
        int grassNeighbors = 0; int concreteNeighbors = 0;
        CheckNeighborType(coord + Vector2Int.up, ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.down, ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.left, ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.right, ref grassNeighbors, ref concreteNeighbors);
        
        Dictionary<MapTileData, int> dynamicWeights = new Dictionary<MapTileData, int>();
        int totalWeight = 0;
        foreach (var tile in candidates)
        {
            int currentWeight = tile.weight;
            if (tile.tileType == TileType.Grass && grassNeighbors > 0) currentWeight *= (grassNeighbors * 4);
            else if (tile.tileType == TileType.Concrete && concreteNeighbors > 0) currentWeight *= (concreteNeighbors * 4);
            if (tile.tileType == TileType.Grass && concreteNeighbors > grassNeighbors) { currentWeight /= 2; if (currentWeight < 1) currentWeight = 1; }
            if (tile.tileType == TileType.Concrete && grassNeighbors > concreteNeighbors) { currentWeight /= 2; if (currentWeight < 1) currentWeight = 1; }
            dynamicWeights.Add(tile, currentWeight);
            totalWeight += currentWeight;
        }
        int randomValue = Random.Range(0, totalWeight);
        int currentSum = 0;
        foreach (var kvp in dynamicWeights) { currentSum += kvp.Value; if (randomValue < currentSum) return kvp.Key; }
        return candidates[0];
    }
    void CheckNeighborType(Vector2Int targetCoord, ref int grassCount, ref int concreteCount)
    {
        MapTileData tile = GetNeighborTileData(targetCoord);
        if (tile != null) { if (tile.tileType == TileType.Grass) grassCount++; if (tile.tileType == TileType.Concrete) concreteCount++; }
    }
    GameObject GetPooledObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(prefab)) poolDictionary.Add(prefab, new Queue<GameObject>());
        if (poolDictionary[prefab].Count > 0) { GameObject obj = poolDictionary[prefab].Dequeue(); obj.transform.position = position; obj.transform.rotation = rotation; obj.SetActive(true); return obj; }
        else { GameObject obj = Instantiate(prefab, position, rotation, transform); obj.name = prefab.name + "(Pooled)"; return obj; }
    }
    void ReturnToPool(GameObject obj, GameObject prefabKey) { obj.SetActive(false); if (poolDictionary.ContainsKey(prefabKey)) poolDictionary[prefabKey].Enqueue(obj); else Destroy(obj); }
    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;
        if (!spawnedTileData.ContainsKey(targetCoord)) return -1;
        MapTileData neighbor = spawnedTileData[targetCoord];
        switch (requiredSide) { case "top": return (int)neighbor.top; case "bottom": return (int)neighbor.bottom; case "left": return (int)neighbor.left; case "right": return (int)neighbor.right; default: return -1; }
    }
}