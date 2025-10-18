using UnityEngine;
using TMPro;

public class RankingRowUI : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI scoreText;

    public void SetData(int rank, RankingEntry entry)
    {
        rankText.text = $"{rank}.";
        nameText.text = entry.name;
        scoreText.text = entry.score.ToString("D9"); // 9桁ゼロ埋め
    }
}