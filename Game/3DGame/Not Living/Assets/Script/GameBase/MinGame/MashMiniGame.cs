using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro; // TextMeshProを使うために必要
using UnityEngine.EventSystems; // クリックイベントのインターフェース

public class MashMiniGame : MonoBehaviour, ITaskMiniGame, IPointerDownHandler
{
    public event Action<bool> OnTaskCompleted;

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

    private void Awake()
    {
        // AudioSourceを自分自身から取得、またはなければ追加する
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        
    }

    public void StartTask(TaskMachine machine)
    {
        // TaskMachineから難易度設定を受け取る
        requiredClicks = machine.SelectedDifficulty.mashQuota;
        
        // UIを初期化
        currentClicks = 0;
        if (progressBar != null)
        {
            progressBar.maxValue = requiredClicks;
            progressBar.value = 0;
        }
        UpdateStatusText();

        isGameActive = true;
        playText.text = $"左クリック 連打!!";
        StartCoroutine(TimerCoroutine());
    }
    
    /// <summary>
    /// IPointerDownHandlerインターフェースの実装。このUIがクリックされた時に呼ばれる。
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isGameActive) return;

        // クリック音を再生
        if (clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }

        // クリック回数を加算
        currentClicks++;
        
        // UIを更新
        if (progressBar != null)
        {
            progressBar.value = currentClicks;
        }
        UpdateStatusText();
        
        // ノルマを達成したら成功
        if (currentClicks >= requiredClicks)
        {
            isGameActive = false;
            OnTaskCompleted?.Invoke(true);
        }
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            statusText.text = $"{currentClicks} / {requiredClicks}";
        }
    }

    private IEnumerator TimerCoroutine()
    {
        float timer = timeLimit;
        while (timer > 0)
        {
            // (任意)残り時間を表示しても良い
            // statusText.text = $"残り時間: {timer:F1}";
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