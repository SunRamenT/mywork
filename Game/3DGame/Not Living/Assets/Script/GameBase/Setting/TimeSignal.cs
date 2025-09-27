// TimeSignal.cs
using UnityEngine;

[System.Serializable]
public class TimeSignal
{
    public string description = "合図の説明"; // Inspectorでの見出し
    public AudioClip signalSound;          // この時間帯に鳴らす効果音
    [Range(0, 23)]
    public int triggerHour;                // この時間（時）になった瞬間に音を鳴らす
}