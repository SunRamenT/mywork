using UnityEngine;

// タイルの形状タイプ（ここを修正）
public enum TileType
{
    Straight,
    Corner,
    T_Junction,
    Cross,
    DeadEnd,
    Grass,    // 
    Concrete  // 
}

public enum ConnectionType
{
    Ground, // 接続上はどちらも「地面」として扱い、隣り合えるようにする
    Road
}

[CreateAssetMenu(fileName = "NewMapTile", menuName = "Map Generation/Tile Data")]
public class MapTileData : ScriptableObject
{
    public GameObject prefab;
    public TileType tileType;

    [Range(1, 100)]
    public int weight = 10;

    [Header("接続情報")]
    public ConnectionType top;
    public ConnectionType right;
    public ConnectionType bottom;
    public ConnectionType left;
}