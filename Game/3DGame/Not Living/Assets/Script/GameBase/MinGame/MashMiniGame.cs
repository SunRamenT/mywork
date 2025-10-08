using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro; // TextMeshProを使うために必要
using UnityEngine.EventSystems; // クリックイベントのインターフェース

public class MashMiniGame : MonoBehaviour, ITaskMiniGame, IPointerDownHandler
{
    public event Action<bool> OnTaskCompleted;

    [Header("クリック演出")]
    [Tooltip("クリック時に再生するアニメーションImage")]
    public Animator clickEffectAnimator; // ← AnimatorをCanvas上のImageにアタッチ
    [Tooltip("アニメーショントリガー名")]
    public string clickTriggerName = "Play"; // AnimatorのTriggerパラメータ名


    [Header("UI要素")]
    [Tooltip("残り時間やクリック回数を表示するテキスト")]
    public TextMeshProUGUI statusText;
    [Tooltip("進捗を示すスライダー")]
    public Slider progressBar;
    [Tooltip("クリック時に鳴らす効果音")]
    public AudioClip clickSound;
    
    [Header("ゲーム設定")]
    [Tooltip("制限時間（秒）")]
    public float timeLimit = 5f;

    public TextMeshProUGUI playText;

    // --- 内部変数 ---
    private int requiredClicks; // 難易度に応じたノルマ
    private int currentClicks = 0;
    private AudioSource audioSource;
    private bool isGameActive = false;
    [Tooltip("ゲーム開始前のカウントダウン秒数")]
    public float startCountdown = 3f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    public void StartTask(TaskMachine machine)
    {
        requiredClicks = machine.SelectedDifficulty.mashQuota;
        currentClicks = 0;

        if (progressBar != null)
        {
            progressBar.maxValue = requiredClicks;
            progressBar.value = 0;
        }

        isGameActive = false;
        playText.text = "";
        statusText.text = ""; // カウントダウンに使用するので一旦空白に

        // ✅ カウントダウンコルーチン開始
        StartCoroutine(StartCountdownCoroutine());
    }

    private IEnumerator StartCountdownCoroutine()
    {
        float countdown = startCountdown;

        // カウントダウン表示
        while (countdown > 0)
        {
            statusText.text = Mathf.CeilToInt(countdown).ToString(); // 3,2,1
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        // スタート表示
        statusText.text = "スタート!!";

        // ゲーム開始
        playText.text = "左クリック 連打!!";
        isGameActive = true;

        // タイマーコルーチン開始
        StartCoroutine(TimerCoroutine());
    }
    /// <summary>
    /// IPointerDownHandlerインターフェースの実装。このUIがクリックされた時に呼ばれる。
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isGameActive) return;

        // 効果音
        if (clickSound != null)
            audioSource.PlayOneShot(clickSound);

        // エフェクト再生
        if (clickEffectAnimator != null)
            clickEffectAnimator.SetTrigger(clickTriggerName);

        // カウント増加
        currentClicks++;
        if (progressBar != null)
            progressBar.value = currentClicks;

        // 進捗はスライダーで見えるため時間表示のみ更新
        UpdateStatusText(remainingTime: 5f);

        if (currentClicks >= requiredClicks)
        {
            isGameActive = false;
            OnTaskCompleted?.Invoke(true);
        }
    }

    private void UpdateStatusText(float remainingTime)
    {
        if (statusText == null) return;

        if (remainingTime >= 0)
        {
            statusText.text = $"残り: {Mathf.CeilToInt(remainingTime)}";
        }
        else
        {
            // 残り時間が指定されていないときは前の値を維持
        }
    }

    private IEnumerator TimerCoroutine()
    {
        float timer = timeLimit;
        while (timer > 0)
        {
            // (任意)残り時間を表示しても良い
            statusText.text = $"残り時間: {Mathf.CeilToInt(timer)}";
            timer -= Time.deltaTime;
            yield return null;
        }

        if (isGameActive)
        {
            // 時間切れで失敗
            isGameActive = false;
            OnTaskCompleted?.Invoke(false);
        }
    }
}