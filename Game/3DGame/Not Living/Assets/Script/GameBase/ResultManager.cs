// ResultManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultManager : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("表示するリザルト画面のCanvas")]
    public GameObject resultCanvas;
    [Tooltip("リザルト画面のタイトルテキスト (「ゲームオーバー」or「ゲームクリア」)")]
    public TextMeshProUGUI titleText;

    [Header("ランキング登録用UI")]
    public ApiManager apiManager;
    public TMP_InputField nameInputField;
    public UnityEngine.UI.Button submitButton;

    // --- ▼▼▼ エンディング遷移ボタンを追加 ▼▼▼ ---
    [Header("エンディング用")]
    [Tooltip("エンディングへ進むボタン")]
    public GameObject endingButton;


    private void OnEnable()
    {
        Debug.Log("ResultManagerが有効になりました。イベントを監視します。");
        // 2つのイベントを監視する
        ReikonManager.OnSpiritDepleted += HandleGameOver;
        GameEvents.OnGameClear += HandleGameClear; // ゲームクリアを監視
    }

    private void OnDisable()
    {
        Debug.Log("HandleGameOverが呼び出されました！");
        ReikonManager.OnSpiritDepleted -= HandleGameOver;
        GameEvents.OnGameClear -= HandleGameClear;
    }

    private void Start()
    {
        if (resultCanvas != null) resultCanvas.SetActive(false);
    }

    // ゲームオーバー時に呼ばれる
    private void HandleGameOver()
    {
        Debug.Log("ゲームオーバー処理を開始します。");
        if(titleText != null) titleText.text = "ゲームオーバー";
        if(endingButton != null) endingButton.SetActive(false); // ゲームオーバー時はエンディングボタンを非表示
        ShowResultScreen();
    }

    // ゲームクリア時に呼ばれる
    private void HandleGameClear()
    {
        Debug.Log("ゲームクリア処理を開始します。");
        if(titleText != null) titleText.text = "ゲームクリア";
        if(endingButton != null) endingButton.SetActive(true); // ゲームクリア時だけエンディングボタンを表示
        ShowResultScreen();
    }

    // リザルト画面を表示する共通処理
    private void ShowResultScreen()
    {
        Debug.Log("ShowResultScreenが呼び出されました！");
        if (resultCanvas != null) {
            Debug.Log("Canvasをアクティブにします。");
            resultCanvas.SetActive(true); 
            
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ランキング取得もここで行う
        if (apiManager != null) apiManager.GetRanking();
    }

    /// 「エンディングへ」ボタンから呼び出される
    public void ProceedToEnding()
    {
        // 各Managerから最終的な値を取得
        int finalScore = ScoreManager.Instance.CurrentScore;
        Vector3 finalAlignment = AlignmentManager.Instance.CurrentAlignment;
        float goodEvil = finalAlignment.y;
        float chaos = finalAlignment.z;

        Debug.Log($"最終スコア: {finalScore}, 最終座標: (Y={goodEvil}, Z={chaos})");

        // --- ここからエンディングの条件分岐 ---
        if (goodEvil <= -80 && chaos <= -50)
        {
            Debug.Log("エンディングA: Very Good End");
            SceneManager.LoadScene("Ending_A"); // Aのシーンへ
        }
        else if (goodEvil <= -50)
        {
            Debug.Log("エンディングB: Good End");
            SceneManager.LoadScene("Ending_B"); // Bのシーンへ
        }
        // ... 他のエンディング条件を追加 ...
        else
        {
            Debug.Log("エンディングE: Neutral End");
            SceneManager.LoadScene("Ending_E"); // Eのシーンへ
        }
    }

    public void SubmitScore()
    {
        // ApiManagerがセットされていなければ処理を中断
        if (apiManager == null)
        {
            Debug.LogError("ApiManagerが設定されていません！");
            return;
        }

        // 入力された名前と現在のスコアを取得
        string playerName = nameInputField.text;
        int score = ScoreManager.Instance.CurrentScore;

        // 名前が空欄の場合は送信しない
        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("プレイヤー名が入力されていません。");
            return; // ここで処理を中断
        }

        // ApiManagerを使ってスコアを送信
        apiManager.PostRanking(playerName, score);

        // 送信後はボタンと入力欄を無効化して、二重送信を防ぐ
        if (submitButton != null) submitButton.interactable = false;
        if (nameInputField != null) nameInputField.interactable = false;
    }
    
    // --- UIボタンから呼び出すためのメソッド ---
    public void RetryGame()
    {
        // 時間を元に戻してからシーンをリロード
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    /// ゲームを終了する
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}