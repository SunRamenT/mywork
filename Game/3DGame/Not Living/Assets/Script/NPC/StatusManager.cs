using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    public float invincibilityDuration = 1.0f;
    private bool isInvincible = false;

    [Header("UI設定")]
    public GameObject healthBarCanvas; 
    public Slider healthBarSlider; 

    [Header("その他")]
    [Tooltip("乗っ取り中に倒した時に落とす霊魂アイテム")]
    public GameObject recoveryItemPrefab;
    [Tooltip("アイテムをドロップする高さのオフセット")] // ▼▼▼ 変数を追加 ▼▼▼
    public float itemDropOffsetY = 1.0f;
    
    private Renderer modelRenderer;
    private int lastHealHour = -1;
    private NPCMove npcMove;

    private void Awake()
    {
        modelRenderer = GetComponentInChildren<Renderer>();
        npcMove = GetComponent<NPCMove>();
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
        if (isInvincible) return;

        currentHp -= damage;
        UpdateHealthBarVisibility();

        if (currentHp <= 0)
        {
            Die(attacker);
            return;
        }

        if (npcMove != null && attacker != this.gameObject)
        {
            npcMove.StartRetaliation(attacker);
        }

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

        if (attacker != null && recoveryItemPrefab != null)
        {
            NPCMove attackerMoveScript = attacker.GetComponent<NPCMove>();
            
            if (attackerMoveScript != null && attackerMoveScript.isNottoried)
            {
                // ▼▼▼ アイテムの生成位置を修正 ▼▼▼
                // キャラクターの位置に、Y軸のオフセットを加算する
                Vector3 dropPosition = transform.position + new Vector3(0, itemDropOffsetY, 0);
                Instantiate(recoveryItemPrefab, dropPosition, Quaternion.identity);
                // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

                Debug.Log($"乗っ取られた {attacker.name} が倒したため、{gameObject.name} は霊魂をドロップした！");
            }
        }
        
        Destroy(gameObject);
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

    IEnumerator BecomeInvincible()
    {
        isInvincible = true;
        
        float endTime = Time.time + invincibilityDuration;
        while (Time.time < endTime)
        {
            if(modelRenderer != null) modelRenderer.enabled = false;
            yield return new WaitForSeconds(0.1f);
            if(modelRenderer != null) modelRenderer.enabled = true;
            yield return new WaitForSeconds(0.1f);
        }

        if(modelRenderer != null) modelRenderer.enabled = true;
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
        if(other.gameObject.CompareTag("punch"))
        {
            if (other.transform.root == this.transform.root)
            {
                return;
            }

            NPCMove attackerMoveScript = other.GetComponentInParent<NPCMove>();

            if (attackerMoveScript != null)
            {
                GameObject attacker = attackerMoveScript.gameObject;
                
                AttackInfo attackInfo = other.GetComponent<AttackInfo>();
                if (attackInfo != null)
                {
                    TakeDamage(attackInfo.damage, attacker);
                }
            }
        }
    }
}