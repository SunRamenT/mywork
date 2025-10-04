using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WorldTintController : MonoBehaviour
{
    [Header("ポストエフェクト設定")]
    [Tooltip("シーンに配置されているVolumeコンポーネント")]
    public Volume postProcessVolume;
    [Tooltip("善の状態の時の色（例：暖色系の黄色）")]
    public Color goodTintColor = new Color(1.0f, 0.9f, 0.7f, 1.0f);
    [Tooltip("悪の状態の時の色（例：退廃的な青紫色）")]
    public Color evilTintColor = new Color(0.8f, 0.7f, 1.0f, 1.0f);

    [Header("色調変更の条件")] // ▼▼▼ 追加 ▼▼▼
    [Tooltip("この善悪値の絶対値を超えたら色調を変化させ始める")]
    [Range(0, 100)]
    public float alignmentThreshold = 40f;
    [Tooltip("この日数（X日目）以降に色調を変化させ始める")]
    public int dayThreshold = 1;

    private ColorAdjustments colorAdjustments;

    private void Start()
    {
        if (postProcessVolume != null && postProcessVolume.profile.TryGet(out colorAdjustments))
        {
            // イベントへの登録
            if (AlignmentManager.Instance != null)
            {
                AlignmentManager.Instance.OnAlignmentChanged += HandleAlignmentChange;
            }
            if (GameTimeManager.Instance != null)
            {
                GameTimeManager.OnDayChanged += HandleDayChange;
            }
            
            // ゲーム開始時の状態で一度、表示を更新
            UpdateVisuals();
        }
        else
        {
            Debug.LogError("VolumeまたはColorAdjustmentsが見つかりません！", this);
        }
    }

    private void OnDestroy()
    {
        // イベントの登録解除
        if (AlignmentManager.Instance != null)
        {
            AlignmentManager.Instance.OnAlignmentChanged -= HandleAlignmentChange;
        }
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.OnDayChanged -= HandleDayChange;
        }
    }

    // AlignmentManagerからの通知で呼ばれる
    private void HandleAlignmentChange(Vector3 alignment)
    {
        UpdateVisuals();
    }

    // GameTimeManagerからの通知で呼ばれる
    private void HandleDayChange(int day)
    {
        UpdateVisuals();
    }

    /// <summary>
    /// 現在の善悪値と日付に基づいて、画面の色調を更新する
    /// </summary>
    private void UpdateVisuals()
    {
        if (colorAdjustments == null || AlignmentManager.Instance == null || GameTimeManager.Instance == null) return;

        float currentAlignment = AlignmentManager.Instance.CurrentAlignment.y;
        int currentDay = GameTimeManager.Instance.daysSurvived;

        // --- 条件判定 ---
        // 善悪値が閾値の範囲内、または指定した日数に達していない場合
        if (Mathf.Abs(currentAlignment) < alignmentThreshold || currentDay < dayThreshold)
        {
            // Color Adjustmentsエフェクト自体をオフにする
            colorAdjustments.active = false;
            return; // これで通常の色（白）になる
        }
        
        // --- 色計算 ---
        // 条件を満たした場合、エフェクトをオンにする
        colorAdjustments.active = true;
        
        // 善悪値がプラス（悪）の場合
        if (currentAlignment >= alignmentThreshold)
        {
            // 閾値から最大値までのどの位置にいるかを0～1で計算
            float t = Mathf.InverseLerp(alignmentThreshold, 100f, currentAlignment);
            colorAdjustments.colorFilter.value = Color.Lerp(Color.white, evilTintColor, t);
        }
        // 善悪値がマイナス（善）の場合
        else if (currentAlignment <= -alignmentThreshold)
        {
            // 閾値から最小値までのどの位置にいるかを0～1で計算
            float t = Mathf.InverseLerp(-alignmentThreshold, -100f, currentAlignment);
            colorAdjustments.colorFilter.value = Color.Lerp(Color.white, goodTintColor, t);
        }
    }
}