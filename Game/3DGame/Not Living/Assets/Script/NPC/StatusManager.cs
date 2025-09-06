using System.Collections;
using UnityEngine;

/// <summary>
/// NPCのステータス（HP、評判など）とダメージ処理を管理するクラス (3D版)
/// </summary>
public class StatusManager : MonoBehaviour
{
    [Header("基本ステータス")]
    public int maxHp = 100; // 最大HP
    public int currentHp;   // 現在のHP
    public int power = 10;      // 攻撃力
    public float speed = 3f;    // 移動速度

    [Header("評判システム")]
    [Range(0, 100)]
    public int reputation = 50; // このキャラクターの評判値
    public string currentPopularity; // 評判を文字列で表したもの（例: "Normal"）

    [Header("ダメージと無敵")]
    public float invincibilityDuration = 1.0f; // ダメージ後の無敵時間
    private bool isInvincible = false;     // 無敵中かどうかのフラグ

    [Header("その他")]
    public GameObject recoveryItemPrefab; // HPが0になった時に落とすアイテム
    
    // 3Dモデルの見た目を管理するためのRenderer
    private Renderer modelRenderer;

    void Start()
    {
        currentHp = maxHp;
        // 子オブジェクトからRendererを取得 (3Dモデルに合わせて調整)
        modelRenderer = GetComponentInChildren<Renderer>();
        if (modelRenderer == null)
        {
            Debug.LogError("子オブジェクトにRendererが見つかりません。", this);
        }
    }

    void Update()
    {
        // 評判を文字列に変換
        UpdatePopularity();

        // HPが最大値を超えないように制限
        if (currentHp > maxHp)
        {
            currentHp = maxHp;
        }

        // HPが0以下になった時の処理
        if (currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// ダメージを受ける処理。外部から呼び出すことを想定。
    /// </summary>
    public void TakeDamage(int damage)
    {
        // 無敵中はダメージを受けない
        if (isInvincible) return;

        currentHp -= damage;
        Debug.Log($"{gameObject.name} は {damage} のダメージを受けた！ 残りHP: {currentHp}");

        if (currentHp > 0)
        {
            // HPが残っていれば無敵状態へ
            StartCoroutine(BecomeInvincible());
        }
    }

    // 死亡処理
    private void Die()
    {
        Debug.Log($"{gameObject.name} は倒れた。");

        // 回復アイテムをドロップ
        if (recoveryItemPrefab != null)
        {
            Instantiate(recoveryItemPrefab, transform.position, Quaternion.identity);
        }
        
        // オブジェクトを破壊
        Destroy(gameObject);
    }

    // 一定時間、無敵になるコルーチン (点滅処理)
    IEnumerator BecomeInvincible()
    {
        isInvincible = true;
        
        // 点滅処理 (Rendererの有効/無効を切り替える)
        float endTime = Time.time + invincibilityDuration;
        while (Time.time < endTime)
        {
            if(modelRenderer != null) modelRenderer.enabled = false;
            yield return new WaitForSeconds(0.1f);
            if(modelRenderer != null) modelRenderer.enabled = true;
            yield return new WaitForSeconds(0.1f);
        }

        if(modelRenderer != null) modelRenderer.enabled = true;
        isInvincible = false;
    }
    
    // 評判の数値を文字列に変換する
    public void UpdatePopularity()
    {
        if (reputation >= 90) currentPopularity = "Saint";
        else if (reputation >= 70) currentPopularity = "So Good";
        else if (reputation >= 60) currentPopularity = "Good";
        else if (reputation >= 40) currentPopularity = "Normal";
        else if (reputation >= 30) currentPopularity = "Bad";
        else if (reputation >= 10) currentPopularity = "So Bad";
        else currentPopularity = "Worst";
    }

    // 3Dの当たり判定メソッド
    private void OnTriggerEnter(Collider other)
    {
        // "punch"タグを持つオブジェクトに当たったらダメージを受ける
        if(other.gameObject.CompareTag("punch"))
        {
            // 固定ダメージを受ける場合
            TakeDamage(10); 
        }
    }
}