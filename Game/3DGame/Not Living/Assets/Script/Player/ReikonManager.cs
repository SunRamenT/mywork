using UnityEngine;
using UnityEngine.UI; // Imageコンポーネント用に必要
using TMPro;          // TextMeshPro用に必要
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

    // ▼▼▼ UI設定を新しいものに変更 ▼▼▼
    [Header("UI設定")]
    [Tooltip("青い炎を表示するImageコンポーネント")]
    public Image flameImage;
    [Tooltip("霊魂の数値を表示するTextMeshProUGUIコンポーネント")]
    public TextMeshProUGUI amountText;
    [Tooltip("霊魂が最大の時の炎の大きさ")]
    public Vector2 maxFlameSize = new Vector2(100f, 100f);
    [Tooltip("霊魂が0の時の炎の大きさ")]
    public Vector2 minFlameSize = new Vector2(20f, 20f);

    public static event Action OnSpiritDepleted;

    private bool isPhasing = false;
    private bool isPossessing = false;

    void Start()
    {
        currentSpirit = maxSpirit;
        UpdateSpiritUI(); // UIの初期表示を更新
    }

    void Update()
    {
        if (currentSpirit <= 0) return;

        float currentMultiplier = 1.0f;
        if (isPossessing)
        {
            currentMultiplier = possessionDrainMultiplier;
        }
        else if (isPhasing)
        {
            currentMultiplier = phasingDrainMultiplier;
        }
        
        currentSpirit -= baseDrainSpeed * currentMultiplier * Time.deltaTime;
        
        UpdateSpiritUI(); // 毎フレームUIを更新

        if (currentSpirit <= 0)
        {
            currentSpirit = 0;
            Debug.Log("霊魂が尽きた...ゲームオーバー");
            OnSpiritDepleted?.Invoke();
        }
    }

    // ▼▼▼ UI更新メソッドを新しいロジックに変更 ▼▼▼
    private void UpdateSpiritUI()
    {
        // 現在の霊魂の割合を計算 (0.0～1.0の範囲)
        float percentage = currentSpirit / maxSpirit;

        // 数値テキストの更新
        if (amountText != null)
        {
            // Mathf.CeilToIntで小数点以下を切り上げて整数にする
            amountText.text = Mathf.CeilToInt(currentSpirit).ToString();
        }

        // 炎の大きさの更新
        if (flameImage != null)
        {
            // Vector2.Lerpを使って、最小サイズと最大サイズの間を割合に応じて線形補間する
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
        UpdateSpiritUI(); // UIを更新
        Debug.Log($"{amount} の霊魂を回復！ 現在値: {currentSpirit}");
    }

    public void TakeDamage(float amount)
    {
        currentSpirit -= amount;
        UpdateSpiritUI(); // UIを更新
        Debug.Log($"{amount} の霊魂ダメージ！ 現在値: {currentSpirit}");
    }
}