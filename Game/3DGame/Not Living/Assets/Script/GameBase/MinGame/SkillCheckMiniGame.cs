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
    [Tooltip("ゲーム開始前のカウントダウン秒数")]
    public float startCountdown = 3f;
    [Tooltip("針が動き出すまでのディレイ")]
    public float startDelay = 0.5f;

    private List<Image> spawnedZones = new List<Image>();

    [Header("テキストUI")]
    public TextMeshProUGUI successCountText;   // 成功数表示
    public TextMeshProUGUI playText;           // 「クリックで止めろ！」など表示
    public TextMeshProUGUI countdownText;      // ✅ カウントダウン専用テキスト

    private AudioSource audioSource;
    public AudioClip successClip;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    public void StartTask(TaskMachine machine)
    {
        successCountText.text = "成功数:";
        playText.text = "";
        countdownText.text = ""; // 初期化
        StartCoroutine(SkillCheckRoutine(machine.SelectedDifficulty));
    }

    private IEnumerator SkillCheckRoutine(TaskDifficulty difficulty)
    {
        // --- 難易度設定 ---
        float currentNeedleSpeed = difficulty.needleSpeed;
        float currentSuccessZoneWidth = difficulty.successZoneWidth;
        int zoneCount = difficulty.numberOfSuccessZones;

        // --- 成功ゾーンの生成 ---
        foreach (var zone in spawnedZones) Destroy(zone.gameObject);
        spawnedZones.Clear();

        playText.text = "左クリック!";

        for (int i = 0; i < zoneCount; i++)
        {
            GameObject zoneObj = Instantiate(successZonePrefab, zonesParent);
            Image zoneImage = zoneObj.GetComponent<Image>();
            zoneImage.fillAmount = currentSuccessZoneWidth / 360f;

            float randomAngle = (360f - 90f) * ((float)i + UnityEngine.Random.value) / zoneCount;
            zoneImage.rectTransform.localEulerAngles = new Vector3(0, 0, randomAngle);
            spawnedZones.Add(zoneImage);
        }

        // --- ✅ カウントダウン処理 ---
        float countdown = startCountdown;
        while (countdown > 0)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(countdown).ToString(); // 3,2,1表示
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }


        if (countdownText != null)
            countdownText.text = "スタート!!";


        if (countdownText != null)
            countdownText.text = ""; // カウントダウンを消す

        // --- ゲーム開始 ---
        float elapsed = 0f;
        int consecutiveSuccess = 0;
        int requiredSuccess = difficulty.numberOfSuccessZones;

        while (true)
        {
            elapsed += Time.deltaTime;
            float angle = elapsed * currentNeedleSpeed;
            needle.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);

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
                foreach (var zone in spawnedZones)
                {
                    float successMin = zone.rectTransform.localEulerAngles.z - 10f;
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

                    successCountText.text = $"成功数: {consecutiveSuccess}";

                    if (consecutiveSuccess >= requiredSuccess)
                    {
                        OnTaskCompleted?.Invoke(true);
                        yield break;
                    }
                }
                else
                {
                    OnTaskCompleted?.Invoke(false);
                    yield break;
                }
            }

            yield return null;
        }
    }
}
