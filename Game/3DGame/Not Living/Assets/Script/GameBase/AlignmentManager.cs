// AlignmentManager.cs
using UnityEngine;
using System;

public class AlignmentManager : MonoBehaviour
{
    public static AlignmentManager Instance { get; private set; }

    [Header("座標の範囲設定")]
    [SerializeField] private Vector2 goodEvilRange = new Vector2(-100, 100);
    [SerializeField] private Vector2 chaosRange = new Vector2(-100, 100);

    private float _timeValue;
    private float _goodEvilValue;
    private float _chaosValue;
    public Vector3 CurrentAlignment => new Vector3(_timeValue, _goodEvilValue, _chaosValue);
    public event Action<Vector3> OnAlignmentChanged;

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
        GameTimeManager.OnTimeChanged += UpdateTimeAxis;
        GameEvents.OnTargetDefeatedWithInfo += HandleTargetDefeated;
        GameEvents.OnGoodDeedPerformed += HandleGoodDeed;
        GameEvents.OnChaosValueChange += AddChaosValue;
    }

    private void OnDisable()
    {
        GameTimeManager.OnTimeChanged -= UpdateTimeAxis;
        GameEvents.OnTargetDefeatedWithInfo -= HandleTargetDefeated;
        GameEvents.OnGoodDeedPerformed -= HandleGoodDeed;
        GameEvents.OnChaosValueChange -= AddChaosValue;
    }

    private void UpdateTimeAxis(int hour, int minute)
    {
        _timeValue = (float)GameTimeManager.Instance.daysSurvived * 24f + hour + (float)minute / 60f;
        NotifyAlignmentChange();
    }

    // ▼▼▼ このメソッドのロジックを変更 ▼▼▼
    /// <summary>
    /// 敵を倒した際に呼ばれる。相手の評判に関わらず、善行として+1する
    /// </summary>
    private void HandleTargetDefeated(StatusManager defeatedStatus)
    {
        AddGoodEvilValue(1f);
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void HandleGoodDeed() => AddGoodEvilValue(2f);
    
    public void AddGoodEvilValue(float amount)
    {
        _goodEvilValue = Mathf.Clamp(_goodEvilValue + amount, goodEvilRange.x, goodEvilRange.y);
        Debug.Log($"[善悪値更新] {amount}変動しました。現在の善悪値: {_goodEvilValue}");
        NotifyAlignmentChange();
    }
    
    public void AddChaosValue(float amount)
    {
        _chaosValue = Mathf.Clamp(_chaosValue + amount, chaosRange.x, chaosRange.y);
        Debug.Log($"[カオス値更新] {amount}変動しました。現在のカオス値: {_chaosValue}");
        NotifyAlignmentChange();
    }

    private void NotifyAlignmentChange() => OnAlignmentChanged?.Invoke(CurrentAlignment);
}