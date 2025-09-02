using UnityEngine;
using System; // Actionイベントのために必要

/// <summary>
/// ゲーム内の時間経過、日数、イベントを管理するシングルトンクラス
/// </summary>
public class GameTimeManager : MonoBehaviour
{
    // シングルトンのインスタンス
    public static GameTimeManager Instance { get; private set; }

    [Header("時間設定")]
    [Tooltip("1日が経過するのにかかる現実世界の時間（分）")]
    public float minutesPerDay = 5.0f;

    [Header("現在のゲーム内時間")]
    [Range(0, 23)] public int currentHour;
    [Range(0, 59)] public int currentMinute;
    public int daysSurvived { get; private set; }

    // 1秒あたりに進むゲーム内時間の分数
    private float minuteMultiplier;
    private float timeAccumulator = 0f;

    // 時間変化を他のスクリプトに通知するためのイベント
    public static event Action<int, int> OnTimeChanged; // <時, 分>
    public static event Action<int> OnDayChanged;      // <日数>

    private void Awake()
    {
        // シングルトンパターンの実装
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

    private void Start()
    {
        // 24時間 * 60分 = 1440分（1日の総分数）
        // これを現実世界の指定した分数で割ることで、時間経過の倍率を計算する
        minuteMultiplier = 1440f / (minutesPerDay * 60f);

        // ゲーム開始時にイベントを発行
        OnDayChanged?.Invoke(daysSurvived);
        OnTimeChanged?.Invoke(currentHour, currentMinute);
    }

    private void Update()
    {
        // Time.deltaTimeに倍率をかけて、経過したゲーム内分数を計算
        timeAccumulator += Time.deltaTime * minuteMultiplier;

        // 経過した分数が1を超えたら、時間を進める
        if (timeAccumulator >= 1f)
        {
            int minutesPassed = Mathf.FloorToInt(timeAccumulator);
            timeAccumulator -= minutesPassed;

            for (int i = 0; i < minutesPassed; i++)
            {
                AdvanceMinute();
            }
        }
    }

    // 分を進める処理
    private void AdvanceMinute()
    {
        currentMinute++;
        if (currentMinute >= 60)
        {
            currentMinute = 0;
            currentHour++;
            if (currentHour >= 24)
            {
                currentHour = 0;
                daysSurvived++;
                OnDayChanged?.Invoke(daysSurvived); // 日付変更イベント
            }
        }
        OnTimeChanged?.Invoke(currentHour, currentMinute); // 時間変更イベント
    }

    /// <summary>
    /// 現在の時刻を文字列で取得する (例: "14:05")
    /// </summary>
    public string GetTimeAsString()
    {
        return $"{currentHour:D2}:{currentMinute:D2}";
    }
}