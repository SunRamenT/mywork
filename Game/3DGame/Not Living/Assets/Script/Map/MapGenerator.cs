using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Unity.AI.Navigation; // NavMeshModifierRegistryを使うために必要

[System.Serializable]
public class StructureItem
{
    public GameObject prefab;
    [Range(1, 100)]
    public int weight = 10;
    // 建物をランダム回転させるか？（木などはtrue、ビルはfalse推奨）
    public bool allowRandomRotation = true; 
}

// ★追加: NavMesh Modifierのキャッシュ管理クラス（静的）
// これにより、InfiniteNavMeshBuilderでのFindObjectsByType(全探索)を回避する
public static class NavMeshModifierRegistry
{
    private static readonly HashSet<NavMeshModifierVolume> _modifiers = new HashSet<NavMeshModifierVolume>();
    public static IEnumerable<NavMeshModifierVolume> ActiveModifiers => _modifiers;

    public static void Register(NavMeshModifierVolume mod) { if (mod != null) _modifiers.Add(mod); }
    public static void Unregister(NavMeshModifierVolume mod) { if (mod != null) _modifiers.Remove(mod); }
}

public class MapGenerator : MonoBehaviour
{
    [Header("設定")]
    public int tileSize = 20;
    
    [Tooltip("一度に読み込むチャンクの「半径」。3なら周囲3チャンク分(約300m)。")]
    [Range(1, 5)] // 安全のため最大5に制限
    public int viewDistance = 3; 
    
    public Transform player;

    [Header("シード値")]
    [Tooltip("この値と座標が変わらなければ、常に同じマップが生成されます")]
    public int worldSeed = 12345;

    [Header("タイルデータ")]
    public List<MapTileData> allTiles;

    [Header("構造物データ")]
    public List<StructureItem> grassStructures;
    public List<StructureItem> concreteStructures;

    [Header("初期エリア設定")]
    [Tooltip("初期スポーン地点から半径何マスを除外するか")]
    public int safeZoneRadius = 1;

    [Header("最適化設定")]
    public int chunkSize = 5;

    // --- データ管理 ---
    // 生成済みの地形データを一時的に保持する辞書
    // ただし、CleanupChunksで範囲外に出たものは削除されるため、メモリは肥大化しない
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    private Dictionary<Vector2Int, StructureItem> spawnedStructureData = new Dictionary<Vector2Int, StructureItem>();
    private Dictionary<Vector2Int, Quaternion> spawnedStructureRotations = new Dictionary<Vector2Int, Quaternion>();

    // チャンク管理（生成済みフラグ）
    private HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
    
    // オブジェクト管理（チャンク座標 -> そのチャンクに含まれる全オブジェクトのリスト）
    private Dictionary<Vector2Int, List<GameObject>> chunkObjects = new Dictionary<Vector2Int, List<GameObject>>();

    // プール管理
    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    // 生成済みオブジェクトの元のPrefabを覚えておくための辞書
    private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

    private Vector2Int currentChunkCoord;
    private bool isUpdatingMap = false;

    // 座標 (x, y) と worldSeed から、一意の乱数シードを生成するハッシュ関数
    // (擬似乱数の決定論的な生成)
    int GetCoordinateSeed(Vector2Int coord)
    {
        // 大きな素数を使ってビット演算し、値を散らばらせる
        // uncheckedブロックでオーバーフローを許容する
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

        // チャンクが変わった時だけ更新処理を走らせる
        if (playerChunkCoord != currentChunkCoord && !isUpdatingMap)
        {
            currentChunkCoord = playerChunkCoord;
            StartCoroutine(UpdateMapCoroutine(false));
        }
    }
    
    // Inspectorの値が変更された時に安全な値に補正する
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

    // マップ更新コルーチン (Time Slicing)
    IEnumerator UpdateMapCoroutine(bool isImmediate)
    {
        isUpdatingMap = true;

        List<Vector2Int> chunksToSpawn = new List<Vector2Int>();
        int safeViewDistance = Mathf.Min(viewDistance, 5); // 安全装置

        for (int x = currentChunkCoord.x - safeViewDistance; x <= currentChunkCoord.x + safeViewDistance; x++)
        {
            for (int y = currentChunkCoord.y - safeViewDistance; y <= currentChunkCoord.y + safeViewDistance; y++)
            {
                Vector2Int targetChunkCoord = new Vector2Int(x, y);
                if (!loadedChunks.Contains(targetChunkCoord))
                {
                    chunksToSpawn.Add(targetChunkCoord);
                }
            }
        }

        // プレイヤーに近い順にソート（足元から生成）
        chunksToSpawn.Sort((a, b) => 
            Vector2Int.Distance(a, currentChunkCoord).CompareTo(Vector2Int.Distance(b, currentChunkCoord))
        );

        // 生成実行
        int processCount = 0;
        foreach (var chunkCoord in chunksToSpawn)
        {
            CreateChunk(chunkCoord);
            loadedChunks.Add(chunkCoord);
            processCount++;

            // 負荷分散：即時モードでなければ1チャンクごとに1フレーム休む
            if (!isImmediate) yield return null;
        }

        // 削除（プール返却）実行
        CleanupChunks();

        isUpdatingMap = false;
    }

