// MazeMiniGame.cs (ランダムスタート/ゴール対応版)
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
public class MazeMiniGame : MonoBehaviour, ITaskMiniGame
{
    public event Action<bool> OnTaskCompleted;

    [Header("迷路設定")]
    [Tooltip("迷路の幅（奇数）")]
    public int mazeWidth = 15;
    [Tooltip("迷路の高さ（奇数）")]
    public int mazeHeight = 15;
    
    [Header("プレイヤー設定")]
    [Tooltip("キーを押し続けたときの移動リピート間隔（秒）")]
    public float moveRepeatDelay = 0.2f;

    private float _moveTimer = 0f;

    [Header("UI参照")]
    public GridLayoutGroup mazeGridParent;
    public TextMeshProUGUI timerText; // 制限時間を表示するUI

    public TextMeshProUGUI keyStatusText;   // 「カギ: X / Y」を表示するUIテキスト
    public Image keyIconUI;

    public GameObject timerUIPrefab;




    [Header("プレハブ")]
    public GameObject wallPrefab, floorPrefab, playerPrefab, goalPrefab;
    public GameObject keyPrefab; // キーのプレハブ
    public GameObject enemyPrefab; // 敵のプレハブ
    
    public Sprite closedDoorSprite;
    public Sprite openDoorSprite;

    private AudioSource audioSource;
    public AudioClip keyClip;
    public AudioClip openClip;


    private int _mazeWidth, _mazeHeight; // 難易度から設定されるサイズ
    private float _timeLimit;            // 難易度から設定される制限時間
    
    private float _timer;                // 残り時間タイマー
    private int[,] _mazeData;
    private GameObject _playerInstance;
    private Vector2Int _playerGridPos;
    private Vector2Int _goalGridPos; //ゴール座標を保持する変数
    private bool _isGameActive = false;
    // 複数キー管理用の変数
    private int _keysRequired;
    private int _keysCollected;
    private List<Vector2Int> _keyPositions = new List<Vector2Int>();
    private Dictionary<Vector2Int, GameObject> _keyInstances = new Dictionary<Vector2Int, GameObject>();
    private Image _goalImage; // ゴールのImageコンポーネント参照
    private List<MazeEnemy> _enemies = new List<MazeEnemy>(); // ▼▼▼ 追加: 敵リスト
    private int _enemyCount; // ▼▼▼ 追加: 敵の数

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    public void StartTask(TaskMachine machine)
    {
        foreach (Transform child in mazeGridParent.transform) Destroy(child.gameObject);

        // 難易度設定を取得
        _mazeWidth = machine.SelectedDifficulty.mazeWidth;
        _mazeHeight = machine.SelectedDifficulty.mazeHeight;
        _timeLimit = machine.SelectedDifficulty.mazeTimeLimit;
        _timer = _timeLimit;
        _keysRequired = machine.SelectedDifficulty.mazeKeyCount;
        _enemyCount = machine.SelectedDifficulty.mazeEnemyCount; // 難易度から敵の数を取得
        _enemies.Clear(); // 敵リストをクリア

        timerUIPrefab.SetActive(true);
        timerText.text = $" WASD で移動";
        
        //_goalImage = null; // 念のためリセット

        if (keyStatusText != null) keyStatusText.gameObject.SetActive(false);

        // MazeGeneratorの静的変数を設定
        MazeGenerator.width = _mazeWidth;
        MazeGenerator.height = _mazeHeight;

        List<Vector2Int> corners = new List<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(_mazeWidth - 1, 0),
            new Vector2Int(0, _mazeHeight - 1),
            new Vector2Int(_mazeWidth - 1, _mazeHeight - 1)
        };
        
        int startIndex = UnityEngine.Random.Range(0, corners.Count);
        _playerGridPos = corners[startIndex];
        corners.RemoveAt(startIndex);

        int goalIndex = UnityEngine.Random.Range(0, corners.Count);
        _goalGridPos = corners[goalIndex];

        _mazeData = MazeGenerator.Generate(_playerGridPos, _goalGridPos);
        
