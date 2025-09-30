using UnityEngine;

// RigidbodyとColliderが必須であることを保証
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    [Header("弾の設定")]
    [Tooltip("弾が与えるダメージ量")]
    public int damage = 25;
    [Tooltip("弾が消滅するまでの時間（秒）")]
    public float lifetime = 5f;

    // --- 内部変数 ---
    private GameObject owner; // この弾を発射したキャラクター

    // 初期設定用のメソッド
    public void Initialize(GameObject owner)
    {
        this.owner = owner;
        // 発射から指定した時間が経過したら、自動で自身を破壊する
        Destroy(gameObject, lifetime);
    }

    // 他のコライダーに衝突した瞬間に呼び出される
    private void OnCollisionEnter(Collision collision)
    {
        // 衝突した相手が自分自身や、自分を発射したキャラクターなら何もしない
        if (collision.transform.root == this.owner.transform.root)
        {
            return;
        }

        // 衝突した相手がダメージを受けられる対象（StatusManagerを持っている）なら
        if (collision.gameObject.TryGetComponent<StatusManager>(out StatusManager victimStatus))
        {
            // ダメージを与える（攻撃者として、この弾を発射したownerを渡す）
            victimStatus.TakeDamage(damage, this.owner);
        }

        // --- (任意) ヒットエフェクトをここで生成しても良い ---
        // if (hitEffectPrefab != null) { ... }

        // 何かに当たったら、弾自身を破壊する
        Destroy(gameObject);
    }
}