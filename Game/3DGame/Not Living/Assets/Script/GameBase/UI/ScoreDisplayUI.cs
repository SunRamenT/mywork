// ScoreDisplayUI.cs
using UnityEngine;
using TMPro; // TextMeshProを使う場合

public class ScoreDisplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        if(ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged += UpdateScoreText;
    }

    private void OnDisable()
    {
        if(ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= UpdateScoreText;
    }



    private void Start()
    {
        if(ScoreManager.Instance != null)
            UpdateScoreText(ScoreManager.Instance.CurrentScore);
    }
    
    private void UpdateScoreText(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = newScore.ToString("D6");
        }
    }
}