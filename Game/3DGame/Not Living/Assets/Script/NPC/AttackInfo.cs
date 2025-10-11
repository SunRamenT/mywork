using UnityEngine;

/// <summary>
/// 攻撃のダメージ量を保持するためのデータ用クラス
/// </summary>
public class AttackInfo : MonoBehaviour
{
    [Tooltip("この攻撃のダメージ量。ゲーム開始時に自動で設定されます。")]
    public int damage;

    // Awakeは、ゲーム開始時にStartよりも先に一度だけ呼ばれる処理
    void Awake()
    {
        // 1. 自分自身と親オブジェクトを遡って "StatusManager" を探す
        StatusManager statusManager = GetComponentInParent<StatusManager>();

        // 2. "StatusManager" が見つかったか確認する（重要！）
        if (statusManager != null)
        {
            // 3. 見つけたStatusManagerの power を自分の damage に設定する
            this.damage = statusManager.power;
        }
        else
        {
            // 4. もし見つからなかった場合、コンソールにエラーを出す（デバッグに便利）
            Debug.LogError("親オブジェクトに StatusManager が見つかりません！", this.gameObject);
        }
    }
}