using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

public class SkillCheckMiniGame : MonoBehaviour, ITaskMiniGame
{
    public event Action<bool> OnTaskCompleted;

    [Header("UI要素")]
    [Tooltip("回転する針")]
    public Image needle;
    [Tooltip("成功ゾーンを1つだけ持つプレハブ")]
    public GameObject successZonePrefab;
    [Tooltip("生成した成功ゾーンを配置する親オブジェクト")]
    public Transform zonesParent;

    [Header("演出設定")]
    public float startDelay = 0.5f;

    // --- 内部変数 ---
    private List<Image> spawnedZones = new List<Image>();

    public void StartTask(TaskMachine machine)
    {
        StartCoroutine(SkillCheckRoutine(machine.SelectedDifficulty));
    }

    private IEnumerator SkillCheckRoutine(TaskDifficulty difficulty)
    {
        // --- 難易度設定を適用 ---
        float currentNeedleSpeed = difficulty.needleSpeed;
        float currentSuccessZoneWidth = difficulty.successZoneWidth;
        int zoneCount = difficulty.numberOfSuccessZones;

        // --- 成功ゾーンの生成と配置 ---
        // 前回のゾーンが残っていれば削除
        foreach (var zone in spawnedZones) { Destroy(zone.gameObject); }
        spawnedZones.Clear();

        for (int i = 0; i < zoneCount; i++)
        {
            // プレハブから成功ゾーンを生成
            GameObject zoneObj = Instantiate(successZonePrefab, zonesParent);
            Image zoneImage = zoneObj.GetComponent<Image>();
            
            // ゾーンの幅を設定
            zoneImage.fillAmount = currentSuccessZoneWidth / 360f;
            
            // ゾーンの開始位置をランダムに設定 (最初の45度は避ける)
            // 他のゾーンと重ならないように、配置可能な範囲を考慮
            float randomAngle = 45f + (360f - 45f) * ((float)i + UnityEngine.Random.value) / zoneCount;
            zoneImage.rectTransform.localEulerAngles = new Vector3(0, 0, randomAngle);
            
            spawnedZones.Add(zoneImage);
        }
        
        yield return new WaitForSeconds(startDelay);

        // --- ゲームループ ---
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float angle = elapsed * currentNeedleSpeed;
            needle.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);

            if (angle >= 360f)
            {
                OnTaskCompleted?.Invoke(false); // 時間切れで失敗
                yield break;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                float needleAngle = angle % 360;
                bool isSuccess = false;

                // 全ての成功ゾーンをチェック
                foreach (var zone in spawnedZones)
                {
                    float successMin = zone.rectTransform.localEulerAngles.z;
                    float successMax = successMin + currentSuccessZoneWidth;

                    // ゾーンが一周をまたぐかどうかの判定
                    if (successMax >= 360f)
                    {
                        successMax -= 360f;
                        if (needleAngle >= successMin || needleAngle <= successMax)
                        {
                            isSuccess = true;
                            break; // いずれかのゾーンに入っていれば成功
                        }
                    }
                    else
                    {
                        if (needleAngle >= successMin && needleAngle <= successMax)
                        {
                            isSuccess = true;
                            break; // いずれかのゾーンに入っていれば成功
                        }
                    }
                }

                OnTaskCompleted?.Invoke(isSuccess);
                yield break;
            }
            yield return null;
        }
    }
}