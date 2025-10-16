// GameTimeManager.cs
using UnityEngine;
using System;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    [Header("時間設定")]
    [Tooltip("1日が経過するのにかかる現実世界の時間（分）")]
    public float minutesPerDay = 5.0f;
    [Tooltip("この日数に達するとゲームクリア")]
    public int daysUntilGameClear = 7; 

    [Header("現在のゲーム内時間")]
    [Range(0, 23)] public int currentHour;
    [Range(0, 59)] public int currentMinute;
    public int daysSurvived { get; private set; }

    private float minuteMultiplier;
    private float timeAccumulator = 0f;

    public static event Action<int, int> OnTimeChanged;
    public static event Action<int> OnDayChanged;

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

    private void Start()
    {
        minuteMultiplier = 1440f / (minutesPerDay * 60f);
        OnDayChanged?.Invoke(daysSurvived);
        OnTimeChanged?.Invoke(currentHour, currentMinute);
    }

    private void Update()
    {
        timeAccumulator += Time.deltaTime * minuteMultiplier;
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
                OnDayChanged?.Invoke(daysSurvived);

                CheckForGameClear();
            }

            // ▼▼▼ 追加 ▼▼▼
            // 正午(12時)と深夜(0時)に半日経過イベントを発行する
            if (currentHour == 12 || currentHour == 0)
            {
                GameEvents.TriggerHalfDayPassed();
            }
            // ▲▲▲▲▲▲▲▲▲
        }
        OnTimeChanged?.Invoke(currentHour, currentMinute);
    }

        private void CheckForGameClear()
    {
        if (daysSurvived >= daysUntilGameClear)
        {
            Debug.Log($"{daysUntilGameClear}日が経過しました。ゲームクリア！");
            GameEvents.TriggerGameClear(); // ゲームクリアのイベントを発行
        }
    }
    public string GetTimeAsString()
    {
        return $"{currentHour:D2}:{currentMinute:D2}";
    }
}