// ScoreManager.cs
using UnityEngine;
using System;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("スコア設定")]
    [SerializeField] private int maxScore = 999999;

    [Header("スコア加算量")]
    [SerializeField] private int scoreForTimePassage = 100;
    [SerializeField] private int scoreForDefeatingTarget = 500;
    [SerializeField] private int scoreForGoodDeed = 1000;

    private int _currentScore;
    public int CurrentScore => _currentScore;
    public event Action<int> OnScoreChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); }
    }

    private void OnEnable()
    {
        GameEvents.OnHalfDayPassed += AddScoreForTime;
        GameEvents.OnTargetDefeatedWithInfo += AddScoreForDefeat; // Info版をリッスン
        GameEvents.OnGoodDeedPerformed += AddScoreForGoodDeed;
    }

    private void OnDisable()
    {
        GameEvents.OnHalfDayPassed -= AddScoreForTime;
        GameEvents.OnTargetDefeatedWithInfo -= AddScoreForDefeat;
        GameEvents.OnGoodDeedPerformed -= AddScoreForGoodDeed;
    }

    private void AddScoreForTime() => AddScore(scoreForTimePassage);
    // 倒した相手の情報は今のところ不要だが、イベントを統一しておく
    private void AddScoreForDefeat(StatusManager defeatedStatus) => AddScore(scoreForDefeatingTarget);
    private void AddScoreForGoodDeed() => AddScore(scoreForGoodDeed);

    public void AddScore(int amount)
    {
        _currentScore = Mathf.Clamp(_currentScore + amount, 0, maxScore);
        OnScoreChanged?.Invoke(_currentScore);
    }
}