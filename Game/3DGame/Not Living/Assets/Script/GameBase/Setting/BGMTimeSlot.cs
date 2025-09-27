// BGMTimeSlot.cs
using UnityEngine;

// [System.Serializable]を付けると、Inspector上に表示できるようになる
[System.Serializable]
public class BGMTimeSlot
{
    public string description = "時間帯の説明"; // Inspectorでの見出し
    public AudioClip bgmClip; // この時間帯に流すBGM
    [Range(0, 23)]
    public int startHour; // この時間（時）からBGMを開始
}