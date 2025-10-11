using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class NumberOrderMiniGame : MonoBehaviour, ITaskMiniGame
{
    public event Action<bool> OnTaskCompleted;

    [Header("UI要素")]
    [Tooltip("指示を表示するテキスト")]
    public TextMeshProUGUI instructionText;
    [Tooltip("あらかじめ配置されている、全てのボタンのリスト")]
    public List<Button> allNumberButtons;

    [Header("制限時間表示")]
    [Tooltip("残り時間を表示するテキスト")]
    public TextMeshProUGUI remainingTimeText;

    [Header("ゲーム設定")]
    [Tooltip("数字の最大値")]
    public int maxNumberValue = 99;
    [Tooltip("制限時間（秒）")]
    public float timeLimit = 10f;

    private List<int> sortedNumbers;
    private int currentIndex;
    private Coroutine timerCoroutine;

    private AudioSource audioSource;
    [Header("オーディオ")] // ▼▼▼ 変更 ▼▼▼
    public AudioClip hitClip;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    public void StartTask(TaskMachine machine)
    {
        InitializeGame(machine.SelectedDifficulty);
        timerCoroutine = StartCoroutine(TimerCoroutine());
    }

    public void InitializeGame(TaskDifficulty difficulty)
    {
        currentIndex = 0;

        // 昇順/降順ランダム
        bool isAscending = UnityEngine.Random.value > 0.5f;
        instructionText.text = isAscending ? "小さい順にクリック" : "大きい順にクリック";

        // ユニークなランダム数字生成
        HashSet<int> uniqueNumbers = new HashSet<int>();
        while (uniqueNumbers.Count < difficulty.numberOfButtons)
        {
            uniqueNumbers.Add(UnityEngine.Random.Range(1, maxNumberValue + 1));
        }

        // 正解順リスト
        sortedNumbers = uniqueNumbers.ToList();
        if (isAscending) sortedNumbers.Sort();
        else sortedNumbers.Sort((a, b) => b.CompareTo(a));

        // 全ボタン非表示
        allNumberButtons.ForEach(btn => btn.gameObject.SetActive(false));

        // 配置シャッフル
        List<Button> shuffledButtonSlots = allNumberButtons.OrderBy(b => UnityEngine.Random.value).ToList();
        List<int> numbersToPlace = uniqueNumbers.ToList();

        for (int i = 0; i < difficulty.numberOfButtons; i++)
        {
            Button buttonToShow = shuffledButtonSlots[i];
            int number = numbersToPlace[i];

            buttonToShow.gameObject.SetActive(true);
            buttonToShow.interactable = true;
            buttonToShow.GetComponentInChildren<TextMeshProUGUI>().text = number.ToString();

            buttonToShow.onClick.RemoveAllListeners();
            buttonToShow.onClick.AddListener(() => OnNumberButtonClicked(number, buttonToShow));
        }

        // 残り時間表示を初期化
        if (remainingTimeText != null)
            remainingTimeText.text = $"{timeLimit:F1}";
    }

    private void OnNumberButtonClicked(int clickedNumber, Button clickedButton)
    {
        if (clickedNumber == sortedNumbers[currentIndex])
        {
            currentIndex++;
            clickedButton.interactable = false;

            if (audioSource != null && hitClip != null)
                audioSource.PlayOneShot(hitClip);

            if (currentIndex >= sortedNumbers.Count)
            {
                if (timerCoroutine != null) StopCoroutine(timerCoroutine);
                OnTaskCompleted?.Invoke(true);
            }
        }
        else
        {
            if (timerCoroutine != null) StopCoroutine(timerCoroutine);
            OnTaskCompleted?.Invoke(false);
        }
    }

    private IEnumerator TimerCoroutine()
    {
        float timer = timeLimit;

        while (timer > 0f)
        {
            timer -= Time.deltaTime;

            // 残り時間表示更新
            if (remainingTimeText != null)
                remainingTimeText.text = $"{Mathf.CeilToInt(timer)}"; // 小数点なしで切り上げ

            yield return null;
        }

        OnTaskCompleted?.Invoke(false);
    }
}
