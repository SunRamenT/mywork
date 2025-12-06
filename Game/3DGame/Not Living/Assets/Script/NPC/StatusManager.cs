using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using UniRx;

public class StatusManager : MonoBehaviour
{
    [Header("基本ステータス")]
    public int maxHp = 100;
    public int currentHp;
    public int power = 10;
    public float speed = 3f;
    public float jumpPower = 5f;
    [Tooltip("このキャラクターが立てる足音の大きさ（聞こえる半径）")] // ▼▼▼ 追加 ▼▼▼
    public float footstepVolume = 10f;

    [Header("自動回復")]
    public int hourlyHealAmount = 10;

    [Header("評判システム")]
    [Range(0, 100)]
    public int reputation = 50;
    public string currentPopularity;

    [Header("ダメージと無敵")]
    [Tooltip("ダメージを受けた際の、デフォルトの無敵時間")]
    public float invincibilityDuration = 0.7f;
    [Tooltip("無敵時間中の点滅間隔（秒）")]
    public float invincibilityBlinkInterval = 0.2f;
    private bool isInvincible = false;    
    [Tooltip("このキャラクターに攻撃が当たった時のヒットストップ時間")] // ▼▼▼ 追加 ▼▼▼
    private float hitStopDuration = 0.5f;
    [Tooltip("ダメージを受けた際のノックバックの強さ")] // ▼▼▼ 追加 ▼▼▼
    public float knockbackForce = 3f;
    [Tooltip("ダメージを受けた際のノックバックの時間")] // ▼▼▼ 追加 ▼▼▼
    public float knockbackDuration = 0.2f;

    [Header("UI設定")]
    public GameObject healthBarCanvas; 
    public Slider healthBarSlider; 

    [Header("その他")]
    [Tooltip("乗っ取り中に倒した時に落とす霊魂アイテム")]
    public GameObject recoveryItemPrefab;
    [Tooltip("アイテムをドロップする高さのオフセット")] // 
    public float itemDropOffsetY = 1.0f;
    [Header("コンポーネント参照")] // 
    [Tooltip("点滅させるキャラクター本体のレンダラー")]
    public Renderer characterModelRenderer;
    
    [Tooltip("ダメージアニメーションのトリガー名")]
    public string damageTriggerName = "Damage";

    [Tooltip("ダメージアニメーションのトリガー名")]
    private string deathTriggerName = "Death";

    private bool isDodgeInvincible = false; // ▼▼▼ 回避無敵中のフラグを追加 ▼▼▼
    [Tooltip("このキャラクターに攻撃が当たった時のヒットストップ時間")] // ▼▼▼ 追加 ▼▼▼
    public float dodgeStopDuration = 0.7f;
    [Tooltip("回避成功時に発動する無敵時間")] // ▼▼▼ 追加 ▼▼▼
    public AudioClip dodgeSuccessSound;

    //private Renderer modelRenderer;
    private int lastHealHour = -1;
    private NPCMove npcMove;

    private NavMeshAgent agent;
    private Animator animator; 

    [Header("エフェクト設定")] 
    [Tooltip("このキャラクターが攻撃を当てた時に出すヒットエフェクト")]
    public GameObject hitEffectPrefab;
    [Tooltip("ヒットエフェクトの高さを調整するオフセット")] 
    public float hitEffectOffsetY = 0.4f;

    [Header("サウンド設定")] // ▼▼▼ 追加 ▼▼▼
    [Tooltip("攻撃がヒットした時に再生する効果音")]
    public AudioClip hitSound;
    private AudioSource audioSource; // ▼▼▼ 追加 ▼▼▼
    [Tooltip("このキャラクターの歩行音")] // ▼▼▼ 追加 ▼▼▼
    public AudioClip footstepSound;

    [Header("ランダム追跡者スポーン設定")]
    [Tooltip("追跡者として出現させる敵Prefabリスト")]
    public GameObject[] chaserPrefabs;

    [Tooltip("追跡者が出現する確率（0〜1）")]
    [Range(0f, 1f)]
    public float chaserSpawnChance = 0.3f;

    [Tooltip("追跡者が出現する距離（倒された位置から）")]
    public float chaserSpawnRadius = 1f;

    [Tooltip("追跡者が出現する時に再生する効果音")]
    public AudioClip spawnSound;

    public bool IsDead { get; private set; } = false;
    private AlignmentManager Alignment;

    private void Awake()
    {
        Alignment = GetComponent<AlignmentManager>();
        npcMove = GetComponent<NPCMove>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>(); 
    }

    private void OnEnable()
    {
        GameTimeManager.OnTimeChanged += HandleTimeChange;
    }

    private void OnDisable()
    {
        GameTimeManager.OnTimeChanged -= HandleTimeChange;
    }

