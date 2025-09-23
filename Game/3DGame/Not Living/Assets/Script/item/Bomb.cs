using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour
{
    [Header("爆弾設定")]
    public float delay = 3f;
    public float explosionRadius = 5f;
    public float knockbackForce = 10f; // 吹き飛ばしの強さ
    public float knockbackDuration = 0.5f; // 吹き飛ばされる時間
    public int damage = 50;

    [Header("エフェクト設定")]
    public GameObject explosionEffectPrefab;

    private void Start()
    {
        StartCoroutine(ExplodeAfterDelay());
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        Explode();
    }

    private void Explode()
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in colliders)
        {
            if (hit.TryGetComponent<StatusManager>(out StatusManager targetStatus))
            {
                // ダメージを与える
                targetStatus.TakeDamage(damage, null);

                // ▼▼▼ 吹き飛ばし処理をこちらに変更 ▼▼▼
                // 爆心地から対象への方向を計算
                Vector3 knockbackDirection = (hit.transform.position - transform.position).normalized;
                // StatusManagerの吹き飛ばしメソッドを呼び出す
                targetStatus.ApplyKnockback(knockbackDirection, knockbackForce, knockbackDuration);
            }
        }

        Destroy(gameObject);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}