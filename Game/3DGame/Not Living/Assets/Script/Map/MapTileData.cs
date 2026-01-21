using UnityEngine;

// タイルの形状タイプ（ルール判定用）
public enum TileType
{
    Straight, // 直線
    Corner,   // 曲がり角
    T_Junction, // T字路
    Cross,    // 十字路
    DeadEnd,  // 行き止まり
    Ground    // 芝生や空き地
}

// 接続の種類（既存）
public enum ConnectionType
{
    Ground,
    Road
}

[CreateAssetMenu(fileName = "NewMapTile", menuName = "Map Generation/Tile Data")]
public class MapTileData : ScriptableObject
{
    public GameObject prefab;
    public TileType tileType; // ▼ 追加: タイルの種類

    [Header("生成確率 (大きいほど出やすい)")]
    [Range(1, 100)]
    public int weight = 10;   // ▼ 追加: 重み

    [Header("接続情報")]
    public ConnectionType top;
    public ConnectionType right;
    public ConnectionType bottom;
    public ConnectionType left;
}