    void Start()
    {
        //治安システムによる最大HPの調整
        if (Alignment.GoodEvilValue <= -10f)
        {
            maxHp = maxHp + (int)(maxHp * 0.3f); // 善寄りなら30%増加
        }
        else if (Alignment.GoodEvilValue >= 10f)
        {
            maxHp = maxHp - (int)(maxHp * 0.3f); // 善寄りなら30%減少
        }
        
        currentHp = maxHp;

        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = maxHp;
            healthBarSlider.value = currentHp;
        }


        UpdateHealthBarVisibility();
    }

    void Update()
    {
        UpdatePopularity();

        if (currentHp > maxHp)
        {
            currentHp = maxHp;
        }

        if (currentHp <= 0)
        {
            Die(null);
        }
    }

    void LateUpdate()
    {
        if (healthBarCanvas != null && healthBarCanvas.activeSelf && Camera.main != null)
        {
            healthBarCanvas.transform.LookAt(healthBarCanvas.transform.position + Camera.main.transform.forward);
        }
    }

    public void TakeDamage(int damage, GameObject attacker)
    {
        // 1. 回避無敵中か？
        if (isDodgeInvincible)
        {
            Debug.Log("ジャスト回避成功！");

            if (dodgeSuccessSound != null)
            {
                audioSource.PlayOneShot(dodgeSuccessSound);
            }
            // PlayerTimeManagerにスローモーションの開始を命令
            // (例: 0.1倍速で2秒間)
            if (PlayerTimeManager.Instance != null)
            {
                float dodgeStopMagnitude = 0.1f;
                float dodgeStopDuration = 3f;
                PlayerTimeManager.Instance.StartSlowMotion(dodgeStopMagnitude, dodgeStopDuration);
            }
            return; 
        }

        // 2. 通常の無敵時間中か？
        if (isInvincible) return;

        // ▼▼▼ ダメージアニメーションとノックバック処理を追加 ▼▼▼

        // 1. ダメージアニメーションのトリガーを起動
        if (HasParameter(animator, damageTriggerName))
        {
            animator.SetTrigger(damageTriggerName);
        }

        // 2. 攻撃者が存在する場合のみ、ノックバック処理を開始
        if (attacker != null)
        {
            Vector3 knockbackDirection = (transform.position - attacker.transform.position).normalized;
            ApplyKnockback(knockbackDirection, knockbackForce, knockbackDuration);
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        // 攻撃側の乗っ取り状態を確認
        bool isAttackerPossessed = false;
        if (attacker != null && attacker.GetComponent<NPCMove>() != null)
        {
            NPCMove attackerNpcMove = attacker.GetComponent<NPCMove>();
            if (attackerNpcMove != null)
            {
                isAttackerPossessed = attackerNpcMove.isNottoried;
            }
        }

        // 自分（受けた側）の乗っ取り状態を確認
        bool isVictimPossessed = (this.npcMove != null && this.npcMove.isNottoried);


        currentHp -= damage;
        UpdateHealthBarVisibility();

        if (currentHp <= 0)
        {
            Die(attacker);
            return;
        }

        if (attacker != null && npcMove != null && attacker != this.gameObject)
        {
            npcMove.StartRetaliation(attacker);
            
        }

        if (attacker != null)
        {
            PlayerController playerController = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
            if (playerController.isDodging == true)
            {
                Debug.Log("攻撃者は回避無敵中！");
                return; // 攻撃者が回避無敵中であれば、ここで終了
            }
        }

        
        // どちらかが乗っ取られている場合のみヒットストップを発動
        if (isAttackerPossessed || isVictimPossessed)
        {
            if (HitStopManager.Instance != null)
            {
                HitStopManager.Instance.ApplyHitStop(hitStopDuration, this);
            }
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        if (currentHp > 0)
        {
            StartCoroutine(BecomeInvincible());
        }
    }
    
    // ▼▼▼ 回避用の新しい無敵化コルーチンを追加 ▼▼▼
    /// <summary>
    /// 回避専用の無敵状態を、指定した時間だけ有効にする
    /// </summary>
    public IEnumerator BecomeDodgeInvincible(float duration)
    {
        isDodgeInvincible = true;
        yield return new WaitForSeconds(duration);
        isDodgeInvincible = false;
    }

    private void Die(GameObject attacker)
    {
        if (!this.enabled) return;
        this.enabled = false;
        IsDead = true;

        animator.SetTrigger(deathTriggerName);

        Debug.Log($"{gameObject.name} は倒れた。");

        if (attacker != null)
        {
            NPCMove attackerMoveScript = attacker.GetComponent<NPCMove>();
            if (attackerMoveScript != null && attackerMoveScript.isNottoried)
            {
                StatusManager attackerStatus = attacker.GetComponent<StatusManager>();
                if (attackerStatus != null)
                {
                    // 攻撃者(attackerStatus)の評判を、倒された相手(this)の情報をもとに更新する
                    attackerStatus.UpdateReputationOnDefeat(this);
                }

                // スコアと善悪値のイベントを発行
                GameEvents.TriggerTargetDefeatedWithInfo(this);

                // 霊魂ドロップ処理
                if (recoveryItemPrefab != null)
                {
                    Vector3 dropPosition = transform.position + new Vector3(0, itemDropOffsetY, 0);
                    Instantiate(recoveryItemPrefab, dropPosition, Quaternion.identity);
                }
                // ▼▼▼ 一定確率でランダム追跡者をスポーン ▼▼▼
                TrySpawnRandomChaser();
            }
        }
        // CharacterControllerを無効化
        GetComponent<CharacterController>().enabled = false;
        Destroy(gameObject, 10f);
    }
    
    private void TrySpawnRandomChaser()
    {
        // 出現確率チェック
        if (chaserPrefabs == null || chaserPrefabs.Length == 0) return;
        if (Random.value > chaserSpawnChance) return;

        // ランダムにPrefabを選択
        GameObject prefab = chaserPrefabs[Random.Range(0, chaserPrefabs.Length)];
        if (prefab == null) return;
        

        // 出現音を再生
        if (spawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }

        // 出現位置をランダムにずらす
        Vector3 randomOffset = new Vector3(
            Random.Range(-chaserSpawnRadius, chaserSpawnRadius),
            0.5f,
            Random.Range(-chaserSpawnRadius, chaserSpawnRadius)
        );

        Vector3 spawnPos = transform.position + randomOffset;

        // NavMesh上の位置を補正
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        // 一定時間後に出現させる（例：5〜9秒のランダムディレイ）
        float delay = Random.Range(5f, 9f);
        StartCoroutine(SpawnChaserAfterDelay(prefab, spawnPos, delay));
    }

    private IEnumerator SpawnChaserAfterDelay(GameObject prefab, Vector3 spawnPos, float delay)
    {
        Debug.Log($"追跡者 {prefab.name} は {delay:F1} 秒後に出現予定…");
        yield return new WaitForSeconds(delay);

        if (prefab != null)
        {
            GameObject chaser = Instantiate(prefab, spawnPos, Quaternion.identity);
            Debug.Log($"ランダム追跡者 {chaser.name} が出現しました！（{delay:F1} 秒後）");
        }
    }

    /// <summary>
    /// このキャラクターの評判を指定した量だけ変動させ、範囲内に収める
    /// </summary>
    public void AddReputation(int amount)
    {
        reputation = Mathf.Clamp(reputation + amount, 0, 100);
        Debug.Log($"<color=cyan>[評判更新] {gameObject.name} の評判が {amount} 変動しました。現在値: {reputation}</color>");
        UpdatePopularity(); // 評判の文字列も更新
    }

    public void UpdateReputationOnDefeat(StatusManager victimStatus)
    {
        int reputationChange = 0;

        if (victimStatus.reputation >= 30) // Bad, Normal, Good...
        {
            reputationChange = -10;
        }
        else if (victimStatus.reputation >= 10) // So Bad
        {
            reputationChange = 10;
        }
        else // Worst
        {
            reputationChange = 40;
        }

        this.reputation = Mathf.Clamp(this.reputation + reputationChange, 0, 100);
        Debug.Log($"[評判更新] {victimStatus.gameObject.name}を倒したため、{this.gameObject.name}の評判が{reputationChange}変動しました。現在の評判: {this.reputation}");
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        // 乗っ取り中のNPCは吹き飛ばないようにする
        if (npcMove != null && npcMove.isNottoried) return;
        
        StartCoroutine(KnockbackCoroutine(direction, force, duration));
    }

    private IEnumerator KnockbackCoroutine(Vector3 direction, float force, float duration)
    {
        if (agent != null) agent.enabled = false;
        
        float timer = 0;
        while (timer < duration)
        {
            // Y方向には動かないように方向を水平にする
            direction.y = 0;
            transform.position += direction * force * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        if (agent != null) agent.enabled = true;
    }
    
    // ▼▼▼ Animatorのパラメータ存在確認メソッドを追加 ▼▼▼
    private bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    private void HandleTimeChange(int hour, int minute)
    {
        if (hour != lastHealHour)
        {
            lastHealHour = hour;

            if (currentHp < maxHp && currentHp > 0)
            {
                currentHp += hourlyHealAmount;
                if (currentHp > maxHp)
                {
                    currentHp = maxHp;
                }
                UpdateHealthBarVisibility();
            }
        }
    }
    
    private void UpdateHealthBarVisibility()
    {
        if (healthBarCanvas == null || healthBarSlider == null) return;
        
        bool shouldBeVisible = currentHp < maxHp;
        healthBarCanvas.SetActive(shouldBeVisible);
        
        healthBarSlider.value = currentHp;
    }

     // バージョン1：引数なし（これまで通り）
    /// Inspectorで設定されたデフォルトの無敵時間を適用する
    public IEnumerator BecomeInvincible()
    {
        // 引数ありのバージョンを、デフォルトの無敵時間で呼び出す
        yield return StartCoroutine(BecomeInvincible(this.invincibilityDuration));
    }

    //  バージョン2：引数あり（新しい）
    /// 引数で指定された時間だけ無敵になる
    public IEnumerator BecomeInvincible(float duration)
    {
        isInvincible = true;

        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            if (characterModelRenderer != null) characterModelRenderer.enabled = false;
            yield return new WaitForSeconds(invincibilityBlinkInterval / 2);
            if (characterModelRenderer != null) characterModelRenderer.enabled = true;
            yield return new WaitForSeconds(invincibilityBlinkInterval / 2);
        }
        
        if (characterModelRenderer != null)
        {
            characterModelRenderer.enabled = true;
        }

        isInvincible = false;
    }


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
    
    public void PlayFootstepSound()
    {
        // NPCMoveコンポーネントがあり、かつ「乗っ取られている」状態の場合のみ
        if (npcMove != null && npcMove.isNottoried)
        {
            // 設定された足音があれば再生する
            if (footstepSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(footstepSound);
            }
        }
        // 乗っ取られていない（AIで動いている）場合は、何もせずに処理を終了する
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // ヒットボックスのタグが "punch" でなければ何もしない
        if (!other.gameObject.CompareTag("punch")) return;

        // --- ▼▼▼ 自分自身への攻撃判定ロジックを修正 ▼▼▼ ---

        // 1. ヒットボックスから、攻撃者のキャラクター本体（NPCMoveを持つオブジェクト）を探す
        NPCMove attackerMoveScript = other.GetComponentInParent<NPCMove>();

        // 2. 攻撃者のキャラクター本体が見つからない場合は、無効な攻撃なので処理を中断
        if (attackerMoveScript == null) return;

        // 3. 攻撃者のキャラクター本体(attackerMoveScript.gameObject)と
        //    ダメージを受ける自分自身(this.gameObject)が同じなら、それは自分自身への攻撃なので処理を中断
        if (attackerMoveScript.gameObject == this.gameObject)
        {
            return;
        }

        // もし攻撃してきた相手が乗っ取られている（＝プレイヤー操作）場合のみ
        if (attackerMoveScript.isNottoried)
        {
            // 攻撃者のヒットボックスからSoundEmitterを探す
            SoundEmitter emitter = other.GetComponent<SoundEmitter>();
            // もし見つかったら音を発生させる
            if (emitter != null)
            {
                emitter.EmitSound();
            }
        }
       
        // 自分自身への攻撃でないことが確定したので、ダメージ処理に進む
        GameObject attacker = attackerMoveScript.gameObject;
        AttackInfo attackInfo = other.GetComponent<AttackInfo>();
        // 接触してきたヒットボックスから、攻撃者本体のStatusManagerを探す
        StatusManager attackerStatus = other.GetComponentInParent<StatusManager>();

        // 攻撃者が見つからなければ処理を中断
        if (attackerStatus == null) return;
        Debug.Log($"{this.gameObject.name} は {attacker.name} から攻撃を受けた！");


        // 2. ヒット音を再生 ▼▼▼ 追加 ▼▼▼
        if (attackerStatus.audioSource != null && attackerStatus.hitSound != null)
        {
            attackerStatus.audioSource.PlayOneShot(attackerStatus.hitSound);
        }

        // 1. ヒットエフェクトを即座に生成
        if (attackerStatus.hitEffectPrefab != null)
        {
            // ▼▼▼ エフェクトの生成位置を修正 ▼▼▼
            // 接触点の最も近い位置を基準にする
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            // そこから、攻撃者側で設定された高さオフセット分だけY座標を上げる
            Vector3 spawnPosition = hitPoint + new Vector3(0, attackerStatus.hitEffectOffsetY, 0);

            Quaternion hitRotation = Quaternion.LookRotation(attackerStatus.transform.forward);
            Instantiate(attackerStatus.hitEffectPrefab, spawnPosition, hitRotation);

        }

        if (attackInfo != null)
        {
            TakeDamage(attackInfo.damage, attacker);
            Debug.Log($"{this.gameObject.name} は {attackInfo.damage} のダメージを受けた。残りHP: {currentHp}/{maxHp}");
            if (isDodgeInvincible)
            { 
                other.enabled = false;// ヒットボックスを無効化して二重ヒットを防止
            }
        }
    }
}