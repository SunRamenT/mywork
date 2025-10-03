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

    [Header("ゲーム設定")]
    [Tooltip("数字の最大値")]
    public int maxNumberValue = 99;
    [Tooltip("制限時間（秒）")]
    public float timeLimit = 10f;

    // --- 内部変数 ---
    private List<int> sortedNumbers;
    private int currentIndex;
    private Coroutine timerCoroutine;

    /// <summary>
    /// TaskMachineから呼び出される
    /// </summary>
    public void StartTask(TaskMachine machine)
    {
        // TaskMachineから渡された難易度情報でゲームを初期化
        InitializeGame(machine.SelectedDifficulty);
        // 制限時間を開始
        timerCoroutine = StartCoroutine(TimerCoroutine());
    }


    public void InitializeGame(TaskDifficulty difficulty)
    {
        currentIndex = 0;

        // 1. 昇順か降順かをランダムに決定
        bool isAscending = UnityEngine.Random.value > 0.5f;
        instructionText.text = isAscending ? "小さい順にクリック" : "大きい順にクリック";

        // 2. ユニークなランダムな数字のリストを生成
        HashSet<int> uniqueNumbers = new HashSet<int>();
        while (uniqueNumbers.Count < difficulty.numberOfButtons)
        {
            uniqueNumbers.Add(UnityEngine.Random.Range(1, maxNumberValue + 1));
        }

        // 3. 正解となる順番のリストを作成
        sortedNumbers = uniqueNumbers.ToList();
        if (isAscending) sortedNumbers.Sort();
        else sortedNumbers.Sort((a, b) => b.CompareTo(a));

        // 4. あらかじめ用意された全てのボタンを一度非表示にする
        allNumberButtons.ForEach(btn => btn.gameObject.SetActive(false));

        // 5. ボタンの配置場所をシャッフル
        List<Button> shuffledButtonSlots = allNumberButtons.OrderBy(b => UnityEngine.Random.value).ToList();
        List<int> numbersToPlace = uniqueNumbers.ToList();

        // 6. 難易度で指定された数だけ、シャッフルした場所にボタンを表示・設定する
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
    }

    private void OnNumberButtonClicked(int clickedNumber, Button clickedButton)
    {
        if (clickedNumber == sortedNumbers[currentIndex])
        {
            // 正解
            currentIndex++;
            clickedButton.interactable = false; // ボタンを無効化

            if (currentIndex >= sortedNumbers.Count)
            {
                if(timerCoroutine != null) StopCoroutine(timerCoroutine);
                OnTaskCompleted?.Invoke(true); // 成功
            }
        }
        else
        {
            // 不正解
            if(timerCoroutine != null) StopCoroutine(timerCoroutine);
            OnTaskCompleted?.Invoke(false); // 失敗
        }
    }

    private IEnumerator TimerCoroutine()
    {
        yield return new WaitForSeconds(timeLimit);
        OnTaskCompleted?.Invoke(false); // 時間切れで失敗
    }
}