// EndingManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingManager : MonoBehaviour
{
    public void DetermineAndLoadEnding()
    {
        int finalScore = ScoreManager.Instance.CurrentScore;
        Vector3 finalAlignment = AlignmentManager.Instance.CurrentAlignment;
        float goodEvil = finalAlignment.y;
        float chaos = finalAlignment.z;

        Debug.Log($"最終スコア: {finalScore}, 最終座標: (Y={goodEvil}, Z={chaos})");

        // --- ここからエンディングの条件分岐 ---
        if (goodEvil >= 80 && chaos <= -50)
        {
            Debug.Log("エンディングA: 秩序の聖人");
            // SceneManager.LoadScene("Ending_A");
        }
        else if (goodEvil >= 50)
        {
            Debug.Log("エンディングB: 善良な市民");
            // SceneManager.LoadScene("Ending_B");
        }
        // ... 他のエンディング条件を追加 ...
        else
        {
            Debug.Log("エンディングE: 平凡な結末");
            // SceneManager.LoadScene("Ending_E");
        }
    }
}