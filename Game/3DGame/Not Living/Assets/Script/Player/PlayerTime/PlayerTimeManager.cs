using UnityEngine;
using System.Collections;

public class PlayerTimeManager : MonoBehaviour
{
    public static PlayerTimeManager Instance { get; private set; }
    
    // 現在プレイヤーに適用すべき時間の倍率
    public float PlayerTimeScale { get; private set; } = 1f;

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
    /// ジャスト回避成功時に呼び出され、スローモーションを開始する
    /// </summary>
    public void StartSlowMotion(float slowTimeScale, float duration)
    {
        StartCoroutine(SlowMotionCoroutine(slowTimeScale, duration));
    }

    private IEnumerator SlowMotionCoroutine(float slowTimeScale, float duration)
    {
        // 1. ゲーム全体の時間を遅くする
        Time.timeScale = slowTimeScale;
        PlayerController playerController = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        // 2. プレイヤーの時間の倍率を、遅くなった分だけ速くする
        // (例: 全体が0.1倍速になったら、プレイヤーは10倍速で動けば、結果的に通常速度に見える)
        PlayerTimeScale = 1f / slowTimeScale;
        playerController.isDodging = true;
        // 現在のアニメーターの再生速度を、プレイヤーの時間倍率に合わせる
        if (playerController.currentAnimator != null)
        {
            playerController.currentAnimator.speed = PlayerTimeScale;
        }
        // 3. 指定された時間が経過するまで待つ
        // Time.timeScaleの影響を受けないリアルタイム秒数で待機
        yield return new WaitForSecondsRealtime(duration);
                
        playerController.isDodging = false;

        // 4. 時間の設定を全て元に戻す
        Time.timeScale = 1f;
        PlayerTimeScale = 1f;
        if (playerController.currentAnimator != null)
        {
            playerController.currentAnimator.speed = PlayerTimeScale;
        }
    }
}