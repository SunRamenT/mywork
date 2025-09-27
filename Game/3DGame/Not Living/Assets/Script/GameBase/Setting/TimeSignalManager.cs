// TimeSignalManager.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class TimeSignalManager : MonoBehaviour
{
    [Header("時間帯ごとの合図")]
    [Tooltip("合図を鳴らしたい時間と、再生するAudioClipのリスト")]
    public List<TimeSignal> timeSignals;

    private AudioSource audioSource;
    private int lastHour = -1; // 前回チェックした時間を記憶しておく

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        // GameTimeManagerの時間変更イベントに、自身のチェックメソッドを登録
        GameTimeManager.OnTimeChanged += CheckForTimeSignal;
    }

    private void OnDisable()
    {
        // オブジェクトが破棄される際に、登録を解除
        GameTimeManager.OnTimeChanged -= CheckForTimeSignal;
    }

    /// <summary>
    /// 時間が変更されるたびに呼び出され、合図を鳴らすか判断する
    /// </summary>
    private void CheckForTimeSignal(int hour, int minute)
    {
        // まだ同じ時間（時）なら、何もしない（1時間のうち最初の1回だけ判定するため）
        if (hour == lastHour)
        {
            return;
        }

        // 現在の時間を記録
        lastHour = hour;

        // 設定されたリストの中から、現在の時間と一致するものを探す
        foreach (var signal in timeSignals)
        {
            if (hour == signal.triggerHour)
            {
                // 一致するものが見つかったら
                Debug.Log($"<color=cyan>時刻が{hour}時になりました。合図の音を再生します。</color>");
                // 設定された効果音を一度だけ再生
                audioSource.PlayOneShot(signal.signalSound);
                // 一致するものが1つ見つかったら、ループを抜ける
                break;
            }
        }
    }
}