        // 複数キーの設置場所を決定
        _keyPositions.Clear();
        _keyInstances.Clear();
        List<Vector2Int> deadEnds = MazeGenerator.FindDeadEnds(_mazeData, _playerGridPos, _goalGridPos);
        
        deadEnds = deadEnds.OrderBy(x => UnityEngine.Random.value).ToList();

        int keysToPlace = Mathf.Min(_keysRequired, deadEnds.Count);
        if (_keysRequired > keysToPlace)
        {
            Debug.LogWarning($"行き止まりの数が足りないため、鍵の数を {keysToPlace} 個に減らします。");
            _keysRequired = keysToPlace;
        }

        for (int i = 0; i < keysToPlace; i++)
        {
            _keyPositions.Add(deadEnds[i]);
        }
        
        _keysCollected = 0;
        UpdateKeyStatusUI();
        if (_keysRequired == 0)
        {
            OpenGoalDoor();
        }
        mazeGridParent.constraintCount = _mazeWidth;
        RenderMaze();
        _isGameActive = true;
    }
    
    void Update()
    {
        if (!_isGameActive) return;

        // --- 移動タイマー ---
        if (_moveTimer > 0) _moveTimer -= Time.deltaTime;
        HandlePlayerInput();

        // ▼▼▼ 制限時間タイマーを追加 ▼▼▼
        if (_timer > 0)
        {
            _timer -= Time.deltaTime;
            if (timerText != null)
            {
                timerText.text = $"残り: {_timer:F1}";
            }

            if (_timer <= 0)
            {
                EndGame(false); // 時間切れで失敗
            }
        }

        CheckEnemyCollision(); // 敵との衝突チェック
    }
    private void RenderMaze()
    {
        // 通路のマスの座標だけを先にリストアップ
        List<Vector2Int> floorPositions = new List<Vector2Int>();
        for (int y = _mazeHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < _mazeWidth; x++)
            {
                if (_mazeData[x, y] > 0)
                {
                    floorPositions.Add(new Vector2Int(x, y));
                }
            }
        }
        // 敵のスポーン位置を決める
        List<Vector2Int> enemySpawnPositions = new List<Vector2Int>();
        // スタートとゴールから遠い通路をスポーン候補地にする
        var spawnCandidates = floorPositions
            .Where(p => Vector2Int.Distance(p, _playerGridPos) > 5 && Vector2Int.Distance(p, _goalGridPos) > 5)
            .OrderBy(p => UnityEngine.Random.value)
            .ToList();
        int enemiesToPlace = Mathf.Min(_enemyCount, spawnCandidates.Count);
        for (int i = 0; i < enemiesToPlace; i++)
        {
            enemySpawnPositions.Add(spawnCandidates[i]);
        }
        

        for (int y = _mazeHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < _mazeWidth; x++)
            {
                GameObject tile;
                if (_mazeData[x, y] == 0)
                {
                    tile = Instantiate(wallPrefab, mazeGridParent.transform);
                }
                else
                {
                    tile = Instantiate(floorPrefab, mazeGridParent.transform);
                    var currentPos = new Vector2Int(x, y);
                    if (x == _playerGridPos.x && y == _playerGridPos.y)
                        _playerInstance = Instantiate(playerPrefab, tile.transform);
                    else if (currentPos == _goalGridPos)
                    {
                        //変更点: ゴールを生成し、Imageコンポーネントを取得・設定
                        GameObject goalInstance = Instantiate(goalPrefab, tile.transform);
                        _goalImage = goalInstance.GetComponent<Image>();
                        if (_goalImage != null && closedDoorSprite != null)
                        {
                            _goalImage.sprite = closedDoorSprite; // 最初は閉じた扉を表示
                        }
                    }
                    else if (_keyPositions.Contains(currentPos))
                    {
                        GameObject keyInstance = Instantiate(keyPrefab, tile.transform);
                        _keyInstances[currentPos] = keyInstance;
                    }
                    //else if (x == _keyGridPos.x && y == _keyGridPos.y) //
                    else if (enemySpawnPositions.Contains(new Vector2Int(x,y)))
                    {
                        GameObject enemyInstance = Instantiate(enemyPrefab, tile.transform);
                        MazeEnemy enemyAI = enemyInstance.AddComponent<MazeEnemy>();
                        enemyAI.Initialize(_mazeData, new Vector2Int(x,y), 1.0f, mazeGridParent.transform);
                        _enemies.Add(enemyAI);
                    }
                }
                tile.name = $"Tile_{x}_{y}";
            }
        }
    }

    private void CheckEnemyCollision()
    {
        foreach (var enemy in _enemies)
        {
            if (enemy != null && _playerGridPos == enemy.currentPos)
            {
                EndGame(false); // 敵と座標が重なったらゲームオーバー
                return;
            }
        }
    }

    // --- (HandlePlayerInputは変更なし) ---
    private void HandlePlayerInput()
    {
        if (_moveTimer > 0) return;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2Int moveDir = Vector2Int.zero;
        if (Mathf.Abs(h) > 0.5f) moveDir = new Vector2Int(h > 0 ? 1 : -1, 0);
        else if (Mathf.Abs(v) > 0.5f) moveDir = new Vector2Int(0, v > 0 ? 1 : -1);
        if (moveDir != Vector2Int.zero)
        {
            MovePlayer(moveDir);
            _moveTimer = moveRepeatDelay;
        }
    }

    private void MovePlayer(Vector2Int direction)
    {
        Vector2Int wallPos = _playerGridPos + direction;
        Vector2Int targetPos = _playerGridPos + direction * 1;

        if (targetPos.x < 0 || targetPos.x >= _mazeWidth || targetPos.y < 0 || targetPos.y >= _mazeHeight)
            return;

        if (_mazeData[wallPos.x, wallPos.y] > 0)
        {
            _playerGridPos = targetPos;

            Transform targetFloor = mazeGridParent.transform.Find($"Tile_{_playerGridPos.x}_{_playerGridPos.y}");
            if (targetFloor != null)
            {
                _playerInstance.transform.SetParent(targetFloor, false);
                _playerInstance.transform.localPosition = Vector3.zero;
            }

            // 鍵の取得判定（複数対応）
            if (_keyInstances.ContainsKey(_playerGridPos))
            {
                Destroy(_keyInstances[_playerGridPos]);
                _keyInstances.Remove(_playerGridPos);
                if (audioSource != null && keyClip != null)
                    audioSource.PlayOneShot(keyClip);
                _keysCollected++;
                UpdateKeyStatusUI();
                Debug.Log($"鍵を入手した！ (残り {_keysRequired - _keysCollected} 個)");
                if (_keysCollected >= _keysRequired)
                {
                    if (audioSource != null && openClip != null)
                        audioSource.PlayOneShot(openClip);
                    OpenGoalDoor();
                }
            }

            // ゴール判定（複数キー対応）
            if (_playerGridPos == _goalGridPos)
            {
                if (_keysCollected >= _keysRequired)
                {
                    EndGame(true);
                }
                else
                {
                    Debug.Log($"鍵が足りません！ (あと { _keysRequired - _keysCollected} 個)");
                }
            }
        }
    }
    
    // 鍵UIの更新メソッド
    private void UpdateKeyStatusUI()
    {
        if (keyStatusText != null)
        {
            if (_keysRequired > 0)
            {
                keyStatusText.gameObject.SetActive(true);
                keyStatusText.text = $"カギ: {_keysCollected} / {_keysRequired}";
            }
            else
            {
                keyStatusText.gameObject.SetActive(false);
            }
        }
    }

    private void EndGame(bool success)
    {
        if (!_isGameActive) return;
        _isGameActive = false;
        // ゲーム終了時にUIを非表示に戻す
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (keyStatusText != null) keyStatusText.gameObject.SetActive(false);

        timerUIPrefab.SetActive(false);

        Debug.Log(success ? "迷路クリア！" : "迷路失敗...");
        OnTaskCompleted?.Invoke(success);
    }
    private void OpenGoalDoor()
    {
        if (_goalImage != null && openDoorSprite != null)
        {
            _goalImage.sprite = openDoorSprite;
            Debug.Log("全ての鍵を集めた！ゴールの扉が開いた！");
            // ここで効果音を鳴らしても良い
        }
    }
}