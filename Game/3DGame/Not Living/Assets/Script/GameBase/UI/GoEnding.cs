using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GoEnding : MonoBehaviour
{
    /// 「エンディングへ」ボタンから呼び出される
    public void ProceedToEnding()
    {
        // 各Managerから最終的な値を取得
        int finalScore = ScoreManager.Instance.CurrentScore;
        Vector3 finalAlignment = AlignmentManager.Instance.CurrentAlignment;
        float goodEvil = finalAlignment.y;
        float chaos = finalAlignment.z;

        // 時間を元に戻すのを忘れない<--これがないとゲームの時間が止まったまま
        Time.timeScale = 1f;
        GameStateManager.Instance.SetState(GameStateManager.GameState.Gameplay);

        Debug.Log($"最終スコア: {finalScore}, 最終座標: (Y={goodEvil}, Z={chaos})");

        // --- ここからエンディングの条件分岐 ---
        if (goodEvil <= -80 && chaos <= -50)
        {
            Debug.Log("エンディングA: Very Good End");
            SceneManager.LoadScene("Ending_A"); // Aのシーンへ
        }
        else if (goodEvil <= -50)
        {
            Debug.Log("エンディングB: Good End");
            SceneManager.LoadScene("Ending_B"); // Bのシーンへ
        }
        // ... 他のエンディング条件を追加 ...
        else
        {
            Debug.Log("エンディングE: Neutral End");
            SceneManager.LoadScene("Ending_E"); // Eのシーンへ
        }
    }
}