    void CreateChunk(Vector2Int chunkCoord)
    {
        // このチャンクに属するオブジェクトリストを初期化
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

                // セーフゾーン（開始地点周辺）は生成しない
                if (Mathf.Abs(tileCoord.x) <= safeZoneRadius && Mathf.Abs(tileCoord.y) <= safeZoneRadius)
                    continue;

                SpawnTileAt(tileCoord, chunkCoord);
                
                // 地面生成に成功していれば建物も試行
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
        
        // メモリにある場合はそれを使う（優先）
        if (spawnedTileData.ContainsKey(coord)) 
        {
            selectedData = spawnedTileData[coord];
        }
        else
        {
            // メモリになくても、シード値から「あるべきタイル」を再計算する（真の無限）
            // この座標専用の乱数生成器を作成
            System.Random tileRng = new System.Random(GetCoordinateSeed(coord));

            // --- 抽選ロジック ---
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

            if (validTiles.Count == 0) 
            { 
                if (allTiles.Count > 0) validTiles.Add(allTiles[0]); 
                else return; 
            }

            // System.Random を渡して決定論的に抽選
            selectedData = GetWeightedRandomTile(validTiles, coord, tileRng);
            spawnedTileData.Add(coord, selectedData);
        }

        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        
        // プールから取得
        GameObject obj = GetPooledObject(selectedData.prefab, pos, selectedData.prefab.transform.rotation);
        
        // リスト管理に追加
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
            // シード値から再計算
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
                    // 回転もシード値で固定
                    float yAngle = structRng.Next(0, 4) * 90f;
                    rotation = Quaternion.Euler(0, yAngle, 0);
                }
                spawnedStructureRotations.Add(coord, rotation);
            }
        }

        if (selectedStructure != null && selectedStructure.prefab != null)
        {
            Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
            
            // プールから取得
            GameObject obj = GetPooledObject(selectedStructure.prefab, pos, rotation);
            chunkObjects[chunkCoord].Add(obj);
        }
    }

    // --- 削除ロジック（プール返却 ＆ データ消去） ---
    void CleanupChunks()
    {
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        
        // 生成範囲より外側 1チャンク分をバッファとする
        int keepThreshold = viewDistance + 1;

        foreach (var chunkCoord in loadedChunks)
        {
            // ★幾何学的矛盾の解消：チェビシェフ距離 (L∞ノルム) で判定
            int dx = Mathf.Abs(chunkCoord.x - currentChunkCoord.x);
            int dy = Mathf.Abs(chunkCoord.y - currentChunkCoord.y);
            int chebyshevDistance = Mathf.Max(dx, dy);

            if (chebyshevDistance > keepThreshold)
            {
                chunksToRemove.Add(chunkCoord);
            }
        }

        foreach (var coord in chunksToRemove)
        {
            if (chunkObjects.ContainsKey(coord))
            {
                // そのチャンクにある全オブジェクトをプールに返す
                foreach (var obj in chunkObjects[coord])
                {
                    if (obj == null) continue;

                    // ★NavMeshレジストリから登録解除
                    var mod = obj.GetComponent<NavMeshModifierVolume>();
                    if (mod != null) NavMeshModifierRegistry.Unregister(mod);
                    
                    ReturnToPool(obj); 
                }
                chunkObjects[coord].Clear();
                chunkObjects.Remove(coord);
            }

            // ★重要：辞書（記憶）からも消去してメモリリークを防ぐ
            // 次に来たときは GetCoordinateSeed で同じ結果が再計算される
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

    // --- オブジェクトプール実装 ---
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

        // ★NavMeshレジストリへ登録
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
            
            if (poolDictionary.ContainsKey(prefabKey)) 
            {
                poolDictionary[prefabKey].Enqueue(obj); 
            }
            else 
            {
                Destroy(obj); 
            }
        }
        else
        {
            Destroy(obj);
        }
    }

    // --- ヘルパーメソッド（System.Random 対応版） ---
    
    bool IsPlacementValid(MapTileData candidate, Vector2Int coord)
    {
        MapTileData top    = GetNeighborTileData(coord + Vector2Int.up);
        MapTileData bottom = GetNeighborTileData(coord + Vector2Int.down);
        MapTileData left   = GetNeighborTileData(coord + Vector2Int.left);
        MapTileData right  = GetNeighborTileData(coord + Vector2Int.right);
        if (candidate.tileType == TileType.Cross || candidate.tileType == TileType.T_Junction) { if (IsBusyJunction(top) || IsBusyJunction(bottom) || IsBusyJunction(left) || IsBusyJunction(right)) return false; }
        if (candidate.tileType == TileType.DeadEnd) { if (IsType(top, TileType.DeadEnd) || IsType(bottom, TileType.DeadEnd) || IsType(left, TileType.DeadEnd) || IsType(right, TileType.DeadEnd)) return false; }
        return true;
    }
    bool IsBusyJunction(MapTileData data) { return data != null && (data.tileType == TileType.Cross || data.tileType == TileType.T_Junction); }
    bool IsType(MapTileData data, TileType type) { return data != null && data.tileType == type; }
    MapTileData GetNeighborTileData(Vector2Int coord) { return spawnedTileData.ContainsKey(coord) ? spawnedTileData[coord] : null; }

    // System.Random を受け取るように変更
    MapTileData GetWeightedRandomTile(List<MapTileData> candidates, Vector2Int coord, System.Random rng)
    {
        int grassNeighbors = 0; int concreteNeighbors = 0;
        CheckNeighborType(coord + Vector2Int.up,    ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.down,  ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.left,  ref grassNeighbors, ref concreteNeighbors);
        CheckNeighborType(coord + Vector2Int.right, ref grassNeighbors, ref concreteNeighbors);
        
        Dictionary<MapTileData, int> dynamicWeights = new Dictionary<MapTileData, int>();
        int totalWeight = 0;
        foreach (var tile in candidates) {
            int currentWeight = tile.weight;
            if (tile.tileType == TileType.Grass && grassNeighbors > 0) currentWeight *= (grassNeighbors * 4);
            else if (tile.tileType == TileType.Concrete && concreteNeighbors > 0) currentWeight *= (concreteNeighbors * 4);
            if (tile.tileType == TileType.Grass && concreteNeighbors > grassNeighbors) { currentWeight /= 2; if (currentWeight < 1) currentWeight = 1; }
            if (tile.tileType == TileType.Concrete && grassNeighbors > concreteNeighbors) { currentWeight /= 2; if (currentWeight < 1) currentWeight = 1; }
            dynamicWeights.Add(tile, currentWeight); totalWeight += currentWeight;
        }

        // rng.Next を使用
        int randomValue = rng.Next(0, totalWeight);
        int currentSum = 0; 
        foreach (var kvp in dynamicWeights) { currentSum += kvp.Value; if (randomValue < currentSum) return kvp.Key; }
        return candidates[0];
    }

    void CheckNeighborType(Vector2Int targetCoord, ref int grassCount, ref int concreteCount)
    {
        MapTileData tile = GetNeighborTileData(targetCoord);
        if (tile == null) return;
        if (tile.tileType == TileType.Grass) grassCount++;
        else if (tile.tileType == TileType.Concrete) concreteCount++;
    }

    // System.Random を受け取るように変更
    StructureItem GetWeightedRandomStructure(List<StructureItem> candidates, System.Random rng)
    {
        int totalWeight = candidates.Sum(x => x.weight);
        int randomValue = rng.Next(0, totalWeight);
        int currentSum = 0;
        foreach (var item in candidates)
        {
            currentSum += item.weight;
            if (randomValue < currentSum) return item;
        }
        return candidates[0];
    }

    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;
        // データが消えていても、生成順序の保証により隣接データ（プレイヤーに近い側）は存在することが多い
        // もし存在しなくても -1 (自由接続) で生成されるため、致命的な破綻は起きない
        if (!spawnedTileData.ContainsKey(targetCoord)) return -1;
        MapTileData neighbor = spawnedTileData[targetCoord];
        switch (requiredSide) { case "top": return (int)neighbor.top; case "bottom": return (int)neighbor.bottom; case "left": return (int)neighbor.left; case "right": return (int)neighbor.right; default: return -1; }
    }
}