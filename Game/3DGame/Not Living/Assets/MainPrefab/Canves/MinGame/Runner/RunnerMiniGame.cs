// RunnerMiniGame.cs (修正版)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;

public class RunnerMiniGame : MonoBehaviour, ITaskMiniGame
{

    public event Action<bool> OnTaskCompleted;

    [Header("UI参照")]
    public RectTransform movingObjectsParent;
    public RectTransform spawnPoint;
    public RectTransform despawnPoint;
    public TextMeshProUGUI timerText;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject groundPrefab;
    public GameObject obstaclePrefab;
    public GameObject sparkPrefab; // ▼▼▼ 追加 ▼▼▼

    [Header("コース設定")]
    [Range(0, 1f)]
    public float obstacleSpawnChance = 0.4f;
    public float fallThresholdY = -350f;
    public Vector2 groundHeightRange = new Vector2(-200f, -150f);
    public Vector2 groundWidthRange = new Vector2(100f, 400f);
    public Vector2 gapWidthRange = new Vector2(70f, 200f);

    [Header("火の粉設定")] // ▼▼▼ 追加 ▼▼▼
    [Tooltip("火の粉が飛んでくる間隔（秒）")]
    public float sparkSpawnInterval = 3.0f;
    [Tooltip("火の粉の飛んでくる速さ")]
    public float sparkSpeed = 600f;
    [Tooltip("火の粉の出現位置")]
    public Vector2 sparkSpawnPosition = new Vector2(0, 300f); // 画面左端から発射されるように調整

    private float _survivalTime;
    private float _scrollSpeed;
    private float _timer;

    public static List<RectTransform> GroundRects { get; private set; }
    private GameObject _playerInstance;
    private List<GameObject> _groundObjects = new List<GameObject>();
    private List<GameObject> _obstacleObjects = new List<GameObject>();
    private List<GameObject> _activeSparks = new List<GameObject>(); // ▼▼▼ 追加 ▼▼▼
    private bool _isGameActive = false;
    private RectTransform _rightmostGround; 

    void Awake()
    {
        if (GroundRects == null) GroundRects = new List<RectTransform>();
    }
    
    public void StartTask(TaskMachine machine)
    {
        _survivalTime = machine.SelectedDifficulty.survivalTime;
        _scrollSpeed = machine.SelectedDifficulty.scrollSpeed;
        _timer = _survivalTime;
        InitializeGame();
    }

    private void InitializeGame()
    {
        foreach (var obj in _groundObjects) Destroy(obj); // クリーンアップ
        foreach (var obj in _obstacleObjects) Destroy(obj);
        foreach (var obj in _activeSparks) Destroy(obj); 
        _groundObjects.Clear();
        _obstacleObjects.Clear();
        _activeSparks.Clear();
        GroundRects.Clear();
        if(_playerInstance != null) Destroy(_playerInstance);
        
        _rightmostGround = null;
        _playerInstance = Instantiate(playerPrefab, movingObjectsParent);

        SpawnGround(true); 
        while(GetRightmostGroundEdgeX() < spawnPoint.anchoredPosition.x)
        {
            SpawnGround(false);
        }

        _isGameActive = true;
        StartCoroutine(SpawnSparksRoutine()); // ▼▼▼ 追加: 火の粉生成コルーチンを開始 ▼▼▼
    }

    void Update()
    {
        if (!_isGameActive) return;

        _timer -= Time.deltaTime;
        timerText.text = $"残り: {_timer:F1}秒";
        if (_timer <= 0)
        {
            EndGame(true);
            return;
        }

        float moveAmount = _scrollSpeed * Time.deltaTime;
        ManageScrollingAndCleanup(moveAmount);
        ManageWorldGeneration();
        CheckForFailure();
    }

    private void EndGame(bool success)
    {
        if (!_isGameActive) return;
        _isGameActive = false;
        
        StopAllCoroutines(); // ▼▼▼ 追加: ゲーム終了時に全てのコルーチンを停止 ▼▼▼

        timerText.text = success ? "クリア！" : "失敗...";
        OnTaskCompleted?.Invoke(success);
    }
    
    // ▼▼▼ 追加: 火の粉を定期的に生成するコルーチン ▼▼▼
    IEnumerator SpawnSparksRoutine()
    {
        // 最初の1回は少し待ってから
        yield return new WaitForSeconds(sparkSpawnInterval);

        while (_isGameActive)
        {
            if (_playerInstance != null)
            {
                // 火の粉を生成し、リストに追加
                GameObject spark = Instantiate(sparkPrefab, movingObjectsParent);
                spark.GetComponent<RectTransform>().anchoredPosition = sparkSpawnPosition;
                _activeSparks.Add(spark);

                // プレイヤーの現在位置を狙わせる
                Vector2 targetPos = _playerInstance.GetComponent<RectTransform>().position;
                spark.GetComponent<SparkMover>().Initialize(targetPos, sparkSpeed);
            }
            
            // 指定した秒数だけ待機
            yield return new WaitForSeconds(sparkSpawnInterval);
        }
    }
    
