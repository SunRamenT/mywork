using System.Collections;
using UnityEngine;

/// <summary>
/// NPCのステータス（HP、評判など）とダメージ処理を管理するクラス (3D版)
/// </summary>
public class StatusManager : MonoBehaviour
{
    [Header("基本ステータス")]
    public int maxHp = 100;
    public int currentHp;
    public int power = 10;
    public float speed = 3f;

    [Header("評判システム")]
    [Range(0, 100)]
    public int reputation = 50;
    public string currentPopularity;

    [Header("ダメージと無敵")]
    public float invincibilityDuration = 1.0f;
    private bool isInvincible = false;

    [Header("その他")]
    public GameObject recoveryItemPrefab;
    
    private Renderer modelRenderer;

    void Start()
    {
        currentHp = maxHp;
        modelRenderer = GetComponentInChildren<Renderer>();
        if (modelRenderer == null)
        {
            Debug.LogError("子オブジェクトにRendererが見つかりません。", this);
        }
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
    
    public void TakeDamage(int damage)
    {
        if (isInvincible) return;

        currentHp -= damage;
        Debug.Log($"{gameObject.name} は {damage} のダメージを受けた！ 残りHP: {currentHp}");

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
            
            // 接触相手のAttackInfoからダメージ量を取得
            AttackInfo attackInfo = other.GetComponent<AttackInfo>();
            if (attackInfo != null)
            {
                TakeDamage(attackInfo.damage);
            }
        }
    }
}