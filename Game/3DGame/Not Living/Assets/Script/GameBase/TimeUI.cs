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
        if (timeText != null)
        {
            timeText.text = $"{hour:D2}:{minute:D2}";
        }
    }
}