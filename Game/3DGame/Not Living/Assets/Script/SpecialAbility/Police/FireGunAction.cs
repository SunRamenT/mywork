using UnityEngine;

public class FireGunAction : MonoBehaviour, ISpecialAction
{
    [Header("能力設定")]
    [Tooltip("UIに表示する能力名")]
    public string abilityName = "発砲";
    [Tooltip("この能力のクールタイム（秒）")]
    public float cooldownDuration = 2f;

    [Header("銃の設定")]
    [Tooltip("発射する弾のプレハブ")]
    public GameObject bulletPrefab;
    [Tooltip("弾が発射される場所（銃口など）")]
    public Transform firePoint;
    [Tooltip("弾の速度")]
    public float bulletSpeed = 50f;
    [Tooltip("発砲時のアニメーショントリガー名")]
    public string fireTriggerName = "Fire";

    // --- 内部変数 ---
    private float nextActionTime = 0f;
    private Animator animator;

    // --- インターフェースの実装 ---
    public string AbilityName => abilityName;

    private Camera mainCamera; // ▼▼▼ 追加 ▼▼▼

    [Header("特殊能力サウンド設定")]
    [Tooltip("特殊能力使用時に再生する音")]
    public AudioClip specialAbilitySound;
    private AudioSource audioSource;


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
        animator = GetComponent<Animator>();
        mainCamera = Camera.main; // ▼▼▼ 追加 ▼▼▼
        // AudioSourceを自分自身から取得、またはなければ追加する
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// PlayerControllerから右クリックで呼び出される
    /// </summary>
    public void PerformAction(PlayerController playerController)
    {
        if (Time.time < nextActionTime)
        {
            Debug.Log("銃はクールタイム中です。");
            return;
        }
        
        Debug.Log("発砲シーケンスを開始！");

        // 発砲アニメーションを再生するだけ
        if (animator != null)
        {
            animator.SetTrigger(fireTriggerName);
        }
        
        // クールタイムタイマーをリセット
        nextActionTime = Time.time + cooldownDuration;
    }

    // ▼▼▼ 新しいメソッドを追加 ▼▼▼
    /// <summary>
    /// アニメーションイベントからこのメソッドを呼び出す
    /// </summary>
    public void FireBullet()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // --- ▼▼▼ 弾の発射ロジックを全面的に修正 ▼▼▼ ---

            // 1. カメラの中心からレイを飛ばし、着弾点を決定する
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 targetPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 999f))
            {
                targetPoint = hit.point; // レイが当たった場所
            }
            else
            {
                targetPoint = ray.GetPoint(100); // 何にも当たらなければ、100m先を狙う
            }

            // 2. 銃口から着弾点への方向を計算する
            Vector3 direction = (targetPoint - firePoint.position).normalized;

            // 3. 弾を生成し、計算した方向に向ける
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));

            // 4. 弾に初期設定と前進する力を与える
            if (bullet.TryGetComponent<Bullet>(out var bulletScript))
            {
                bulletScript.Initialize(this.gameObject);
            }
            if (bullet.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = direction * bulletSpeed;
            }
            // --- ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲ ---
            // 1. 自分のスピーカーで音を鳴らす
            if (specialAbilitySound != null)
            {
                audioSource.PlayOneShot(specialAbilitySound);
            }
        }
    }
}