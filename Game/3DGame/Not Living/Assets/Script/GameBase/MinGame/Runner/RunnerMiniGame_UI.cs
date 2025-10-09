using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RunnerMiniGame_UI : MonoBehaviour
{
    [Header("UI参照")]
    public RectTransform movingObjectsParent;
    public RectTransform spawnPoint;
    public RectTransform despawnPoint;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject groundPrefab;
    public GameObject obstaclePrefab;

    [Header("ゲーム設定")]
    public float scrollSpeed = 400f;
    [Range(0, 1f)]
    public float obstacleSpawnChance = 0.4f; // 地面の上に障害物が生成される確率
    public float fallThresholdY = -350f;     // このY座標より下に落ちたらゲームオーバー

    [Header("地面のランダム設定")]
    public Vector2 groundHeightRange = new Vector2(-200f, -150f); // 地面のY座標の範囲
    public Vector2 groundWidthRange = new Vector2(100f, 400f);   // 地面の幅の範囲
    public Vector2 gapWidthRange = new Vector2(70f, 200f);      // 穴（地面同士の間隔）の幅の範囲
    
    // プレイヤーコントローラーが地面の位置を把握するための静的リスト
    public static List<RectTransform> GroundRects { get; private set; }

    private GameObject playerInstance;
    private List<GameObject> groundObjects = new List<GameObject>();
    private List<GameObject> obstacleObjects = new List<GameObject>();
    private bool isGameActive = false;
    private float lastGroundEdgeX; // 最後に生成された地面の右端のX座標

    void Awake()
    {
        // アプリケーション開始時にリストを初期化
        if (GroundRects == null)
        {
            GroundRects = new List<RectTransform>();
        }
    }

    void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        if (playerInstance != null) Destroy(playerInstance);
        foreach (var obj in groundObjects) Destroy(obj);
        foreach (var obj in obstacleObjects) Destroy(obj);
        
        groundObjects.Clear();
        obstacleObjects.Clear();
        GroundRects.Clear();

        isGameActive = true;

        playerInstance = Instantiate(playerPrefab, movingObjectsParent);

        // 最初の足場を生成
        lastGroundEdgeX = -300f; // プレイヤーの開始位置あたり
        SpawnGround(true); // isInitial: true で安全な最初の足場を生成

        // 画面右端まで地面で埋める
        while(lastGroundEdgeX < spawnPoint.anchoredPosition.x)
        {
            SpawnGround(false);
        }
    }

    void Update()
    {
        if (!isGameActive) return;

        // --- スクロールと自動削除 ---
        float moveAmount = scrollSpeed * Time.deltaTime;
        ScrollAndCleanupObjects(groundObjects, GroundRects, moveAmount);
        ScrollAndCleanupObjects(obstacleObjects, null, moveAmount);
        
        // --- 新しい地面の生成 ---
        if (lastGroundEdgeX < spawnPoint.anchoredPosition.x)
        {
            SpawnGround(false);
        }

        // --- 当たり判定 ---
        CheckCollision();
        
        // --- 落下判定 ---
        if (playerInstance != null && playerInstance.GetComponent<RectTransform>().anchoredPosition.y < fallThresholdY)
        {
            GameOver("穴に落下しました");
        }
    }

    void SpawnGround(bool isInitial)
    {
        float groundY = Random.Range(groundHeightRange.x, groundHeightRange.y);
        float groundWidth = isInitial ? 600f : Random.Range(groundWidthRange.x, groundWidthRange.y);
        float gapWidth = isInitial ? 0f : Random.Range(gapWidthRange.x, gapWidthRange.y);
        float spawnX = lastGroundEdgeX + gapWidth + (groundWidth / 2f);

        GameObject ground = Instantiate(groundPrefab, movingObjectsParent);
        RectTransform rt = ground.GetComponent<RectTransform>();
        
        rt.sizeDelta = new Vector2(groundWidth, 50f);
        rt.anchoredPosition = new Vector2(spawnX, groundY);
        
        groundObjects.Add(ground);
        GroundRects.Add(rt);

        lastGroundEdgeX = rt.anchoredPosition.x + rt.rect.width / 2f;

        // 最初の足場以外で、確率で障害物を生成
        if (!isInitial && Random.value < obstacleSpawnChance)
        {
            SpawnObstacleOnGround(rt);
        }
    }

    void SpawnObstacleOnGround(RectTransform groundRect)
    {
        GameObject obstacle = Instantiate(obstaclePrefab, movingObjectsParent);
        RectTransform rt = obstacle.GetComponent<RectTransform>();
        
        // 地面の真ん中に配置
        float obstacleX = groundRect.anchoredPosition.x;
        // 地面の上端に配置 (地面と障害物の高さが50、Pivotが中心と仮定)
        float obstacleY = groundRect.anchoredPosition.y + (groundRect.rect.height / 2f) + (rt.rect.height / 2f);

        rt.anchoredPosition = new Vector2(obstacleX, obstacleY);
        obstacleObjects.Add(obstacle);
    }

    void ScrollAndCleanupObjects(List<GameObject> objectList, List<RectTransform> rectList, float moveAmount)
    {
        for (int i = objectList.Count - 1; i >= 0; i--)
        {
            GameObject obj = objectList[i];
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition += Vector2.left * moveAmount;

            // 画面外に出たらリストから削除して破壊
            if (rt.anchoredPosition.x + (rt.rect.width / 2f) < despawnPoint.anchoredPosition.x)
            {
                objectList.RemoveAt(i);
                rectList?.Remove(rt); // rectListがnullでなければ、そちらからも削除
                Destroy(obj);
            }
        }
    }

    void CheckCollision()
    {
        if (playerInstance == null) return;
        RectTransform playerRect = playerInstance.GetComponent<RectTransform>();

        foreach (var obstacle in obstacleObjects)
        {
            if (IsRectTransformOverlapping(playerRect, obstacle.GetComponent<RectTransform>()))
            {
                GameOver("障害物に衝突しました");
                return; // ゲームオーバーになったらループを抜ける
            }
        }
    }

    bool IsRectTransformOverlapping(RectTransform rect1, RectTransform rect2)
    {
        Rect r1 = new Rect(rect1.position.x - rect1.pivot.x * rect1.rect.width,
                           rect1.position.y - rect1.pivot.y * rect1.rect.height,
                           rect1.rect.width, rect1.rect.height);
        Rect r2 = new Rect(rect2.position.x - rect2.pivot.x * rect2.rect.width,
                           rect2.position.y - rect2.pivot.y * rect2.rect.height,
                           rect2.rect.width, rect2.rect.height);
        return r1.Overlaps(r2);
    }

    void GameOver(string reason)
    {
        if (!isGameActive) return;

        isGameActive = false;
        Debug.Log($"ゲームオーバー！理由: {reason}");
        // ここでリスタートボタンの表示などを行う
    }
}