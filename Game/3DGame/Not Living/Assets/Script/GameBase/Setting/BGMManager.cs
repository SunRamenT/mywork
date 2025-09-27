using UnityEngine;
using System.Collections; // Coroutineを使うために必要
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    [Header("BGM設定")]
    [Tooltip("時間帯ごとのBGMリスト。開始時間(startHour)の昇順で並べてください。")]
    public List<BGMTimeSlot> bgmTimeSlots;

    [Header("フェード設定")]
    [Tooltip("BGMが切り替わる際のフェードアウト/インの時間（秒）")]
    public float fadeDuration = 2.0f;

    private AudioSource audioSource;
    private AudioClip currentClip;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true; // BGMはループ再生
    }

    private void OnEnable()
    {
        // GameTimeManagerの時間変更イベントに、自身のBGMチェックメソッドを登録
        GameTimeManager.OnTimeChanged += CheckForBGMChange;
    }

    private void OnDisable()
    {
        // オブジェクトが破棄される際に、登録を解除
        GameTimeManager.OnTimeChanged -= CheckForBGMChange;
    }

    /// <summary>
    /// 時間が変更されるたびに呼び出され、BGMを切り替えるか判断する
    /// </summary>
    private void CheckForBGMChange(int hour, int minute)
    {
        BGMTimeSlot targetSlot = null;

        // 設定されたリストの中から、現在の時間に最も近い過去の開始時間のスロットを探す
        foreach (var slot in bgmTimeSlots)
        {
            if (hour >= slot.startHour)
            {
                targetSlot = slot;
            }
        }
        
        // 適切なスロットが見つかり、かつそれが現在再生中のBGMでなければ
        if (targetSlot != null && currentClip != targetSlot.bgmClip)
        {
            // 新しいBGMに切り替える
            currentClip = targetSlot.bgmClip;
            // 実行中のクロスフェードがあれば停止してから新しいものを開始
            StopAllCoroutines();
            StartCoroutine(CrossfadeBGM(currentClip));
        }
    }

    /// <summary>
    /// 新しいBGMに滑らかにクロスフェードする
    /// </summary>
    // ▼▼▼ この行の型を修正 ▼▼▼
    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        float startVolume = audioSource.volume;
        float timer = 0f;

        // 現在のBGMをフェードアウト
        while (timer < fadeDuration / 2)
        {
            audioSource.volume = Mathf.Lerp(startVolume, 0, timer / (fadeDuration / 2));
            timer += Time.deltaTime;
            yield return null;
        }
        audioSource.volume = 0;
        audioSource.Stop();

        // 新しいBGMを設定して再生開始
        audioSource.clip = newClip;
        audioSource.Play();
        
        // 新しいBGMをフェードイン
        timer = 0f;
        while (timer < fadeDuration / 2)
        {
            audioSource.volume = Mathf.Lerp(0, startVolume, timer / (fadeDuration / 2));
            timer += Time.deltaTime;
            yield return null;
        }
        audioSource.volume = startVolume;
    }
}