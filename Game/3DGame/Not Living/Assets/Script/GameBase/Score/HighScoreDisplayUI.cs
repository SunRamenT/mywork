using UnityEngine;
using TMPro;

public class HighScoreDisplayUI : MonoBehaviour
{
    public TextMeshProUGUI highScoreText;

    void OnEnable()
    {
        // 保存されているハイスコアを読み込む
        int highScore = HighScoreManager.Instance.LoadHighScore();
        
        // テキストを更新（9桁ゼロ埋め）
        highScoreText.text = "HI-SCORE: " + highScore.ToString("D9");
    }
}