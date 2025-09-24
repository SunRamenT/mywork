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

    [Header("ゲーム設定")]
    public float needleSpeed = 200f; // 針の回転速度（度/秒）
    public float successZoneWidth = 30f; // 成功ゾーンの角度
    public float startDelay = 0.5f; // 開始までの待機時間

    public void StartTask(TaskMachine machine)
    {
        StartCoroutine(SkillCheckRoutine());
    }

    private IEnumerator SkillCheckRoutine()
    {
        // 成功ゾーンをランダムな位置に設定
        successZone.rectTransform.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(0, 360));
        successZone.fillAmount = successZoneWidth / 360f;

        yield return new WaitForSeconds(startDelay);

        float timer = 0f;
        while (timer < 360f / needleSpeed) // 針が一周するまで
        {
            // 針を回転させる
            float angle = timer * needleSpeed;
            needle.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);

            // スペースキーが押されたら判定
            if (Input.GetKeyDown(KeyCode.Space))
            {
                float successMin = successZone.rectTransform.localEulerAngles.z;
                float successMax = successMin + successZoneWidth;
                
                // 角度の比較（一周をまたぐ場合も考慮）
                if (successMax >= 360)
                {
                    if (angle >= successMin || angle <= (successMax - 360))
                    {
                        OnTaskCompleted?.Invoke(true); // 成功
                        yield break; // コルーチン終了
                    }
                }
                else
                {
                    if (angle >= successMin && angle <= successMax)
                    {
                        OnTaskCompleted?.Invoke(true); // 成功
                        yield break; // コルーチン終了
                    }
                }
                
                OnTaskCompleted?.Invoke(false); // 失敗
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        OnTaskCompleted?.Invoke(false); // 時間切れで失敗
    }
}