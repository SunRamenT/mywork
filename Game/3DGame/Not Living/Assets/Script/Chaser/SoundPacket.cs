// SoundPacket.cs
using UnityEngine;

// 音の種類を定義する
public enum SoundType
{
    PlayerFootstep, // プレイヤーの足音
    PlayerAction,   // プレイヤーのアクション音（パンチなど）
    EnemyNoise      // 敵自身の音
}

// 送信するメッセージの本体
public class SoundPacket
{
    public Vector3 Position { get; private set; } // 音が発生した座標
    public float Volume { get; private set; }     // 音の大きさ（届く半径）
    public SoundType Type { get; private set; }   // 音の種類

    public SoundPacket(Vector3 position, float volume, SoundType type)
    {
        this.Position = position;
        this.Volume = volume;
        this.Type = type;
    }
}