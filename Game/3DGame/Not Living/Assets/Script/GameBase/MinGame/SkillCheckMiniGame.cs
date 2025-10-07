using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro; 
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

    public TextMeshProUGUI successCountText;
    public TextMeshProUGUI playText;
    AudioSource audioSource;
    public AudioClip successClip;
 
    public void StartTask(TaskMachine machine)
    {
        StartCoroutine(SkillCheckRoutine(machine.SelectedDifficulty));
        successCountText.text = $"成功数:";
        playText.text = $"左クリック";
        audioSource = GetComponent<AudioSource>();
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
            
            // ゾーンの開始位置をランダムに設定 (最初の90度は避ける)
            // 他のゾーンと重ならないように、配置可能な範囲を考慮
            float randomAngle = 0f + (360f - 90f) * ((float)i + UnityEngine.Random.value) / zoneCount;
            zoneImage.rectTransform.localEulerAngles = new Vector3(0, 0, randomAngle);
            
            spawnedZones.Add(zoneImage);
        }
        
        yield return new WaitForSeconds(startDelay);

        // --- ゲームループ ---
        float elapsed = 0f;
        int consecutiveSuccess = 0; // 連続成功カウント

        int requiredSuccess = difficulty.numberOfSuccessZones; // 例えば3回連続成功が必要

        while (true)
        {
            elapsed += Time.deltaTime;
            float angle = elapsed * currentNeedleSpeed;
            needle.rectTransform.localEulerAngles = new Vector3(0, 0, -angle); // 
            
            // 360度回ったら失敗として終了
            if (angle >= 360f)
            {
                OnTaskCompleted?.Invoke(false);
                yield break;
            }

            if (Input.GetButtonDown("Fire1"))
            {
                float needleAngle = (-angle) % 360f;
                if (needleAngle < 0) needleAngle += 360f;

                bool hit = false;

                // 全てのゾーンをチェック
                foreach (var zone in spawnedZones)
                {
                    float successMin = zone.rectTransform.localEulerAngles.z - 10f; // ゾーン少し広め
                    float successMax = (successMin + currentSuccessZoneWidth) % 360f;

                    if (successMax >= successMin)
                        hit = needleAngle >= successMin && needleAngle <= successMax;
                    else
                        hit = needleAngle >= successMin || needleAngle <= successMax;

                    if (hit) break;
                }

                if (hit)
                {
                    consecutiveSuccess++;
                    if (audioSource != null && successClip != null)
                        audioSource.PlayOneShot(successClip);

                    // --- UI 更新 ---
                    if (successCountText != null)
                        successCountText.text = $"成功数: {consecutiveSuccess}";

                    if (consecutiveSuccess >= requiredSuccess)
                    {
                        OnTaskCompleted?.Invoke(true);
                        yield break;
                    }
                }
                else
                {
                    OnTaskCompleted?.Invoke(false); // 一度でも失敗したら終了
                    yield break;
                }
            }


            yield return null;
        }
    }
}