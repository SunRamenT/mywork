using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    [Header("設定")]
    public int tileSize = 20;
    public int viewDistance = 3; // テスト用なので狭く設定
    public Transform player;

    [Header("タイルデータ")]
    public List<MapTileData> allTiles;

    // 生成済みのタイルデータ（接続確認用）
    private Dictionary<Vector2Int, MapTileData> spawnedTileData = new Dictionary<Vector2Int, MapTileData>();
    // 生成済みのゲームオブジェクト（削除用）
    private Dictionary<Vector2Int, GameObject> spawnedObjects = new Dictionary<Vector2Int, GameObject>();

    private Vector2Int currentChunkCoord;

    void Start()
    {
        // 最初に一回生成
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
                if (!spawnedTileData.ContainsKey(coord))
                {
                    SpawnTileAt(coord);
                }
            }
        }
    }

    void SpawnTileAt(Vector2Int coord)
    {
        // 1. 周囲の接続タイプを取得 (戻り値を int にすることで -1 を扱えるようにする)
        int reqTop    = GetNeighborConnection(coord, Vector2Int.up,    "bottom");
        int reqBottom = GetNeighborConnection(coord, Vector2Int.down,  "top");
        int reqLeft   = GetNeighborConnection(coord, Vector2Int.left,  "right");
        int reqRight  = GetNeighborConnection(coord, Vector2Int.right, "left");

        // 2. 条件に合うタイルを絞り込む
        // エラーの原因だった箇所：enumを(int)でキャストして比較します
        var validTiles = allTiles.Where(t => 
            (reqTop    == -1 || (int)t.top    == reqTop) &&
            (reqBottom == -1 || (int)t.bottom == reqBottom) &&
            (reqLeft   == -1 || (int)t.left   == reqLeft) &&
            (reqRight  == -1 || (int)t.right  == reqRight)
        ).ToList();

        // 候補がない場合のフォールバック（エラー回避）
        if (validTiles.Count == 0)
        {
            Debug.LogError($"接続できるタイルがありません: {coord}");
            // 強制的にリストの最初のものを置く（穴あき防止）
            if (allTiles.Count > 0) validTiles.Add(allTiles[0]); 
            else return;
        }

        // 3. ランダムに選択して生成
        MapTileData selectedData = validTiles[Random.Range(0, validTiles.Count)];
        
        Vector3 pos = new Vector3(coord.x * tileSize, 0, coord.y * tileSize);
        //GameObject obj = Instantiate(selectedData.prefab, pos, Quaternion.identity, transform);
        // 変更後（Prefabに設定されている回転を使用する）
        GameObject obj = Instantiate(selectedData.prefab, pos, selectedData.prefab.transform.rotation, transform);

        spawnedTileData.Add(coord, selectedData);
        spawnedObjects.Add(coord, obj);
    }

    // 指定した方向にあるタイルの、自分と接する面の接続タイプを取得
    int GetNeighborConnection(Vector2Int myCoord, Vector2Int direction, string requiredSide)
    {
        Vector2Int targetCoord = myCoord + direction;

        // まだ生成されていない場合は -1 (何でもOK) を返す
        if (!spawnedTileData.ContainsKey(targetCoord))
        {
            return -1;
        }

        MapTileData neighbor = spawnedTileData[targetCoord];

        // 隣のタイルが持っている接続情報を返す
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