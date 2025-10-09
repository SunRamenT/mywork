using UnityEngine;
using System.Collections.Generic; // Listを使うために必要
using TMPro; 

// --- 難易度設定用のクラス定義 ---
[System.Serializable]
public class TaskDifficulty
{
    public string difficultyName = "Easy";

    [Tooltip("この難易度が選択される重み。値が大きいほど選ばれやすい。")]
    public int selectionWeight = 1;

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
    public int sparkCount = 3;


    [Header("Common Reward")]
    public int soulReward = 25;
}

// IInteractableインターフェースを実装
public class TaskMachine : MonoBehaviour, IInteractable
{
    [Header("頻度設定")]
    [Tooltip("このマシンが最後に使用された日付を記録します（デバッグ用）")]
    [SerializeField] private int dayLastUsed = -1; // -1は「まだ一度も使われていない」ことを示す
    [Header("タスク設定")]
    [Tooltip("この機械が提供するミニゲームのUIプレハブのリスト")] // ▼▼▼ 複数形に変更 ▼▼▼

    [Header("ワールド空間UI")]
    public GameObject statusUIPrefab;
    [Tooltip("オブジェクトの基点からのUIのオフセット")]
    public Vector3 statusUIOffset = new Vector3(0, 1.5f, 0);
    // ▼▼▼ 変更点 ▼▼▼
    private GameObject _statusUIInstance; // UIのインスタンスを保持
    private TextMeshProUGUI _statusUIText;


    public List<GameObject> miniGamePrefabs;
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

    // ▼▼▼ Start, OnEnable, OnDisable, HandleDayChangeメソッドを追加 ▼▼▼
    void Start()
    {
        if (statusUIPrefab != null)
        {
            // ▼▼▼ 変更点: 生成したUIを保持し、最初は非表示にする ▼▼▼
            _statusUIInstance = Instantiate(statusUIPrefab, transform);
            _statusUIInstance.transform.localPosition = statusUIOffset;
            _statusUIText = _statusUIInstance.GetComponentInChildren<TextMeshProUGUI>();
            _statusUIInstance.SetActive(false); // ★最初は非表示
        }
        UpdateStatusUI();
    }

    void OnEnable()
    {
        // 日付変更イベントを購読
        GameTimeManager.OnDayChanged += HandleDayChange;
    }

    void OnDisable()
    {
        // 日付変更イベントの購読を解除
        GameTimeManager.OnDayChanged -= HandleDayChange;
    }

    // 日付が変わったときに呼び出される
    private void HandleDayChange(int newDay)
    {
        UpdateStatusUI();
    }
    
    // ▼▼▼ 新しいメソッドを追加 ▼▼▼
    /// <summary>
    /// 頭上UIのテキストを現在の状態で更新する
    /// </summary>
    private void UpdateStatusUI()
    {
        if (_statusUIText == null) return;

        int currentDay = GameTimeManager.Instance.daysSurvived;
        if (dayLastUsed < currentDay)
        {
            _statusUIText.text = "右クリック"; // 未使用時のテキスト
        }
        else
        {
            _statusUIText.text = "使用済み"; // 使用済み時のテキスト
        }
    }


    /// <summary>
    /// PlayerControllerから、プレイヤーが検知範囲に入った時に呼び出される
    /// </summary>
     // ▼▼▼ OnPlayerEnterRangeメソッドを変更 ▼▼▼
    public void OnPlayerEnterRange()
    {
        Debug.Log($"<color=green>[TaskMachine] プレイヤーが {this.gameObject.name} の範囲内に入りました。</color>", this.gameObject);
        if (_statusUIInstance != null)
        {
            _statusUIInstance.SetActive(true); // UIを表示する
        }
    }

    // ▼▼▼ OnPlayerExitRangeメソッドを変更 ▼▼▼
    public void OnPlayerExitRange()
    {
        Debug.Log($"<color=orange>[TaskMachine] プレイヤーが {this.gameObject.name} の範囲外に出ました。</color>", this.gameObject);
        if (_statusUIInstance != null)
        {
            _statusUIInstance.SetActive(false); // UIを非表示にする
        }
    }

    // ▼▼▼ OnInteractメソッドのロジックを変更 ▼▼▼
    public void OnInteract(PlayerController playerController)
    {
        // 現在のゲーム内日付を取得
        int currentDay = GameTimeManager.Instance.daysSurvived;

        // 「最後に使用した日」が「現在の日」より前であれば、タスクは利用可能
        if (dayLastUsed < currentDay)
        {
            if (currentMiniGame != null) return;

            // ★重要：タスクを開始する直前に、今日使ったことを記録する
            dayLastUsed = currentDay;
            
            StartCoroutine(StartMiniGameSequence());
        }
        else
        {
            // 今日は既に使用済みの場合
            Debug.Log($"TaskMachine '{this.gameObject.name}' は今日は使用済みです。明日また利用できます。");
            // ここで効果音を鳴らすなどのフィードバックをしても良い
        }
    }
    
    private TaskDifficulty SelectDifficultyByWeight()
    {
        // 1. 全ての難易度の重みの合計を計算する
        int totalWeight = 0;
        foreach (var difficulty in possibleDifficulties)
        {
            totalWeight += difficulty.selectionWeight;
        }

        // (もし全ての重みが0なら、等確率で返す)
        if (totalWeight == 0)
        {
            Debug.LogWarning("全ての難易度の重みが0です。等確率で選択します。");
            return possibleDifficulties[Random.Range(0, possibleDifficulties.Count)];
        }

        // 2. 0から合計の重み-1までの間でランダムな数値を生成
        int randomValue = Random.Range(0, totalWeight);

        // 3. 難易度を順番に見ていき、ランダムな数値から重みを引いていく
        foreach (var difficulty in possibleDifficulties)
        {
            // ランダムな値が現在の難易度の重みの範囲内に入ったら、その難易度を返す
            if (randomValue < difficulty.selectionWeight)
            {
                return difficulty;
            }
            // 入らなかった場合、ランダムな値からその難易度の重みを引いて、次の難易度へ
            randomValue -= difficulty.selectionWeight;
        }

        // (エラーケースのフォールバック)
        return possibleDifficulties[0];
    }
    private System.Collections.IEnumerator StartMiniGameSequence()
    {
        if (miniGamePrefabs == null || miniGamePrefabs.Count == 0 || possibleDifficulties == null || possibleDifficulties.Count == 0)
        {
            Debug.LogError("ミニゲームのプレハブまたは難易度が設定されていません！", this);
            yield break; // コルーチンを終了
        }

        // --- 1. ゲームと難易度を決定 ---
        SelectedDifficulty = SelectDifficultyByWeight(); 
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
            // ▼▼▼ ここからが変更点 ▼▼▼

            // 1. ゲームロジックを開始する直前に、ゲームオブジェクトを有効化（表示）する
            miniGameInstance.SetActive(true);

            // 2. イベントを購読し、タスクを開始する
            currentMiniGame.OnTaskCompleted += HandleTaskCompletion;
            currentMiniGame.StartTask(this);

            // ▲▲▲ ここまでが変更点 ▲▲▲
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