using UnityEngine;
using UnityEngine.AI;

// ▼▼▼ IInteractableではなく、ISpecialActionを実装する ▼▼▼
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

    [Header("エフェクト設定")]
    public GameObject warpOutEffectPrefab;
    public GameObject warpInEffectPrefab;
    
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

    // ▼▼▼ Updateから入力検知を削除し、PerformActionに処理を移す ▼▼▼
    // private void Update() { ... }

    /// <summary>
    /// PlayerControllerから右クリックで呼び出される
    /// </summary>
    public void PerformAction(PlayerController playerController)
    {
        // 幽霊状態でない場合は、この能力を使えない
        if (playerController.IsPossessing()) return;
        
        // クールタイムが終わっていなければ何もしない
        if (Time.time < nextActionTime)
        {
            Debug.Log("ワープはクールタイム中です。");
            return;
        }
        
        // --- ワープ実行 ---
        Vector3 randomDirection = Random.insideUnitSphere * warpRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, warpRadius, NavMesh.AllAreas))
        {
            if (warpOutEffectPrefab != null) Instantiate(warpOutEffectPrefab, transform.position, Quaternion.identity);

            characterController.enabled = false;
            transform.position = hit.position;
            characterController.enabled = true;

            if (warpInEffectPrefab != null) Instantiate(warpInEffectPrefab, hit.position, Quaternion.identity);

            nextActionTime = Time.time + cooldownDuration;
        }
        else
        {
            Debug.LogWarning("ワープ可能な有効な地点が見つかりませんでした。");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, warpRadius);
    }
}