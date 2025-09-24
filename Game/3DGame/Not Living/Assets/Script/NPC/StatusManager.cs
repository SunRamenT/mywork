using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class StatusManager : MonoBehaviour
{
    [Header("基本ステータス")]
    public int maxHp = 100;
    public int currentHp;
    public int power = 10;
    public float speed = 3f;
    public float jumpPower = 5f;

    [Header("自動回復")]
    public int hourlyHealAmount = 10;

    [Header("評判システム")]
    [Range(0, 100)]
    public int reputation = 50;
    public string currentPopularity;

    [Header("ダメージと無敵")]
    [Tooltip("ダメージを受けた際の、デフォルトの無敵時間")]
    public float invincibilityDuration = 1.0f;
    [Tooltip("無敵時間中の点滅間隔（秒）")]
    public float invincibilityBlinkInterval = 0.2f;
    private bool isInvincible = false;

    [Header("UI設定")]
    public GameObject healthBarCanvas; 
    public Slider healthBarSlider; 

    [Header("その他")]
    [Tooltip("乗っ取り中に倒した時に落とす霊魂アイテム")]
    public GameObject recoveryItemPrefab;
    [Tooltip("アイテムをドロップする高さのオフセット")] // ▼▼▼ 変数を追加 ▼▼▼
    public float itemDropOffsetY = 1.0f;
    [Header("コンポーネント参照")] // ▼▼▼ 新しいヘッダーを追加 ▼▼▼
    [Tooltip("点滅させるキャラクター本体のレンダラー")]
    public Renderer characterModelRenderer;

    //private Renderer modelRenderer;
    private int lastHealHour = -1;
    private NPCMove npcMove;

    private NavMeshAgent agent;

    private void Awake()
    {
        // modelRenderer = GetComponentInChildren<Renderer>(); // ← この行を削除またはコメントアウト
        npcMove = GetComponent<NPCMove>();
        agent = GetComponent<NavMeshAgent>();
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
        if (isInvincible)
        {
            Debug.Log("無敵状態のため、ダメージを無効化しました！");
            return; // isInvincibleがtrueなら、ここで処理を中断する
        }

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

        // ダメージを受けた後、無敵状態を開始
        if (currentHp > 0)
        {
            StartCoroutine(BecomeInvincible());
        }
    }
    
    private void Die(GameObject attacker)
    {
        if (!this.enabled) return;
        this.enabled = false;

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
            }
        }
        Destroy(gameObject);
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

    /// 外部（爆弾など）から呼び出され、吹き飛ばし処理を開始する
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        // isNottoriedフラグがない場合はnpcMoveで代用
        if (npcMove != null && npcMove.isNottoried)
        {
            // 乗っ取り中のNPCは吹き飛ばないようにする
            return;
        }
        StartCoroutine(KnockbackCoroutine(direction, force, duration));
    }

    private IEnumerator KnockbackCoroutine(Vector3 direction, float force, float duration)
    {
        // 吹き飛ばされている間、AIの動きを止める
        if (agent != null)
        {
            agent.enabled = false;
        }
        
        float timer = 0;
        while (timer < duration)
        {
            // キャラクターを吹き飛ばし方向に動かす
            // （CharacterControllerを持つオブジェクトでも機能する）
            transform.position += direction * force * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        // 吹き飛ばしが終わったら、AIの動きを元に戻す
        if (agent != null)
        {
            agent.enabled = true;
        }
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
    
    
    private void OnTriggerEnter(Collider other)
    {
        // ヒットボックスのタグが "punch" でなければ何もしない
        if(!other.gameObject.CompareTag("punch")) return;

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

        // --- ▲▲▲ 修正ここまで ▲▲▲ ---

        // 自分自身への攻撃でないことが確定したので、ダメージ処理に進む
        GameObject attacker = attackerMoveScript.gameObject;
        AttackInfo attackInfo = other.GetComponent<AttackInfo>();
        if (attackInfo != null)
        {
            TakeDamage(attackInfo.damage, attacker);
        }
    }
}