    private void CheckForFailure()
    {
        if (_playerInstance == null) return;
        var playerRect = _playerInstance.GetComponent<RectTransform>();

        // (落下判定と障害物判定はそのまま)
        if (playerRect.anchoredPosition.y < fallThresholdY)
        {
            EndGame(false); return;
        }
        foreach (var obstacle in _obstacleObjects)
        {
            if (playerRect.Overlaps(obstacle.GetComponent<RectTransform>()))
            {
                EndGame(false); return;
            }
        }
        
        // ▼▼▼ 追加: 火の粉との当たり判定 ▼▼▼
        // リストを逆からループして、nullチェックと当たり判定を同時に行う
        for (int i = _activeSparks.Count - 1; i >= 0; i--)
        {
            // 火の粉が画面外に出て破棄された場合、リスト内ではnullになる
            if (_activeSparks[i] == null)
            {
                _activeSparks.RemoveAt(i);
                continue;
            }

            var sparkRect = _activeSparks[i].GetComponent<RectTransform>();
            if (playerRect.Overlaps(sparkRect))
            {
                EndGame(false);
                return;
            }
        }
    }
    private void ManageScrollingAndCleanup(float moveAmount)
    {
        // ... (このメソッドの中身は変更ありません)
        for (int i = _groundObjects.Count - 1; i >= 0; i--)
        {
            var obj = _groundObjects[i];
            var rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition += Vector2.left * moveAmount;
            if (rt.anchoredPosition.x + rt.rect.width / 2f < despawnPoint.anchoredPosition.x)
            {
                if (rt == _rightmostGround) _rightmostGround = null; // 念の為
                GroundRects.Remove(rt);
                _groundObjects.RemoveAt(i);
                Destroy(obj);
            }
        }
        for (int i = _obstacleObjects.Count - 1; i >= 0; i--)
        {
            var obj = _obstacleObjects[i];
            obj.GetComponent<RectTransform>().anchoredPosition += Vector2.left * moveAmount;
            if (obj.GetComponent<RectTransform>().anchoredPosition.x < despawnPoint.anchoredPosition.x)
            {
                _obstacleObjects.RemoveAt(i);
                Destroy(obj);
            }
        }
    }

    private void ManageWorldGeneration()
    {
        // 一番右の地面の「現在の右端X座標」を取得して判定する
        if (GetRightmostGroundEdgeX() < spawnPoint.anchoredPosition.x)
        {
            SpawnGround(false);
        }
    }

    void SpawnGround(bool isInitial)
    {
        float lastEdgeX = GetRightmostGroundEdgeX();
        
        float groundY = UnityEngine.Random.Range(groundHeightRange.x, groundHeightRange.y);
        float groundWidth = isInitial ? 600f : UnityEngine.Random.Range(groundWidthRange.x, groundWidthRange.y);
        float gapWidth = isInitial ? 0f : UnityEngine.Random.Range(gapWidthRange.x, gapWidthRange.y);
        
        // 最初の地面か、2個目以降かでX座標の計算を分ける
        float spawnX = isInitial ? -300f : lastEdgeX + gapWidth + (groundWidth / 2f);

        GameObject ground = Instantiate(groundPrefab, movingObjectsParent);
        RectTransform rt = ground.GetComponent<RectTransform>();
        
        rt.sizeDelta = new Vector2(groundWidth, 50f);
        rt.anchoredPosition = new Vector2(spawnX, groundY);
        
        _groundObjects.Add(ground);
        GroundRects.Add(rt);
        
        // 新しく生成した地面を「一番右の地面」として記録する
        _rightmostGround = rt; 

        if (!isInitial && UnityEngine.Random.value < obstacleSpawnChance)
        {
            SpawnObstacleOnGround(rt);
        }
    }
    
    // 一番右にある地面の、現在の右端X座標を取得する
    float GetRightmostGroundEdgeX()
    {
        if (_rightmostGround == null)
        {
            // 最初の1個目を生成する場合
            return -Mathf.Infinity; 
        }
        // 現在の位置 + 幅の半分 = 右端の座標
        return _rightmostGround.anchoredPosition.x + _rightmostGround.rect.width / 2f;
    }

    void SpawnObstacleOnGround(RectTransform groundRect)
    {
        GameObject obstacle = Instantiate(obstaclePrefab, movingObjectsParent);
        RectTransform rt = obstacle.GetComponent<RectTransform>();

        // ▼▼▼ ここからが変更点 ▼▼▼
        // 障害物が地面からはみ出さないように、生成可能なX座標の範囲を計算
        float groundHalfWidth = groundRect.rect.width / 2f;
        float obstacleHalfWidth = rt.rect.width / 2f;
        float minX = groundRect.anchoredPosition.x - groundHalfWidth + obstacleHalfWidth;
        float maxX = groundRect.anchoredPosition.x + groundHalfWidth - obstacleHalfWidth;

        // 計算した範囲内のランダムなX座標を決定
        float obstacleX = UnityEngine.Random.Range(minX, maxX);
        // ▲▲▲ ここまでが変更点 ▲▲▲

        float obstacleY = groundRect.anchoredPosition.y + (groundRect.rect.height / 2f) + (rt.rect.height / 2f);
        rt.anchoredPosition = new Vector2(obstacleX, obstacleY);
        _obstacleObjects.Add(obstacle);
    }
}

// 拡張メソッド (変更なし)
public static class RectTransformExtensions
{
    public static bool Overlaps(this RectTransform rect1, RectTransform rect2)
    {
        Rect r1 = new Rect(rect1.position, rect1.rect.size);
        Rect r2 = new Rect(rect2.position, rect2.rect.size);
        return r1.Overlaps(r2, true);
    }
}