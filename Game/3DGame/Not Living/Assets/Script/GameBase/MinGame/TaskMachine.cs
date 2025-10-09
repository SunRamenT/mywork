using UnityEngine;
using System.Collections.Generic; // Listを使うために必要
using TMPro; 

// --- 難易度設定用のクラス定義 ---
[System.Serializable]
public class TaskDifficulty
{
    public string difficultyName = "Easy";

    [Header("Number Order Game")]
    public int numberOfButtons = 3;

    [Header("Skill Check Game")]
    public float needleSpeed = 200f;
    public float successZoneWidth = 45f;
    [Tooltip("スキルチェックで表示される成功ゾーンの数")] // ▼▼▼ 名前を変更 ▼▼▼
    public int numberOfSuccessZones = 1;

    [Header("Mash Game")] // ▼▼▼ 追加 ▼▼▼
    [Tooltip("連打ゲームのノルマ回数")]
    public int mashQuota = 15;

    // ▼▼▼ 以下をクラスの末尾に追加 ▼▼▼
    [Header("Runner Game")]
    [Tooltip("クリアまでの生存時間（秒）")]
    public float survivalTime = 15f;
    [Tooltip("地面や障害物が流れてくる速さ")]
    public float scrollSpeed = 400f;
    // ▲▲▲ ここまで追加 ▲▲▲


    [Header("Common Reward")]
    public int soulReward = 25;
}

// IInteractableインターフェースを実装
public class TaskMachine : MonoBehaviour, IInteractable
{
    [Header("タスク設定")]
    [Tooltip("この機械が提供するミニゲームのUIプレハブのリスト")] // ▼▼▼ 複数形に変更 ▼▼▼
    public List<GameObject> miniGamePrefabs;
    [Tooltip("この機械で選択される可能性のある難易度のリスト")] // ▼▼▼ 追加 ▼▼▼
    public List<TaskDifficulty> possibleDifficulties;

    [Header("UI設定")]
    [Tooltip("ミニゲームUIを配置するCanvas")]
    public Canvas targetCanvas;
    [Tooltip("ゲーム開始前に表示する、名前と難易度告知用のUIプレハブ")] // ▼▼▼ 追加 ▼▼▼
    public GameObject introUIPrefab;
    [Tooltip("告知UIを表示する時間（秒）")] // ▼▼▼ 追加 ▼▼▼
    public float introDisplayDuration = 2.0f;
    AudioSource audioSource;
    public AudioClip failClip;

    private ITaskMiniGame currentMiniGame;

     // ▼▼▼ 選択された難易度を保持するプロパティを追加 ▼▼▼
    public TaskDifficulty SelectedDifficulty { get; private set; }



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

    // OnInteractはコルーチンを直接開始できないため、ラッパーメソッドを呼ぶ
    public void OnInteract(PlayerController playerController)
    {
        // 既にミニゲームが実行中でないか確認
        if (currentMiniGame != null) return;
        
        StartCoroutine(StartMiniGameSequence());
    }
    private System.Collections.IEnumerator StartMiniGameSequence()
    {
        if (miniGamePrefabs == null || miniGamePrefabs.Count == 0 || possibleDifficulties == null || possibleDifficulties.Count == 0)
        {
            Debug.LogError("ミニゲームのプレハブまたは難易度が設定されていません！", this);
            yield break; // コルーチンを終了
        }

        // --- 1. ゲームと難易度を決定 ---
        SelectedDifficulty = possibleDifficulties[Random.Range(0, possibleDifficulties.Count)];
        GameObject selectedMiniGamePrefab = miniGamePrefabs[Random.Range(0, miniGamePrefabs.Count)];
        
        // --- 2. ゲームの状態を切り替え、マウスを表示 ---
        GameStateManager.Instance.SetState(GameStateManager.GameState.MiniGameActive);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // --- 3. 紹介UIを表示し、一定時間待機 ---
        if (introUIPrefab != null)
        {
            GameObject introInstance = Instantiate(introUIPrefab, targetCanvas.transform);
            MiniGameIntroUI introUI = introInstance.GetComponent<MiniGameIntroUI>();
            if (introUI != null)
            {
                // UIにゲーム名と難易度を渡して表示させる
                introUI.ShowIntro(selectedMiniGamePrefab.name, SelectedDifficulty.difficultyName, introDisplayDuration);
            }
            
            // UIが表示されている間、待機する
            yield return new WaitForSeconds(introDisplayDuration);
        }

        // --- 4. 実際のミニゲームを開始 ---
        GameObject miniGameInstance = Instantiate(selectedMiniGamePrefab, targetCanvas.transform);
        currentMiniGame = miniGameInstance.GetComponent<ITaskMiniGame>();
        
        if (currentMiniGame != null)
        {
            currentMiniGame.OnTaskCompleted += HandleTaskCompletion;
            currentMiniGame.StartTask(this);
        }
    }
    private void HandleTaskCompletion(bool success)
    {
        if (success)
        {
            Debug.Log("タスク成功！ 霊魂を獲得。");
            // 選択された難易度に応じた報酬を与える
            FindFirstObjectByType<ReikonManager>()?.Heal(SelectedDifficulty.soulReward);
            GameEvents.TriggerGoodDeedPerformed();
        }
        else
        {
            if (audioSource != null && failClip != null)
                audioSource.PlayOneShot(failClip);
            Debug.Log("タスク失敗...");
        }

        // マウスカーソルを元に戻し、ゲームの状態を再開
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameStateManager.Instance.SetState(GameStateManager.GameState.Gameplay);

        currentMiniGame.OnTaskCompleted -= HandleTaskCompletion;
        Destroy((currentMiniGame as MonoBehaviour).gameObject);
        currentMiniGame = null;
    }
}