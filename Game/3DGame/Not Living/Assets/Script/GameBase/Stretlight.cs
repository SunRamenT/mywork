using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 個々の街灯にアタッチし、時間に応じて自分を点灯・消灯させるクラス
/// </summary>
[RequireComponent(typeof(Light))]
public class Stretlight : MonoBehaviour
{
    [Header("点灯・消灯時間の設定")]
    [Range(0, 23)]
    public int turnOnHour = 18;
    [Range(0, 23)]
    public int turnOffHour = 4;

    // この街灯自身のLightコンポーネント
    private Light myLight;

    void Awake()
    {
        // 自身のLightコンポーネントを取得
        myLight = GetComponent<Light>();
    }

    void Start()
    {
        // ゲーム開始時の時間で、初期状態を正しく設定
        if (GameTimeManager.Instance != null)
        {
            HandleTimeChanged(GameTimeManager.Instance.currentHour, GameTimeManager.Instance.currentMinute);
        }
    }

    private void OnEnable()
    {
        GameTimeManager.OnTimeChanged += HandleTimeChanged;
    }

    private void OnDisable()
    {
        GameTimeManager.OnTimeChanged -= HandleTimeChanged;
    }

    /// <summary>
    /// 時間の変更に応じてライトの点灯・消灯を判断する
    /// </summary>
    private void HandleTimeChanged(int hour, int minute)
    {
        // 夜間（点灯時間内）かどうかを判定
        bool isNight = (hour >= turnOnHour || hour < turnOffHour);
        
        // 状態が異なる場合のみ更新
        if (myLight.enabled != isNight)
        {
            myLight.enabled = isNight;
        }
    }
}