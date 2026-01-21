using UnityEngine;

// 接続の種類
public enum ConnectionType
{
    Ground, // 何もない地面（芝生など）
    Road    // 道路
}

[CreateAssetMenu(fileName = "NewMapTile", menuName = "Map Generation/Tile Data")]
public class MapTileData : ScriptableObject
{
    public GameObject prefab;

    [Header("接続情報 (上・右・下・左)")]
    public ConnectionType top;
    public ConnectionType right;
    public ConnectionType bottom;
    public ConnectionType left;
}