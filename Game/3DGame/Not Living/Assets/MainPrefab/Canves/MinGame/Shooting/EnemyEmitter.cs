// EnemyEmitter.cs (パターン詳細設定・改良版)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// --- 攻撃パターンの種類を定義 ---
public enum AttackPatternType
{
    RotatingCircle,
    TargetedNWay,
    RandomBurst
}

// ▼▼▼ 追加: パターンごとの詳細設定をまとめるためのクラス ▼▼▼
[System.Serializable]
public class AttackPattern
{
    [Tooltip("このパターンの名前（識別のためのメモ）")]
    public string patternName = "New Pattern";
    [Tooltip("このパターンで使用する攻撃の種類")]
    public AttackPatternType patternType;
    [Tooltip("このパターンで使用する弾のプレハブ")]
    public GameObject bulletPrefab;

    [Space(5)]
    [Tooltip("弾の発射間隔（秒）")]
    public float fireInterval = 0.5f;
    [Tooltip("一度に発射する弾の数")]
    public int bulletsPerShot = 12;
    [Tooltip("弾の速度")]
    public float bulletSpeed = 200f;

    [Space(5)]
    [Header("パターン固有設定")]
    [Tooltip("（回転円形弾用）発射角の回転速度")]
    public float rotateSpeed = 20f;
    [Tooltip("（N-Way弾用）弾の数")]
    [Range(1, 15)]
    public int nWayCount = 5;
    [Tooltip("（N-Way弾用）弾が広がる角度")]
    public float nWaySpreadAngle = 60f;
}


public class EnemyEmitter : MonoBehaviour
{
    [Header("パターン切り替え")]
    [Tooltip("このエネミーが使用する攻撃パターンのリスト")]
    public List<AttackPattern> attackPatterns = new List<AttackPattern>();
    [Tooltip("パターンを切り替える間隔（秒）")]
    public float patternSwitchInterval = 8f;

    // --- private変数 ---
    private Transform _playerTransform;
    private Coroutine _currentPatternCoroutine;
    private float _rotatingCircleAngle = 0f;

    public void InitializeAndStart(Transform player)
    {
        _playerTransform = player;
        StartCoroutine(PatternManagerRoutine());
    }

    private IEnumerator PatternManagerRoutine()
    {
        if (attackPatterns == null || attackPatterns.Count == 0)
        {
            Debug.LogError("攻撃パターンが設定されていません！", this.gameObject);
            yield break;
        }

        while (true)
        {
            AttackPattern nextPattern = attackPatterns[Random.Range(0, attackPatterns.Count)];

            switch (nextPattern.patternType)
            {
                case AttackPatternType.RotatingCircle:
                    _currentPatternCoroutine = StartCoroutine(RotatingCircleRoutine(nextPattern));
                    break;
                case AttackPatternType.TargetedNWay:
                    _currentPatternCoroutine = StartCoroutine(TargetedNWayRoutine(nextPattern));
                    break;
                case AttackPatternType.RandomBurst:
                    _currentPatternCoroutine = StartCoroutine(RandomBurstRoutine(nextPattern));
                    break;
            }

            yield return new WaitForSeconds(patternSwitchInterval);

            if (_currentPatternCoroutine != null)
            {
                StopCoroutine(_currentPatternCoroutine);
            }
        }
    }

    // ===== 個別の弾幕パターン用コルーチン =====
    // 引数でAttackPatternを受け取るように変更

    private IEnumerator RotatingCircleRoutine(AttackPattern settings)
    {
        while (true)
        {
            float angleStep = 360f / settings.bulletsPerShot;
            for (int i = 0; i < settings.bulletsPerShot; i++)
            {
                float angle = _rotatingCircleAngle + i * angleStep;
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                FireBullet(dir, settings.bulletPrefab, settings.bulletSpeed);
            }
            _rotatingCircleAngle = (_rotatingCircleAngle + settings.rotateSpeed) % 360f;
            yield return new WaitForSeconds(settings.fireInterval);
        }
    }

    private IEnumerator TargetedNWayRoutine(AttackPattern settings)
    {
        while (true)
        {
            if (_playerTransform == null) yield break;

            Vector3 playerDir = (_playerTransform.position - transform.position).normalized;
            float centerAngle = Mathf.Atan2(playerDir.y, playerDir.x) * Mathf.Rad2Deg;
            float startAngle = centerAngle - settings.nWaySpreadAngle / 2f;

            for (int i = 0; i < settings.nWayCount; i++)
            {
                float angle = startAngle + (settings.nWaySpreadAngle / (settings.nWayCount - 1)) * i;
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                FireBullet(dir, settings.bulletPrefab, settings.bulletSpeed);
            }
            yield return new WaitForSeconds(settings.fireInterval);
        }
    }

    private IEnumerator RandomBurstRoutine(AttackPattern settings)
    {
        while (true)
        {
            for (int i = 0; i < settings.bulletsPerShot; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                FireBullet(dir, settings.bulletPrefab, settings.bulletSpeed);
            }
            yield return new WaitForSeconds(settings.fireInterval);
        }
    }

    // 弾を生成する共通関数を、プレハブと速度を受け取るように変更
    private void FireBullet(Vector3 direction, GameObject bulletPrefab, float speed)
    {
        GameObject bullet = Instantiate(bulletPrefab, transform.parent);
        bullet.transform.position = transform.position;
        bullet.GetComponent<Bullet_BH>().Initialize(direction, speed);
    }
}