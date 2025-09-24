using UnityEngine;

public class HitEffectSpawner : MonoBehaviour
{
    [Header("エフェクト設定")]
    [Tooltip("攻撃がヒットした時に生成するエフェクトのプレハブ")]
    public GameObject hitEffectPrefab;

    // このスクリプトがアタッチされているコライダーが、他のトリガーコライダーに接触した瞬間に呼び出される
    private void OnTriggerEnter(Collider other)
    {
        // 攻撃者とヒットした相手のキャラクター本体（一番親のオブジェクト）を取得
        Transform attackerRoot = this.transform.root;
        Transform victimRoot = other.transform.root;

        // 自分自身に当たった場合は、何もしない
        if (attackerRoot == victimRoot)
        {
            return;
        }

        // ヒットした相手が "NPC" タグを持っている場合
        if (victimRoot.CompareTag("NPC"))
        {
            Debug.Log("Hit！");
            // エフェクトのプレハブが設定されていれば
            if (hitEffectPrefab != null)
            {
                // 接触した点に最も近い位置を計算
                Vector3 hitPoint = other.ClosestPoint(transform.position);

                // 攻撃者の向き（右手）に合わせた回転を計算
                Quaternion hitRotation = Quaternion.LookRotation(attackerRoot.forward);

                // 計算した位置と向きでエフェクトを生成
                Instantiate(hitEffectPrefab, hitPoint, hitRotation);
            }
        }
    }
}