// HitboxController.cs
using UnityEngine;

/// <summary>
/// アニメーションイベントに応じて、武器や体の当たり判定（Collider）を有効化/無効化するクラス
/// </summary>
public class HitboxController : MonoBehaviour
{
    [Tooltip("攻撃用の当たり判定。複数設定可能")]
    public Collider[] attackHitboxes;

    // アニメーションイベントから呼び出される
    public void EnableHitboxes()
    {
        // ▼▼▼ デバッグログを追加 ▼▼▼
        //Debug.Log($"<color=green>{transform.root.name} の EnableHitboxes が呼ばれました。</color>", transform.root.gameObject);

        foreach (var hitbox in attackHitboxes)
        {
            if (hitbox != null)
            {
                hitbox.enabled = true;
            }
        }
    }

    // アニメーションイベントから呼び出される
    public void DisableHitboxes()
    {
        // ▼▼▼ デバッグログを追加 ▼▼▼
        //Debug.Log($"<color=red>{transform.root.name} の DisableHitboxes が呼ばれました。</color>", transform.root.gameObject);

        foreach (var hitbox in attackHitboxes)
        {
            if (hitbox != null)
            {
                hitbox.enabled = false;
            }
        }
    }
}