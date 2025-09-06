using UnityEngine;
using System; // Actionイベントのために必要

/// <summary>
/// Ghostの霊魂（体力）を管理するクラス
/// </summary>
public class ReikonManager : MonoBehaviour
{
    [Header("霊魂（体力）設定")]
    [Tooltip("霊魂の最大値")]
    public float maxSpirit = 100f;
    [Tooltip("現在の霊魂")]
    [SerializeField] // privateでもインスペクターに表示
    private float currentSpirit;

    [Header("霊魂の減少速度")]
    [Tooltip("通常時の1秒あたりの減少量")]
    public float baseDrainSpeed = 1f;
    [Tooltip("壁抜け時の減少速度の倍率")]
    public float phasingDrainMultiplier = 2f;

    [Header("UI設定")]
    [Tooltip("霊魂の残量を表示するUIオブジェクト（通常はImage）")]
    public Transform spiritBar;

    // ゲームオーバーを通知するイベント
    public static event Action OnSpiritDepleted;

    private Vector3 initialBarScale;
    private bool isPhasing = false; // 壁抜け中かどうかのフラグ

    void Start()
    {
        currentSpirit = maxSpirit;
        if (spiritBar != null)
        {
            initialBarScale = spiritBar.localScale;
        }
    }

    void Update()
    {
        if (currentSpirit <= 0)
        {
            return; // 霊魂が0以下なら何もしない
        }

        // 現在の減少速度を計算
        float currentDrainSpeed = baseDrainSpeed;
        if (isPhasing)
        {
            currentDrainSpeed *= phasingDrainMultiplier;
        }

        // 時間経過で霊魂を減らす
        currentSpirit -= currentDrainSpeed * Time.deltaTime;
        
        // UIバーのスケールを更新
        UpdateSpiritBar();

        // 霊魂が0になったらゲームオーバーイベントを発行
        if (currentSpirit <= 0)
        {
            currentSpirit = 0;
            Debug.Log("霊魂が尽きた...ゲームオーバー");
            OnSpiritDepleted?.Invoke();
        }
    }

    // UIバーの見た目を更新する
    private void UpdateSpiritBar()
    {
        if (spiritBar != null)
        {
            float percentage = currentSpirit / maxSpirit;
            spiritBar.localScale = new Vector3(initialBarScale.x * percentage, initialBarScale.y, initialBarScale.z);
        }
    }

    /// <summary>
    /// 壁抜け状態を外部から設定するためのメソッド
    /// </summary>
    public void SetPhasingState(bool isPhasing)
    {
        this.isPhasing = isPhasing;
    }
}