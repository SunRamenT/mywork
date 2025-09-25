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

    /// <summary>
    /// 指定した時間だけヒットストップを適用する
    /// </summary>
    /// <param name="duration">ヒットストップの時間（秒）</param>
    /// <param name="caller">このメソッドを呼び出したスクリプト（デバッグ用）</param>
    public void ApplyHitStop(float duration, MonoBehaviour caller)
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        // 呼び出し元の情報をコルーチンに渡す
        hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration, caller));
    }

    private IEnumerator HitStopCoroutine(float duration, MonoBehaviour caller)
    {
        // ▼▼▼ 呼び出し元の情報をログに表示 ▼▼▼
        Debug.Log($"<color=orange>【ヒットストップ開始】 Time.timeScaleを 0.01f に変更。呼び出し元: {caller.gameObject.name} ({caller.GetType().Name})</color>", caller.gameObject);
        
        Time.timeScale = 0.01f;
        yield return new WaitForSecondsRealtime(duration);
        
        Debug.Log("<color=green>【ヒットストップ終了】 Time.timeScaleを 1.0f に戻します。</color>");
        Time.timeScale = 1.0f;
    }
}