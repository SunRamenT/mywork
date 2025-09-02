using UnityEngine;

/// <summary>
/// GameTimeManagerと連携して、昼夜のライティングを管理するクラス
/// </summary>
public class LightingManager : MonoBehaviour
{
    [Header("参照オブジェクト")]
    [Tooltip("シーン内の太陽 Directional Light")]
    public Light sun;

    [Header("時間帯ごとの色設定")]
    [Tooltip("時間帯による環境光の色")]
    public Gradient ambientColor;
    [Tooltip("時間帯による太陽光の色")]
    public Gradient directionalLightColor;
    [Tooltip("時間帯による太陽の光の強さ")]
    public AnimationCurve sunIntensity;

    private void OnEnable()
    {
        // GameTimeManagerのイベントに、ライティング更新処理を登録
        GameTimeManager.OnTimeChanged += UpdateLighting;
    }

    private void OnDisable()
    {
        // オブジェクトが無効になったら、イベントから登録解除
        GameTimeManager.OnTimeChanged -= UpdateLighting;
    }

    /// <summary>
    /// 時間の変更に応じてライティングを更新する
    /// </summary>
    private void UpdateLighting(int hour, int minute)
    {
        // 現在の時間を1日のうちの割合（0～1）に変換
        // 1日は1440分 (24時間 * 60分)
        float timeFraction = (hour * 60f + minute) / 1440f;

        // 設定したグラデーションとカーブから現在の値を取得
        RenderSettings.ambientLight = ambientColor.Evaluate(timeFraction);
        sun.color = directionalLightColor.Evaluate(timeFraction);
        sun.intensity = sunIntensity.Evaluate(timeFraction);

        // 太陽の角度を時間に応じて回転させる
        // 0時(0.0)と24時(1.0)で真下、12時(0.5)で真上を向くように調整
        float sunAngle = timeFraction * 360f;
        sun.transform.rotation = Quaternion.Euler(new Vector3(sunAngle - 90f, 170f, 0));
    }
}