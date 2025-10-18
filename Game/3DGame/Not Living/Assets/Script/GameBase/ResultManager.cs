// ResultManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultManager : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("表示するリザルト画面のCanvas")]
    public GameObject resultCanvas;
    [Tooltip("表示するスコア画面のCanvas")]
    public GameObject scoreCanvas;
    [Tooltip("リザルト画面のタイトルテキスト (「ゲームオーバー」or「ゲームクリア」)")]
    public TextMeshProUGUI titleText;

    [Header("ランキング登録用UI")]
    public ApiManager apiManager;
    public TMP_InputField nameInputField;
    public UnityEngine.UI.Button submitButton;

    // --- スコア画面遷移ボタンを追加 ---
    [Header("スコア画面用")]
    [Tooltip("スコア画面へ進むボタン")]
    public GameObject scoreButton;

    


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
        if (scoreCanvas != null) scoreCanvas.SetActive(false);

        // もしInspectorでApiManagerが設定されていなかったら
        if (apiManager == null)
        {
            // シーンに存在するApiManagerのインスタンスを自動で探してセットする
            apiManager = ApiManager.Instance; 
        }
    }

    // ゲームオーバー時に呼ばれる
    private void HandleGameOver()
    {
        Debug.Log("ゲームオーバー処理を開始します。");
        if(titleText != null) titleText.text = "ゲームオーバー";
        ShowResultScreen();
    }

    // ゲームクリア時に呼ばれる
    private void HandleGameClear()
    {
        Debug.Log("ゲームクリア処理を開始します。");
        if(titleText != null) titleText.text = "ゲームクリア";
        
        ShowResultScreen();
    }

    // リザルト画面を表示する共通処理
    private void ShowResultScreen()
    {
        Debug.Log("ShowResultScreenが呼び出されました！");
        if (resultCanvas != null || GameStateManager.Instance != null) {
            Debug.Log("Canvasをアクティブにします。");
            resultCanvas.SetActive(true); 
            GameStateManager.Instance.SetState(GameStateManager.GameState.End);
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 現在のスコアを取得してハイスコアを更新・保存する
        HighScoreManager.Instance.SaveHighScore(ScoreManager.Instance.CurrentScore);

        // ランキング取得もここで行う
        if (apiManager != null) apiManager.GetRanking();
    }

    /// 「エンディングへ」ボタンから呼び出される
    public void ProceedToScore()
    {
        resultCanvas.SetActive(false);
        scoreCanvas.SetActive(true); 
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