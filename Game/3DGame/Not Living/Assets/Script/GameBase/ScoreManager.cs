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
            Debug.Log("[ScoreManager] ScoreManagerが初期化されました。");
        }
        else { Destroy(gameObject); }
    }

    private void OnEnable()
    {
        Debug.Log("[ScoreManager] 各種イベントの購読を開始します...");
        GameEvents.OnHalfDayPassed += AddScoreForTime;
        GameEvents.OnTargetDefeatedWithInfo += AddScoreForDefeat;
        GameEvents.OnGoodDeedPerformed += AddScoreForGoodDeed;
    }

    private void OnDisable()
    {
        GameEvents.OnHalfDayPassed -= AddScoreForTime;
        GameEvents.OnTargetDefeatedWithInfo -= AddScoreForDefeat;
        GameEvents.OnGoodDeedPerformed -= AddScoreForGoodDeed;
    }

    private void AddScoreForTime() => AddScore(scoreForTimePassage);
    
    private void AddScoreForDefeat(StatusManager defeatedStatus)
    {
        Debug.Log($"<color=green>[ScoreManager] 敵撃破イベントを受信！ {defeatedStatus.gameObject.name} を倒したのでスコアを加算します。</color>");
        AddScore(scoreForDefeatingTarget);
    }

    private void AddScoreForGoodDeed() => AddScore(scoreForGoodDeed);

    public void AddScore(int amount)
    {
        _currentScore = Mathf.Clamp(_currentScore + amount, 0, maxScore);
        Debug.Log($"[ScoreManager] スコアが {amount} 加算されました。現在のスコア: {_currentScore}");
        OnScoreChanged?.Invoke(_currentScore);
    }
}