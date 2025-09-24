using UnityEngine;

// IInteractableインターフェースを実装
public class TaskMachine : MonoBehaviour, IInteractable
{
    [Header("タスク設定")]
    [Tooltip("この機械が提供するミニゲームのUIプレハブ")]
    public GameObject miniGamePrefab;
    [Tooltip("成功時の霊魂報酬")]
    public int soulReward = 50;

    [Header("UI設定")]
    [Tooltip("ミニゲームUIを配置するCanvas")]
    public Canvas targetCanvas;

    private ITaskMiniGame currentMiniGame;

    /// <summary>
    /// PlayerControllerから、プレイヤーが検知範囲に入った時に呼び出される
    /// </summary>
    public void OnPlayerEnterRange()
    {
        // ▼▼▼ デバッグログを追加 ▼▼▼
        Debug.Log($"<color=green>[TaskMachine] プレイヤーが {this.gameObject.name} の範囲内に入りました。</color>", this.gameObject);
        // ここで「SHIFTキーでタスク開始」などのUIヒントを表示しても良い
    }

    /// <summary>
    /// PlayerControllerから、プレイヤーが検知範囲から出た時に呼び出される
    /// </summary>
    public void OnPlayerExitRange()
    {
        // ▼▼▼ デバッグログを追加 ▼▼▼
        Debug.Log($"<color=orange>[TaskMachine] プレイヤーが {this.gameObject.name} の範囲外に出ました。</color>", this.gameObject);
        // UIヒントを非表示にする
    }

    /// <summary>
    /// PlayerControllerから、インタラクトキーが押された時に呼び出される
    /// </summary>
    public void OnInteract(PlayerController playerController)
    {
        // ▼▼▼ デバッグログを追加 ▼▼▼
        Debug.Log($"<color=cyan>[TaskMachine] {this.gameObject.name} でインタラクトが実行されました。タスクを開始します。</color>", this.gameObject);
        ActivateTask();
    }
    
    public void ActivateTask()
    {
        if (currentMiniGame != null) return;

        if (miniGamePrefab != null && targetCanvas != null)
        {
            GameStateManager.Instance.SetState(GameStateManager.GameState.MiniGameActive);

            GameObject miniGameInstance = Instantiate(miniGamePrefab, targetCanvas.transform);
            currentMiniGame = miniGameInstance.GetComponent<ITaskMiniGame>();
            
            if (currentMiniGame != null)
            {
                currentMiniGame.OnTaskCompleted += HandleTaskCompletion;
                currentMiniGame.StartTask(this);
            }
            else
            {
                Debug.LogError($"<color=red>エラー:</color> {miniGamePrefab.name} プレハブにITaskMiniGameを実装したスクリプト（SkillCheckMiniGameなど）がアタッチされていません！", this.gameObject);
            }
        }
        else
        {
            Debug.LogError("<color=red>エラー:</color> TaskMachineのMiniGamePrefabまたはTargetCanvasが設定されていません。", this.gameObject);
        }
    }

    private void HandleTaskCompletion(bool success)
    {
        if (success)
        {
            Debug.Log("タスク成功！ 霊魂を獲得。");
            FindFirstObjectByType<ReikonManager>()?.Heal(soulReward);
            GameEvents.TriggerGoodDeedPerformed();
        }
        else
        {
            Debug.Log("タスク失敗...");
        }

        currentMiniGame.OnTaskCompleted -= HandleTaskCompletion;
        Destroy((currentMiniGame as MonoBehaviour).gameObject);
        currentMiniGame = null;

        GameStateManager.Instance.SetState(GameStateManager.GameState.Gameplay);
    }
}