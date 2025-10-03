// SkillCheckMiniGame.cs
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public class SkillCheckMiniGame : MonoBehaviour, ITaskMiniGame
{
    public event Action<bool> OnTaskCompleted;

    [Header("UI要素")]
    public Image needle; // 回転する針
    public Image successZone; // 成功ゾーン

    [Header("デフォルト設定")]
    [Tooltip("難易度設定が見つからない場合のフォールバック値")]
    public float defaultNeedleSpeed = 200f;
    [Tooltip("難易度設定が見つからない場合のフォールバック値")]
    public float defaultSuccessZoneWidth = 30f;
    public float startDelay = 0.5f;
    public void StartTask(TaskMachine machine)
    {
        // TaskMachineから難易度設定を受け取る
        TaskDifficulty difficulty = machine.SelectedDifficulty;
        StartCoroutine(SkillCheckRoutine(difficulty));
    }

    private IEnumerator SkillCheckRoutine(TaskDifficulty difficulty)
    {
        // --- 難易度設定を適用 ---
        // difficultyがnullの場合は、Inspectorのデフォルト値を使用
        float currentNeedleSpeed = difficulty?.needleSpeed ?? defaultNeedleSpeed;
        float currentSuccessZoneWidth = difficulty?.successZoneWidth ?? defaultSuccessZoneWidth;

        // --- 成功ゾーンの初期化 ---
        // 最初の90度（0-90）を避けるため、91度から360度の範囲でランダムな開始位置を決める
        float zoneStart = UnityEngine.Random.Range(90f, 360f);
        successZone.rectTransform.localEulerAngles = new Vector3(0, 0, zoneStart);
        successZone.fillAmount = currentSuccessZoneWidth / 360f;

        yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        while (true) // 1周したら失敗するロジックに変更
        {
            elapsed += Time.deltaTime;
            float angle = elapsed * currentNeedleSpeed;

            // 針を回転させる
            needle.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);
            
            // 1周（360度）回ったら時間切れで失敗
            if (angle >= 360f)
            {
                OnTaskCompleted?.Invoke(false);
                yield break;
            }

            // スペースキーが押されたら判定
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // 現在の針の角度を0-360の範囲で正規化
                float needleAngle = angle % 360;

                // 成功ゾーンの開始角度と終了角度を計算
                float successMin = zoneStart;
                float successMax = zoneStart + currentSuccessZoneWidth;
                
                bool isSuccess;
                // 成功ゾーンが1周をまたぐ場合（例: 350度から20度）
                if (successMax >= 360f)
                {
                    successMax -= 360f;
                    isSuccess = (needleAngle >= successMin || needleAngle <= successMax);
                }
                else // 通常の場合
                {
                    isSuccess = (needleAngle >= successMin && needleAngle <= successMax);
                }

                OnTaskCompleted?.Invoke(isSuccess);
                yield break; // 判定が終わったらコルーチン終了
            }
            
            yield return null;
        }
    }
}