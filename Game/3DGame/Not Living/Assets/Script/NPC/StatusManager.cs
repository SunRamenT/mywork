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
    public GameObject recoveryItemPrefab;
    
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
            Die();
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
        Debug.Log($"{gameObject.name} は {damage} のダメージを受けた！ 残りHP: {currentHp}");

        if (npcMove != null && attacker != this.gameObject)
        {
            npcMove.StartRetaliation(attacker);
        }

        if (currentHp > 0)
        {
            StartCoroutine(BecomeInvincible());
        }
    }
    
    private void Die()
    {
        Debug.Log($"{gameObject.name} は倒れた。");

        if (recoveryItemPrefab != null)
        {
            Instantiate(recoveryItemPrefab, transform.position, Quaternion.identity);
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
                Debug.Log($"{gameObject.name} が {hourlyHealAmount} 回復した。");
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

            GameObject attacker = other.transform.parent.gameObject;
            
            AttackInfo attackInfo = other.GetComponent<AttackInfo>();
            if (attackInfo != null)
            {
                TakeDamage(attackInfo.damage, attacker);
            }
        }
    }
}