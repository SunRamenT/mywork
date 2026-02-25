using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NewsDisplay : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private AINewsCaster newsCaster;
    [SerializeField] private TextMeshProUGUI newsText; 
    [SerializeField] private GameObject newsPanel;

    [Header("設定")]
    [Tooltip("1ページあたりの表示時間（秒）")]
    [SerializeField] private float pageDisplayDuration = 2.5f;
    [Tooltip("1ページあたりの最大文字数")]
    [SerializeField] private int maxCharsPerPage = 50;

    // 現在実行中の表示コルーチンを保持し、競合を防ぐ（前回の改善点）
    private Coroutine currentDisplayRoutine;

    private void Start()
    {
        if (newsPanel != null) newsPanel.SetActive(false);
        if (newsText != null) newsText.text = "";
    }

    private void Update()
    {
        // ※最終的にはInputSystemへの移行を推奨
        if (Input.GetKeyDown(KeyCode.N)) 
        {
            RequestNews();
        }
    }

    public void RequestNews()
    {
        if (newsText != null) newsText.text = "ニュースじゅしんちゅう...";
        if (newsPanel != null) newsPanel.SetActive(true);

        if (newsCaster != null)
        {
            newsCaster.GenerateNews(ShowNews, HandleError);
        }
    }

    private void ShowNews(string generatedText)
    {
        // すでに別のニュースが表示中であれば、強制的に停止して新しいものを表示
        if (currentDisplayRoutine != null)
        {
            StopCoroutine(currentDisplayRoutine);
        }
        currentDisplayRoutine = StartCoroutine(DisplayRoutine(generatedText));
    }

    private void HandleError(string errorMsg)
    {
        if (currentDisplayRoutine != null)
        {
            StopCoroutine(currentDisplayRoutine);
        }
        // 世界観に合わせたエラー表示
        if (newsText != null) newsText.text = "つうしんえらーが はっせいしました。";
    }

    private IEnumerator DisplayRoutine(string fullText)
    {
        if (newsPanel != null) newsPanel.SetActive(true);

        // APIの出力に含まれる改行や不要な空白を削除（UI崩れ防止）
        fullText = fullText.Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("　", "");

        // テキストを50文字ずつに分割
        List<string> pages = new List<string>();
        for (int i = 0; i < fullText.Length; i += maxCharsPerPage)
        {
            int length = Mathf.Min(maxCharsPerPage, fullText.Length - i);
            pages.Add(fullText.Substring(i, length));
        }

        // 分割したページを一定間隔で切り替え表示
        foreach (string pageText in pages)
        {
            if (newsText != null) newsText.text = pageText;
            yield return new WaitForSeconds(pageDisplayDuration);
        }

        // 表示完了後の後処理
        if (newsPanel != null) newsPanel.SetActive(false);
        if (newsText != null) newsText.text = "";
        
        currentDisplayRoutine = null;
    }
}