using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
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

    public void ApplyHitStop(float duration)
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration));
    }

    private IEnumerator HitStopCoroutine(float duration)
    {
        // ▼▼▼ デバッグログを追加 ▼▼▼
        Debug.Log($"<color=orange>【ヒットストップ開始】 Time.timeScaleを 0.01f に変更します。持続時間: {duration}秒</color>");
        
        // 時間の流れをほぼ停止させる
        Time.timeScale = 0.01f;

        // Time.timeScaleの影響を受けないリアルタイム秒数で待機する
        yield return new WaitForSecondsRealtime(duration);

        // ▼▼▼ デバッグログを追加 ▼▼▼
        Debug.Log("<color=green>【ヒットストップ終了】 Time.timeScaleを 1.0f に戻します。</color>");
        
        // 時間の流れを元に戻す
        Time.timeScale = 1.0f;
    }
}