using UnityEngine;
using System;

public class ReikonManager : MonoBehaviour
{
    [Header("霊魂（体力）設定")]
    [Tooltip("霊魂の最大値")]
    public float maxSpirit = 100f;
    [Tooltip("現在の霊魂")]
    [SerializeField]
    private float currentSpirit;

    [Header("霊魂の減少速度")]
    [Tooltip("通常時の1秒あたりの減少量")]
    public float baseDrainSpeed = 1f;
    [Tooltip("壁抜け時の減少速度の倍率")]
    public float phasingDrainMultiplier = 2f;

    [Header("UI設定")]
    [Tooltip("霊魂の残量を表示するUIオブジェクト")]
    public Transform spiritBar;

    public static event Action OnSpiritDepleted;

    private Vector3 initialBarScale;
    private bool isPhasing = false;

    void Start()
    {
        currentSpirit = maxSpirit;
        if (spiritBar != null)
        {
            initialBarScale = spiritBar.localScale;
        }
        
        PlayerController player = GetComponent<PlayerController>();
        if (player != null)
        {
            isPhasing = !player.IsCollisionsEnabled();
        }
    }

    void Update()
    {
        if (currentSpirit <= 0)
        {
            return;
        }

        float currentDrainSpeed = baseDrainSpeed;
        if (isPhasing)
        {
            currentDrainSpeed *= phasingDrainMultiplier;
        }

        currentSpirit -= currentDrainSpeed * Time.deltaTime;
        
        UpdateSpiritBar();

        if (currentSpirit <= 0)
        {
            currentSpirit = 0;
            Debug.Log("霊魂が尽きた...ゲームオーバー");
            OnSpiritDepleted?.Invoke();
        }
    }

    private void UpdateSpiritBar()
    {
        if (spiritBar != null)
        {
            float percentage = currentSpirit / maxSpirit;
            spiritBar.localScale = new Vector3(initialBarScale.x * percentage, initialBarScale.y, initialBarScale.z);
        }
    }
    
    public void SetPhasingState(bool phasing)
    {
        this.isPhasing = phasing;
    }
    
    public void Heal(float amount)
    {
        currentSpirit += amount;
        if (currentSpirit > maxSpirit)
        {
            currentSpirit = maxSpirit;
        }
        UpdateSpiritBar();
        Debug.Log($"{amount} の霊魂を回復！ 現在値: {currentSpirit}");
    }

    public void TakeDamage(float amount)
    {
        currentSpirit -= amount;
        UpdateSpiritBar();
        Debug.Log($"{amount} の霊魂ダメージ！ 現在値: {currentSpirit}");
    }
}