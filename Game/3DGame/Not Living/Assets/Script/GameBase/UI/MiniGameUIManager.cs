using UnityEngine;

public class MiniGameUIManager : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("ミニゲーム用のUI要素全体をまとめているCanvasのGameObject")]
    public GameObject miniGameCanvasObject;

    private void Start()
    {
        // GameStateManagerのイベントに、自身の状態更新メソッドを登録
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged += HandleGameStateChange;
            // ゲーム開始時の状態で一度更新
            HandleGameStateChange(GameStateManager.Instance.CurrentState);
        }
        else
        {
            Debug.LogError("GameStateManagerがシーンに存在しません！");
            // 初期状態では非表示にしておく
            if (miniGameCanvasObject != null)
            {
                miniGameCanvasObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される際にイベントの登録を解除
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleGameStateChange;
        }
    }

    /// <summary>
    /// GameStateの変更を検知して、Canvasの表示/非表示を切り替える
    /// </summary>
    private void HandleGameStateChange(GameStateManager.GameState newState)
    {
        if (miniGameCanvasObject != null)
        {
            // 新しい状態が「ミニゲーム実行中」であればCanvasを表示、そうでなければ非表示
            bool shouldBeActive = (newState == GameStateManager.GameState.MiniGameActive);
            miniGameCanvasObject.SetActive(shouldBeActive);
        }
    }
}