using UnityEngine;

public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    // PlayerPrefsに保存する時の「鍵」となる名前
    private const string HighScoreKey = "HighScore";

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
    /// 新しいスコアを受け取り、ハイスコアなら更新して保存する
    /// </summary>
    public void SaveHighScore(int newScore)
    {
        // これまでのハイスコアを読み込む（なければ0）
        int oldHighScore = LoadHighScore();

        // 新しいスコアの方が高ければ更新
        if (newScore > oldHighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, newScore);
            PlayerPrefs.Save(); // 変更をディスクに書き込む
            Debug.Log($"ハイスコア更新！ {oldHighScore} -> {newScore}");
        }
    }

    /// <summary>
    /// 保存されているハイスコアを読み込む
    /// </summary>
    public int LoadHighScore()
    {
        // "HighScore"という鍵で保存された整数を読み込む。なければ0を返す。
        return PlayerPrefs.GetInt(HighScoreKey, 0);
    }
}