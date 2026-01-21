using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    [Header("設定")]
    public int tileSize = 20;
    public int viewDistance = 3; // 見える範囲
    public int destroyDistance = 4; // これ以上離れたら消す（viewDistanceより大きくする）
    public Transform player;

    [Header("タイルデータ")]
    public List<MapTileData> allTiles;

    // 生成済みのタイルデータ（地図の記憶：これは消さない）
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    
    // 生成済みのゲームオブジェクト（見た目：遠くに行くと消す）
    private Dictionary<Vector2Int, GameObject> spawnedObjects = new Dictionary<Vector2Int, GameObject>();

    private Vector2Int currentChunkCoord;

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
        // 1. 新しいタイルの生成（または再表示）
        for (int x = currentChunkCoord.x - viewDistance; x <= currentChunkCoord.x + viewDistance; x++)
        {
            for (int y = currentChunkCoord.y - viewDistance; y <= currentChunkCoord.y + viewDistance; y++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                
                // オブジェクトがまだ存在しない場合のみ生成処理を行う
                if (!spawnedObjects.ContainsKey(coord))
                {
                    SpawnTileAt(coord);
                }
            }
        }

        // 2. 遠くのタイルの削除
        CleanupTiles();
    }

    void SpawnTileAt(Vector2Int coord)
    {
        MapTileData selectedData;

        // 【重要】既にデータがあるかチェック（戻ってきた場合）
        if (spawnedTileData.ContainsKey(coord))
        {
            // 以前生成したデータを再利用（これで景色が変わらない）
            selectedData = spawnedTileData[coord];
        }
        else
        {
            // --- 新規生成ロジック ---
            
            // 1. 周囲の接続タイプを取得
            int reqTop    = GetNeighborConnection(coord, Vector2Int.up,    "bottom");
            int reqBottom = GetNeighborConnection(coord, Vector2Int.down,  "top");
            int reqLeft   = GetNeighborConnection(coord, Vector2Int.left,  "right");
            int reqRight  = GetNeighborConnection(coord, Vector2Int.right, "left");

            // 2. 条件に合うタイルを絞り込む
            var validTiles = allTiles.Where(t => 
                (reqTop    == -1 || (int)t.top    == reqTop) &&
                (reqBottom == -1 || (int)t.bottom == reqBottom) &&
                (reqLeft   == -1 || (int)t.left   == reqLeft) &&
                (reqRight  == -1 || (int)t.right  == reqRight)
            ).ToList();

            // 候補がない場合のフォールバック
            if (validTiles.Count == 0)
            {
                // エラー時はとりあえずリストの最初を使う
                if (allTiles.Count > 0) validTiles.Add(allTiles[0]);
                else return;
            }

            // 3. ランダムに選択
            selectedData = validTiles[Random.Range(0, validTiles.Count)];
            
            // データを辞書に保存（記憶する）
            spawnedTileData.Add(coord, selectedData);
        }

        // --- 生成処理 (共通) ---
        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        // 回転をPrefabの設定に合わせる修正も適用済み
        GameObject obj = Instantiate(selectedData.prefab, pos, selectedData.prefab.transform.rotation, transform);

        spawnedObjects.Add(coord, obj);
    }

    // 遠くのオブジェクトを削除
    void CleanupTiles()
    {
        // 削除対象を一時リストに保存
        List<Vector2Int> tilesToRemove = new List<Vector2Int>();

        foreach (var item in spawnedObjects)
        {
            // プレイヤーとの距離を計算
            float dist = Vector2.Distance(item.Key, currentChunkCoord);
            
            // 設定した削除距離より遠ければ
            if (dist > destroyDistance)
            {
                // GameObjectを破壊
                Destroy(item.Value);
                // リストに追加
                tilesToRemove.Add(item.Key);
            }
        }

        // 辞書から削除
        foreach (var coord in tilesToRemove)
        {
            spawnedObjects.Remove(coord);
        }
    }
    // 

    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;

        // まだ生成されていない場合は -1
        if (!spawnedTileData.ContainsKey(targetCoord))
        {
            return -1;
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