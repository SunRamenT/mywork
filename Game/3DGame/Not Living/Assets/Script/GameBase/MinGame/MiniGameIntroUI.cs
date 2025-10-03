using UnityEngine;
using TMPro;
using System.Collections;

public class MiniGameIntroUI : MonoBehaviour
{
    [Header("UI要素")]
    [Tooltip("ミニゲーム名を表示するテキスト")]
    public TextMeshProUGUI gameNameText;
    [Tooltip("難易度を表示するテキスト")]
    public TextMeshProUGUI difficultyText;

    /// <summary>
    /// TaskMachineから呼び出され、UIの表示を開始する
    /// </summary>
    public void ShowIntro(string gameName, string difficultyName, float duration)
    {
        if (gameNameText != null)
        {
            // プレハブ名から "(Clone)" などを取り除いて表示
            gameNameText.text = gameName.Replace("(Clone)", "").Replace("Panel", "");
        }
        if (difficultyText != null)
        {
            difficultyText.text = difficultyName;
        }
        
        // 指定された時間後に、このUIを破棄する
        Destroy(gameObject, duration);
    }
}