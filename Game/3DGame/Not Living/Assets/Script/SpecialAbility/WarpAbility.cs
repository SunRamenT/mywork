using UnityEngine;
using UnityEngine.AI; // NavMeshを使うために必要

[RequireComponent(typeof(CharacterController))]
public class WarpAbility : MonoBehaviour, ISpecialAction
{
    [Header("ワープ設定")]
    [Tooltip("ワープする最大の半径")]
    public float warpRadius = 15f;
    [Tooltip("この能力のクールタイム（秒）")]
    public float cooldownDuration = 5f;
    [Tooltip("UIに表示する能力名")]
    public string abilityName = "ワープ";
    [Tooltip("ワープできる現在地からの最大高度差")] // ▼▼▼ 追加 ▼▼▼
    public float maxHeightDifference = 3f;
    [Tooltip("ワープ先を探す試行回数")] // ▼▼▼ 追加 ▼▼▼
    public int maxWarpAttempts = 10;


    [Header("エフェクト設定")]
    public GameObject warpOutEffectPrefab;
    public GameObject warpInEffectPrefab;
    
    // --- 内部変数 ---
    private CharacterController characterController;
    private float nextActionTime = 0f;

    // --- インターフェースの実装 ---
    public string AbilityName => abilityName;
    public float CooldownProgress
    {
        get
        {
            if (Time.time >= nextActionTime) return 1.0f;
            float startTime = nextActionTime - cooldownDuration;
            float elapsedTime = Time.time - startTime;
            return elapsedTime / cooldownDuration;
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// PlayerControllerから右クリックで呼び出される
    /// </summary>
    public void PerformAction(PlayerController playerController)
    {
        if (playerController.IsPossessing()) return;
        if (Time.time < nextActionTime)
        {
            Debug.Log("ワープはクールタイム中です。");
            return;
        }
        
        // --- ワープ実行ロジック ---

        // 有効なワープ先が見つかるまで、指定回数だけ試行する
        for (int i = 0; i < maxWarpAttempts; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * warpRadius;
            randomDirection += transform.position;

            // NavMesh上の有効な地面を探す
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, warpRadius, NavMesh.AllAreas))
            {
                // ▼▼▼ 高度差をチェックする処理を追加 ▼▼▼
                // 見つかった地点と現在地のY座標の差が、許容範囲内か確認
                if (Mathf.Abs(hit.position.y - transform.position.y) <= maxHeightDifference)
                {
                    // --- 条件をクリアしたので、ワープを実行 ---
                    WarpToPosition(hit.position);
                    return; // 成功したのでメソッドを抜ける
                }
                // もし高すぎたら、このforループの次の試行に移る
            }
        }

        // forループが最後まで回っても有効な地点が見つからなかった場合
        Debug.LogWarning("ワープ可能な有効な地点が見つかりませんでした。");
    }
    
    /// <summary>
    /// 指定した座標へキャラクターをワープさせる
    /// </summary>
    private void WarpToPosition(Vector3 position)
    {
        // ワープ元エフェクトを再生
        if (warpOutEffectPrefab != null) Instantiate(warpOutEffectPrefab, transform.position, Quaternion.identity);

        // CharacterControllerを一時的に無効化
        characterController.enabled = false;
        // 地面にめり込まないように、少しだけ上にオフセットして座標を更新
        transform.position = position + Vector3.up * 0.1f;
        // CharacterControllerを再度有効化
        characterController.enabled = true;

        // ワープ先エフェクトを再生
        if (warpInEffectPrefab != null) Instantiate(warpInEffectPrefab, transform.position, Quaternion.identity);

        // クールタイムをリセット
        nextActionTime = Time.time + cooldownDuration;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, warpRadius);
    }
}