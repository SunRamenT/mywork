using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    // どこからでもアクセスできるシングルトンインスタンス
    public static HitStopManager Instance { get; private set; }

    private Coroutine hitStopCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 指定した時間だけヒットストップを適用する
    /// </summary>
    /// <param name="duration">ヒットストップの時間（秒）</param>
    public void ApplyHitStop(float duration)
    {
        // 既にヒットストップ中なら、新しいもので上書きするために一度停止
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration));
    }

    private IEnumerator HitStopCoroutine(float duration)
    {
        // 時間の流れをほぼ停止させる（0にすると止まってしまう処理があるため、ごく僅かに動かす）
        Time.timeScale = 0.01f;

        // Time.timeScaleの影響を受けないリアルタイム秒数で待機する
        yield return new WaitForSecondsRealtime(duration);

        // 時間の流れを元に戻す
        Time.timeScale = 1.0f;
    }
}