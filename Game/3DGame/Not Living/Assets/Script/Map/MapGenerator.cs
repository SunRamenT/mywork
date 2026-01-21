using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    [Header("設定")]
    public int tileSize = 20;
    public int viewDistance = 3;
    public int destroyDistance = 4;
    public Transform player;

    [Header("タイルデータ")]
    public List<MapTileData> allTiles;

    // データ管理
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    private Dictionary<Vector2Int, GameObject> spawnedObjects = new Dictionary<Vector2Int, GameObject>();
    
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
                if (!spawnedObjects.ContainsKey(coord))
                {
                    SpawnTileAt(coord);
                }
            }
        }
        CleanupTiles();
    }

    void SpawnTileAt(Vector2Int coord)
    {
        MapTileData selectedData;

        if (spawnedTileData.ContainsKey(coord))
        {
            selectedData = spawnedTileData[coord];
        }
        else
        {
            // --- 1. 接続条件のチェック ---
            int reqTop    = GetNeighborConnection(coord, Vector2Int.up,    "bottom");
            int reqBottom = GetNeighborConnection(coord, Vector2Int.down,  "top");
            int reqLeft   = GetNeighborConnection(coord, Vector2Int.left,  "right");
            int reqRight  = GetNeighborConnection(coord, Vector2Int.right, "left");

            // 接続的にOKなタイルをリストアップ
            var validTiles = allTiles.Where(t => 
                (reqTop    == -1 || (int)t.top    == reqTop) &&
                (reqBottom == -1 || (int)t.bottom == reqBottom) &&
                (reqLeft   == -1 || (int)t.left   == reqLeft) &&
                (reqRight  == -1 || (int)t.right  == reqRight)
            ).ToList();

            // --- 2. 高度なルールの適用（フィルタリング） ---
            // 追加ルールに違反するタイルを除外する
            var strictTiles = validTiles.Where(t => IsPlacementValid(t, coord)).ToList();

            // もしルールが厳しすぎて候補がゼロになったら、緩和して元のリストを使う
            if (strictTiles.Count > 0)
            {
                validTiles = strictTiles;
            }

            // フォールバック（接続できるものが無い場合）
            if (validTiles.Count == 0)
            {
                if (allTiles.Count > 0) validTiles.Add(allTiles[0]);
                else return;
            }

            // --- 3. 重みづけ抽選 ---
            selectedData = GetWeightedRandomTile(validTiles, coord);
            
            spawnedTileData.Add(coord, selectedData);
        }

        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        GameObject obj = GetPooledObject(selectedData.prefab, pos, selectedData.prefab.transform.rotation);
        spawnedObjects.Add(coord, obj);
    }

    // ▼▼▼ ルール判定メソッド ▼▼▼
    bool IsPlacementValid(MapTileData candidate, Vector2Int coord)
    {
        // 周囲のタイルデータを取得
        MapTileData topTile    = GetNeighborTileData(coord + Vector2Int.up);
        MapTileData bottomTile = GetNeighborTileData(coord + Vector2Int.down);
        MapTileData leftTile   = GetNeighborTileData(coord + Vector2Int.left);
        MapTileData rightTile  = GetNeighborTileData(coord + Vector2Int.right);

        // ルールA: 十字路の隣に十字路を置かない（クドいから）
        if (candidate.tileType == TileType.Cross)
        {
            if (IsType(topTile, TileType.Cross) || IsType(bottomTile, TileType.Cross) ||
                IsType(leftTile, TileType.Cross) || IsType(rightTile, TileType.Cross))
            {
                return false;
            }
        }

        // ルールB: 行き止まりの隣に行き止まりを置かない（移動不能になるから）
        if (candidate.tileType == TileType.DeadEnd)
        {
             // 接続チェックで弾かれているはずだが、念の為のルール
             if (IsType(topTile, TileType.DeadEnd) || IsType(bottomTile, TileType.DeadEnd) ||
                 IsType(leftTile, TileType.DeadEnd) || IsType(rightTile, TileType.DeadEnd))
             {
                 return false;
             }
        }

        return true;
    }

    // ヘルパー: タイルタイプが一致するか（nullチェック付き）
    bool IsType(MapTileData data, TileType type)
    {
        return data != null && data.tileType == type;
    }

    // ▼▼▼ 重みづけ抽選メソッド ▼▼▼
    MapTileData GetWeightedRandomTile(List<MapTileData> candidates, Vector2Int coord)
    {
        // 追加ルールC: 芝生(Ground)の隣は、芝生が出やすくなる（公園のように広がる）
        // 候補リストをコピーして重みを変動させる
        List<MapTileData> dynamicCandidates = new List<MapTileData>(candidates);

        int grassNeighbors = 0;
        if (IsType(GetNeighborTileData(coord + Vector2Int.up), TileType.Ground)) grassNeighbors++;
        if (IsType(GetNeighborTileData(coord + Vector2Int.down), TileType.Ground)) grassNeighbors++;
        if (IsType(GetNeighborTileData(coord + Vector2Int.left), TileType.Ground)) grassNeighbors++;
        if (IsType(GetNeighborTileData(coord + Vector2Int.right), TileType.Ground)) grassNeighbors++;

        // 総重量を計算
        int totalWeight = 0;
        foreach (var tile in candidates)
        {
            int currentWeight = tile.weight;
            
            // 芝生ボーナス: 周囲に芝生が多いほど、このタイルが芝生なら重みを倍増させる
            if (tile.tileType == TileType.Ground && grassNeighbors > 0)
            {
                currentWeight *= (grassNeighbors * 2); 
            }

            totalWeight += currentWeight;
        }

        // 抽選
        int randomValue = Random.Range(0, totalWeight);
        int currentSum = 0;

        foreach (var tile in candidates)
        {
            int currentWeight = tile.weight;
            // 計算時と同じボーナスを適用
            if (tile.tileType == TileType.Ground && grassNeighbors > 0)
            {
                currentWeight *= (grassNeighbors * 2);
            }

            currentSum += currentWeight;
            if (randomValue < currentSum)
            {
                return tile;
            }
        }

        return candidates[0]; // 万が一のためのフォールバック
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // --- 以下、既存のヘルパーメソッド ---

    MapTileData GetNeighborTileData(Vector2Int coord)
    {
        if (spawnedTileData.ContainsKey(coord)) return spawnedTileData[coord];
        return null;
    }

    void CleanupTiles()
    {
        List<Vector2Int> tilesToRemove = new List<Vector2Int>();
        foreach (var item in spawnedObjects)
        {
            float dist = Vector2.Distance(item.Key, currentChunkCoord);
            if (dist > destroyDistance)
            {
                GameObject prefabKey = spawnedTileData[item.Key].prefab;
                ReturnToPool(item.Value, prefabKey);
                tilesToRemove.Add(item.Key);
            }
        }
        foreach (var coord in tilesToRemove) spawnedObjects.Remove(coord);
    }

    GameObject GetPooledObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(prefab)) poolDictionary.Add(prefab, new Queue<GameObject>());
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

    void ReturnToPool(GameObject obj, GameObject prefabKey)
    {
        obj.SetActive(false);
        if (poolDictionary.ContainsKey(prefabKey)) poolDictionary[prefabKey].Enqueue(obj);
        else Destroy(obj);
    }

    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;
        if (!spawnedTileData.ContainsKey(targetCoord)) return -1;
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