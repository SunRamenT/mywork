using UnityEngine;
using TMPro; // TextMeshProを使用するために必要

/// <summary>
/// GameTimeManagerから時間を受け取り、UIに表示する
/// </summary>
public class TimeUI : MonoBehaviour
{
    [Header("UI要素")]
    public TextMeshProUGUI dayText;   // 「x日目」を表示するテキスト
    public TextMeshProUGUI timeText;  // 「HH:MM」を表示するテキスト
    public TextMeshProUGUI timeOfDayText; // 「朝、昼、夕、夜」を表示するテキスト

    // OnEnable/OnDisableでイベントの登録・解除を行うのが安全
    private void OnEnable()
    {
        GameTimeManager.OnDayChanged += UpdateDayUI;
        GameTimeManager.OnTimeChanged += UpdateTimeUI;
    }

    private void OnDisable()
    {
        GameTimeManager.OnDayChanged -= UpdateDayUI;
        GameTimeManager.OnTimeChanged -= UpdateTimeUI;
    }

    // 日付UIを更新する
    private void UpdateDayUI(int day)
    {
        if (dayText != null)
        {
            dayText.text = $"{day + 1}日目"; // 0日目を1日目として表示
        }
    }

    // 時刻UIを更新する
    private void UpdateTimeUI(int hour, int minute)
    {
        // HH:MM形式の時刻表示
        if (timeText != null)
        {
            timeText.text = $"{hour:D2}:{minute:D2}";
        }

        // ▼▼▼ 時間帯のテキストを更新する処理を追加 ▼▼▼
        if (timeOfDayText != null)
        {
            if (hour >= 5 && hour < 12)
            {
                timeOfDayText.text = "朝";
            }
            else if (hour >= 12 && hour < 17)
            {
                timeOfDayText.text = "昼";
            }
            else if (hour >= 17 && hour < 20)
            {
                timeOfDayText.text = "夕";
            }
            else
            {
                timeOfDayText.text = "夜";
            }
        }
    }
}