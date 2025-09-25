using UnityEngine;

public class HitEffectSpawner : MonoBehaviour
{
    [Header("エフェクト設定")]
    [Tooltip("攻撃がヒットした時に生成するエフェクトのプレハブ")]
    public GameObject hitEffectPrefab;

    // HitEffectSpawner.cs
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[HitEffectSpawner] {this.transform.root.name} のパンチが、{other.name} に接触しました！");

        Transform attackerRoot = this.transform.root;
        Transform victimRoot = other.transform.root;

        if (attackerRoot == victimRoot) return;
        
        // ▼▼▼ 接触した相手のTagをログに表示する ▼▼▼
        Debug.Log($"接触した相手の親オブジェクトは '{victimRoot.name}' で、そのTagは '{victimRoot.tag}' です。");

        if (victimRoot.CompareTag("NPC"))
        {
            Debug.Log("<color=green>Hit！ NPCタグを正しく検知しました。エフェクトを生成します。</color>");
            if (hitEffectPrefab != null)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Quaternion hitRotation = Quaternion.LookRotation(attackerRoot.forward);
                Instantiate(hitEffectPrefab, hitPoint, hitRotation);
            }
        }
    }
}