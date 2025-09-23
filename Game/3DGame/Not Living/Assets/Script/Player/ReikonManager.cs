using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ReikonManager : MonoBehaviour
{
    [Header("霊魂（体力）設定")]
    public float maxSpirit = 100f;
    [SerializeField]
    private float currentSpirit;

    [Header("霊魂の減少速度")]
    public float baseDrainSpeed = 1f;
    public float phasingDrainMultiplier = 2f;
    public float possessionDrainMultiplier = 1.5f;
    [Tooltip("Chaser接近時の減少速度の倍率")]
    public float chaserDrainMultiplier = 3f;

    [Header("UI設定")]
    [Tooltip("青い炎を表示するImageコンポーネント")]
    public Image flameImage;
    [Tooltip("霊魂の数値を表示するTextMeshProUGUIコンポーネント")]
    public TextMeshProUGUI amountText;
    [Tooltip("霊魂が最大の時の炎の大きさ")]
    public Vector2 maxFlameSize = new Vector2(100f, 100f);
    [Tooltip("霊魂が0の時の炎の大きさ")]
    public Vector2 minFlameSize = new Vector2(20f, 20f);
    [Tooltip("霊魂の減少が加速している時に表示するエフェクト")]
    public GameObject debuffEffect;

    public static event Action OnSpiritDepleted;

    private bool isPhasing = false;
    private bool isPossessing = false;
    private int nearbyChaserCount = 0;

    void Start()
    {
        currentSpirit = maxSpirit;
        UpdateSpiritUI();
        
        if (debuffEffect != null)
        {
            debuffEffect.SetActive(false);
        }
    }

    void Update()
    {
        if (currentSpirit <= 0) return;

        float currentMultiplier = 1.0f;
        
        // 優先順位1: Chaser接近中
        if (nearbyChaserCount > 0)
        {
            currentMultiplier = chaserDrainMultiplier;
        }
        // 優先順位2: 憑依中
        else if (isPossessing)
        {
            currentMultiplier = possessionDrainMultiplier;
        }
        // 優先順位3: 壁抜け中
        else if (isPhasing)
        {
            currentMultiplier = phasingDrainMultiplier;
        }
        
        // デバフエフェクトの表示判定
        if (debuffEffect != null)
        {
            bool isDebuffed = currentMultiplier > 1.0f;
            debuffEffect.SetActive(isDebuffed);
        }

        currentSpirit -= baseDrainSpeed * currentMultiplier * Time.deltaTime;
        
        UpdateSpiritUI();

        if (currentSpirit <= 0)
        {
            currentSpirit = 0;
            Debug.Log("霊魂が尽きた...ゲームオーバー");
            OnSpiritDepleted?.Invoke();
        }
    }

    /// <summary>
    /// Chaserの危険オーラに入った時に呼ばれる
    /// </summary>
    public void OnChaserEnterAura()
    {
        nearbyChaserCount++;
    }

    /// <summary>
    /// Chaserの危険オーラから出た時に呼ばれる
    /// </summary>
    public void OnChaserExitAura()
    {
        if (nearbyChaserCount > 0)
        {
            nearbyChaserCount--;
        }
    }

    private void UpdateSpiritUI()
    {
        float percentage = currentSpirit / maxSpirit;

        if (amountText != null)
        {
            amountText.text = Mathf.CeilToInt(currentSpirit).ToString();
        }
        if (flameImage != null)
        {
            flameImage.rectTransform.sizeDelta = Vector2.Lerp(minFlameSize, maxFlameSize, percentage);
        }
    }
    
    public void UpdateState(bool isPhasing, bool isPossessing)
    {
        this.isPhasing = isPhasing;
        this.isPossessing = isPossessing;
    }
    
    public void Heal(float amount)
    {
        currentSpirit = Mathf.Clamp(currentSpirit + amount, 0, maxSpirit);
        UpdateSpiritUI();
        Debug.Log($"{amount} の霊魂を回復！ 現在値: {currentSpirit}");
    }

    public void TakeDamage(float amount)
    {
        currentSpirit -= amount;
        UpdateSpiritUI();
        Debug.Log($"{amount} の霊魂ダメージ！ 現在値: {currentSpirit}");
    }
}