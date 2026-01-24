using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class StructureItem
{
    public GameObject prefab;
    [Range(1, 100)]
    public int weight = 10;
    // 建物をランダム回転させるか？（木などはtrue、ビルはfalse推奨）
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
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    private Dictionary<Vector2Int, GameObject> spawnedObjects = new Dictionary<Vector2Int, GameObject>();

    // 建物データ
    private Dictionary<Vector2Int, StructureItem> spawnedStructureData = new Dictionary<Vector2Int, StructureItem>();
    private Dictionary<Vector2Int, GameObject> spawnedStructureObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, Quaternion> spawnedStructureRotations = new Dictionary<Vector2Int, Quaternion>();

    // プール管理
    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();

    private Vector2Int currentChunkCoord;

    [Header("初期エリア設定")]
    [Tooltip("初期スポーン地点から半径何マスを除外するか（1なら3x3、2なら5x5）")]
    public int safeZoneRadius = 1;

    void Start() 
    { 
        UpdateMap(); 
    }

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

                // セーフゾーン判定
                // 原点(0,0)からの距離が safeZoneRadius 以内なら生成しない
                // これにより、(-1, -1) ～ (1, 1) の範囲（3x3）がスキップされる
                if (Mathf.Abs(coord.x) <= safeZoneRadius && Mathf.Abs(coord.y) <= safeZoneRadius)
                {
                    continue; 
                }
                
                // 地面の生成
                if (!spawnedObjects.ContainsKey(coord))
                {
                    SpawnTileAt(coord);
                }
                
                // 建物の生成
                if (spawnedObjects.ContainsKey(coord) && !spawnedStructureObjects.ContainsKey(coord))
                {
                    SpawnStructureAt(coord, spawnedTileData[coord]);
                }
            }
        }
        CleanupTiles();
    }

    // --- 地面の生成 ---
    void SpawnTileAt(Vector2Int coord)
    {
        MapTileData selectedData;

        // 1. 既にデータがある場合は再利用（記憶の復元）
        if (spawnedTileData.ContainsKey(coord)) 
        {
            selectedData = spawnedTileData[coord];
        }
        else
        {
            // 2. 周囲の接続タイプを確認
            int reqTop    = GetNeighborConnection(coord, Vector2Int.up,    "bottom");
            int reqBottom = GetNeighborConnection(coord, Vector2Int.down,  "top");
            int reqLeft   = GetNeighborConnection(coord, Vector2Int.left,  "right");
            int reqRight  = GetNeighborConnection(coord, Vector2Int.right, "left");

            // 3. 接続条件（物理的に繋がるか）でフィルタリング
            var validTiles = allTiles.Where(t => 
                (reqTop    == -1 || (int)t.top    == reqTop) &&
                (reqBottom == -1 || (int)t.bottom == reqBottom) &&
                (reqLeft   == -1 || (int)t.left   == reqLeft) &&
                (reqRight  == -1 || (int)t.right  == reqRight)
            ).ToList();

            // 4. 配置ルール（十字路の隣はダメなど）でさらにフィルタリング
            var strictTiles = validTiles.Where(t => IsPlacementValid(t, coord)).ToList();

            // ルールを守れる候補があるなら、そちらを優先する
            if (strictTiles.Count > 0) 
            {
                validTiles = strictTiles;
            }
            else
            {
                //Debug.LogWarning($"座標 {coord} でルール適合タイルなし。接続可能なタイルのみで抽選します。");
            }

            // 5. 候補が一つもない場合の最終手段（エラー回避）
            if (validTiles.Count == 0) 
            { 
                if (allTiles.Count > 0) 
                {
                    // ここがCrossだと、エラー時にCrossが強制配置されてしまいます。
                    validTiles.Add(allTiles[0]); 
                }
                else return; 
            }

            // 6. 重みづけ抽選で決定
            selectedData = GetWeightedRandomTile(validTiles, coord);
            spawnedTileData.Add(coord, selectedData);
        }

        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        GameObject obj = GetPooledObject(selectedData.prefab, pos, selectedData.prefab.transform.rotation);
        spawnedObjects.Add(coord, obj);
    }

    // --- 建物の生成 ---
    void SpawnStructureAt(Vector2Int coord, MapTileData tileData)
    {
        // 道路には建物を置かない
        if (tileData.tileType != TileType.Grass && tileData.tileType != TileType.Concrete) return;

        StructureItem selectedStructure = null;
        Quaternion rotation = Quaternion.identity;

        if (spawnedStructureData.ContainsKey(coord))
        {
            selectedStructure = spawnedStructureData[coord];
            if (selectedStructure == null) return; 
            
            if (spawnedStructureRotations.ContainsKey(coord)) 
                rotation = spawnedStructureRotations[coord];
        }
        else
        {
            List<StructureItem> candidates = null;

            if (tileData.tileType == TileType.Grass) candidates = grassStructures;
            else if (tileData.tileType == TileType.Concrete) candidates = concreteStructures;

            if (candidates == null || candidates.Count == 0) return;

            selectedStructure = GetWeightedRandomStructure(candidates);
            spawnedStructureData.Add(coord, selectedStructure);
            
            if (selectedStructure != null)
            {
                if (selectedStructure.allowRandomRotation)
                {
                    float yAngle = Random.Range(0, 4) * 90f;
                    rotation = Quaternion.Euler(0, yAngle, 0);
                }
                spawnedStructureRotations.Add(coord, rotation);
            }
        }

        if (selectedStructure != null && selectedStructure.prefab != null)
        {
            Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
            GameObject obj = GetPooledObject(selectedStructure.prefab, pos, rotation);
            spawnedStructureObjects.Add(coord, obj);
        }
    }

    // --- クリーンアップ ---
    void CleanupTiles()
    {
        List<Vector2Int> tilesToRemove = new List<Vector2Int>();

        // 地面の削除
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

        // 建物の削除
        List<Vector2Int> structuresToRemove = new List<Vector2Int>();
        foreach (var item in spawnedStructureObjects)
        {
            if (Vector2.Distance(item.Key, currentChunkCoord) > destroyDistance)
            {
                if (spawnedStructureData.TryGetValue(item.Key, out StructureItem data) && data != null)
                {
                    ReturnToPool(item.Value, data.prefab);
                }
                structuresToRemove.Add(item.Key);
            }
        }
        foreach (var coord in structuresToRemove) spawnedStructureObjects.Remove(coord);
    }

    // =========================================================
    //   ヘルパーメソッド (可読性向上のため展開済み)
    // =========================================================

    // --- ルール判定: 配置しても大丈夫か？ ---
    bool IsPlacementValid(MapTileData candidate, Vector2Int coord)
    {
        MapTileData top    = GetNeighborTileData(coord + Vector2Int.up);
        MapTileData bottom = GetNeighborTileData(coord + Vector2Int.down);
        MapTileData left   = GetNeighborTileData(coord + Vector2Int.left);
        MapTileData right  = GetNeighborTileData(coord + Vector2Int.right);

        // ルールA: 十字路(Cross)やT字路(T_Junction)は連続させない
        if (candidate.tileType == TileType.Cross || candidate.tileType == TileType.T_Junction)
        {
            // 上下左右に既に「混雑した交差点」がある場合はNG
            if (IsBusyJunction(top) || IsBusyJunction(bottom) || IsBusyJunction(left) || IsBusyJunction(right))
            {
                return false;
            }
        }

        // ルールB: 行き止まりの連続禁止
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

    // 交差点かどうか判定
    bool IsBusyJunction(MapTileData data)
    {
        return data != null && (data.tileType == TileType.Cross || data.tileType == TileType.T_Junction);
    }

    // 指定したタイプかどうか判定
    bool IsType(MapTileData data, TileType type) 
    { 
        return data != null && data.tileType == type; 
    }

    // 指定座標のタイルデータを取得 (生成済みの場合のみ)
    MapTileData GetNeighborTileData(Vector2Int coord) 
    { 
        if (spawnedTileData.ContainsKey(coord)) 
        {
            return spawnedTileData[coord]; 
        }
        return null; 
    }

    // 重みづけ抽選（周囲の環境によって確率を変える）
    MapTileData GetWeightedRandomTile(List<MapTileData> candidates, Vector2Int coord)
    {
        int grassNeighbors = 0; 
        int concreteNeighbors = 0;

        // 周囲4方向の環境を調査
        CheckNeighborType(coord + Vector2Int.up,    ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.down,  ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.left,  ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.right, ref grassNeighbors, ref concreteNeighbors);
        
        Dictionary<MapTileData, int> dynamicWeights = new Dictionary<MapTileData, int>();
        int totalWeight = 0;

        // 重み計算
        foreach (var tile in candidates)
        {
            int currentWeight = tile.weight;

            // 芝生の隣には芝生ができやすくする
            if (tile.tileType == TileType.Grass && grassNeighbors > 0) 
            {
                currentWeight *= (grassNeighbors * 4);
            }
            // コンクリートの隣にはコンクリートができやすくする
            else if (tile.tileType == TileType.Concrete && concreteNeighbors > 0) 
            {
                currentWeight *= (concreteNeighbors * 4);
            }

            // 異物が混ざりにくくする（コンクリートの中に芝生など）
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

            dynamicWeights.Add(tile, currentWeight);
            totalWeight += currentWeight;
        }

        // 抽選実行
        int randomValue = Random.Range(0, totalWeight);
        int currentSum = 0;

        foreach (var kvp in dynamicWeights) 
        { 
            currentSum += kvp.Value; 
            if (randomValue < currentSum) 
            {
                return kvp.Key; 
            }
        }

        return candidates[0];
    }

    // 指定座標のタイルの種類をカウントするヘルパー
    void CheckNeighborType(Vector2Int targetCoord, ref int grassCount, ref int concreteCount)
    {
        MapTileData tile = GetNeighborTileData(targetCoord);
        if (tile != null) 
        { 
            if (tile.tileType == TileType.Grass) grassCount++; 
            if (tile.tileType == TileType.Concrete) concreteCount++; 
        }
    }

    // 構造物の重み抽選
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

    // オブジェクトプールから取得
    GameObject GetPooledObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(prefab)) 
        {
            poolDictionary.Add(prefab, new Queue<GameObject>());
        }

        if (poolDictionary[prefab].Count > 0) 
        { 
            GameObject obj = poolDictionary[prefab].Dequeue(); 
            obj.transform.position = position; 
            obj.transform.rotation = rotation; 
            obj.SetActive(true); 
            return obj; 
        }
        else 
        { 
            GameObject obj = Instantiate(prefab, position, rotation, transform); 
            obj.name = prefab.name + "(Pooled)"; 
            return obj; 
        }
    }

    // オブジェクトプールへ返却
    void ReturnToPool(GameObject obj, GameObject prefabKey) 
    { 
        obj.SetActive(false); 
        if (poolDictionary.ContainsKey(prefabKey)) 
        {
            poolDictionary[prefabKey].Enqueue(obj); 
        }
        else 
        {
            Destroy(obj); 
        }
    }

    // 指定方向の接続タイプを取得
    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;
        
        if (!spawnedTileData.ContainsKey(targetCoord)) 
        {
            return -1; // 接続なし（何でもOK）
        }
        
        MapTileData neighbor = spawnedTileData[targetCoord];
        
        switch (requiredSide) 
        { 
            case "top":    return (int)neighbor.top; 
            case "bottom": return (int)neighbor.bottom; 
            case "left":   return (int)neighbor.left; 
            case "right":  return (int)neighbor.right; 
            default:       return -1; 
        }
    }
}