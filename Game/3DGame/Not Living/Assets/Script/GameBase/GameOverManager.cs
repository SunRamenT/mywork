using UnityEngine;
using UnityEngine.SceneManagement; // シーンをリロードするために必要
using TMPro; 

public class GameOverManager : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("表示するゲームオーバー画面のCanvas")]
    public GameObject gameOverCanvas;

    [Header("ランキング登録用UI")]
    [Tooltip("ApiManagerがアタッチされたGameObject")]
    public ApiManager apiManager;
    [Tooltip("プレイヤー名を入力するInputField")]
    public TMP_InputField nameInputField;
    [Tooltip("スコア送信ボタン")]
    public UnityEngine.UI.Button submitButton;

    private void OnEnable()
    {
        // ReikonManagerのイベントに、自身のゲームオーバー処理メソッドを登録
        ReikonManager.OnSpiritDepleted += HandleGameOver;
    }

    private void OnDisable()
    {
        // オブジェクトが破棄される際に、登録を解除
        ReikonManager.OnSpiritDepleted -= HandleGameOver;
    }

    private void Start()
    {
        // ゲーム開始時は、必ずゲームオーバー画面を非表示にしておく
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// 霊魂が0になった時に呼び出される
    /// </summary>
    private void HandleGameOver()
    {
        Debug.Log("ゲームオーバー処理を開始します。");

        // 1. ゲームオーバー画面を表示する
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }

        // 2. ゲームの時間を完全に停止する
        Time.timeScale = 0f;

        // 3. マウスカーソルを表示して、操作できるようにする
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        if(submitButton != null) submitButton.interactable = false;
        if(nameInputField != null) nameInputField.interactable = false;
    }

    // --- UIボタンから呼び出すためのメソッド ---

    /// <summary>
    /// 現在のシーンをリロードして、ゲームをリトライする
    /// </summary>
    public void RetryGame()
    {
        // 時間を元に戻してからシーンをリロード
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// ゲームを終了する
    /// </summary>
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}