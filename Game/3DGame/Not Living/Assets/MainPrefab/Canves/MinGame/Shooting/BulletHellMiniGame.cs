// BulletHellMiniGame.cs (新規作成)
using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;

public class BulletHellMiniGame : MonoBehaviour, ITaskMiniGame
{
    public event Action<bool> OnTaskCompleted;

    [Header("UI参照")]
    public RectTransform playArea; // プレイヤーと敵を配置する親オブジェクト
    public TextMeshProUGUI timerText;

    [Header("プレハブ")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    [Header("ゲーム設定")]
    [Tooltip("プレイヤーの当たり判定の半径")]
    public float playerHitboxRadius = 10f;

    // --- private変数 ---
    private float _survivalTime;
    private int _enemyCount;
    private float _timer;
    private bool _isGameActive = false;
    private GameObject _playerInstance;
    private List<GameObject> _enemies = new List<GameObject>();
    private List<Bullet> _activeBullets = new List<Bullet>();

    public void StartTask(TaskMachine machine)
    {
        // --- 既存オブジェクトの掃除 ---
        if (_playerInstance != null) Destroy(_playerInstance);
        foreach (var enemy in _enemies) Destroy(enemy);
        _enemies.Clear();
        // Bulletは自動で消えるので、ここでは何もしない

        // --- 難易度設定を取得 ---
        // ※TaskDifficultyクラスに以下の変数を追加してください
        // public float bh_survivalTime = 20f;
        // public int bh_enemyCount = 1;

        _survivalTime = machine.SelectedDifficulty.survivalTime;
        _enemyCount = machine.SelectedDifficulty.mazeEnemyCount; // 他のゲームの設定を流用
        _timer = _survivalTime;

        // --- ゲームオブジェクトの生成 ---
        _playerInstance = Instantiate(playerPrefab, playArea);
        
        // 敵をランダムな位置に配置
        for (int i = 0; i < _enemyCount; i++)
        {
            float x = UnityEngine.Random.Range(-100, 100);
            float y = UnityEngine.Random.Range(300, 400);
            GameObject enemy = Instantiate(enemyPrefab, playArea);
            enemy.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            // ▼▼▼ この行を変更 ▼▼▼
            // enemy.GetComponent<EnemyEmitter>().StartFiring(); // ← 古いコード
            enemy.GetComponent<EnemyEmitter>().InitializeAndStart(_playerInstance.transform); // ← 新しいコード
            
            _enemies.Add(enemy);
        }

        _isGameActive = true;
    }

    void Update()
    {
        if (!_isGameActive) return;

        // --- タイマー処理 ---
        _timer -= Time.deltaTime;
        timerText.text = $"残り: {_timer:F1}";
        if (_timer <= 0)
        {
            EndGame(true); // 時間切れで成功
            return;
        }

        // --- 当たり判定 ---
        if (_playerInstance == null) return;
        
        Bullet_BH[] bullets = playArea.GetComponentsInChildren<Bullet_BH>();
        Vector2 playerPos = _playerInstance.GetComponent<RectTransform>().anchoredPosition;

        foreach (var bullet in bullets)
        {
            Vector2 bulletPos = bullet.GetComponent<RectTransform>().anchoredPosition;

            // ▼▼▼ 判定ロジックを修正 ▼▼▼
            // プレイヤーと弾の半径を合計した距離より近ければ当たり
            float combinedRadius = playerHitboxRadius + bullet.hitboxRadius;
            if (Vector2.Distance(playerPos, bulletPos) < combinedRadius)
            {
                EndGame(false); // 弾に当たったら失敗
                return;
            }
        }
    }

    private void EndGame(bool success)
    {
        if (!_isGameActive) return;
        _isGameActive = false;

        // 全てのオブジェクトを削除
        if (_playerInstance != null) Destroy(_playerInstance);
        foreach (var enemy in _enemies) Destroy(enemy);
        _enemies.Clear();
        foreach (var bullet in playArea.GetComponentsInChildren<Bullet>()) Destroy(bullet.gameObject);

        OnTaskCompleted?.Invoke(success);
    }
}