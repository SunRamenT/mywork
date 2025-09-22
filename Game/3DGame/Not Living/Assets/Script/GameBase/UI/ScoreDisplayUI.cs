// ScoreDisplayUI.cs
using UnityEngine;
using TMPro; // TextMeshProを使う場合

public class ScoreDisplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    // ScoreManagerへのイベント登録が完了したかどうかのフラグ
    private bool isSubscribed = false;

    // ▼▼▼ OnEnableの代わりにUpdateで、より確実に接続を試みるように変更 ▼▼▼
    private void Update()
    {
        // まだイベント登録が済んでおらず、ScoreManagerが利用可能になったら
        if (!isSubscribed && ScoreManager.Instance != null)
        {
            // イベントにテキスト更新メソッドを登録する
            ScoreManager.Instance.OnScoreChanged += UpdateScoreText;
            // 登録が完了したことを記録
            isSubscribed = true;
            // 現在のスコアで一度表示を更新する
            UpdateScoreText(ScoreManager.Instance.CurrentScore);
        }
    }

    private void OnDisable()
    {
        // ScoreManagerが存在すれば、登録を解除する
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= UpdateScoreText;
        }
    }
    
    /// <summary>
    /// スコアの数値を受け取り、9桁のゼロ埋め形式でテキストを更新する
    /// </summary>
    private void UpdateScoreText(int newScore)
    {
        if (scoreText != null)
        {
            // "D9"は「9桁の整数（足りない分は0で埋める）」という書式設定
            scoreText.text = newScore.ToString("D9");
        }
    }
}