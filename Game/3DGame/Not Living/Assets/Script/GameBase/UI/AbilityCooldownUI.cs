using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Image))]
public class AbilityCooldownUI : MonoBehaviour
{
    [Header("コンポーネント参照")]
    [Tooltip("円グラフ表示用のImage")]
    [SerializeField] private Image cooldownImage;
    [Tooltip("能力名表示用のテキスト")]
    [SerializeField] private TextMeshProUGUI nameText;

    private PlayerController playerController;

    void Start()
    {
        if (cooldownImage == null)
        {
            cooldownImage = GetComponent<Image>();
        }
        
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Top;

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("シーンにPlayerControllerが見つかりません！");
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        ISpecialAction currentAction = playerController.CurrentSpecialAction;

        if (currentAction != null)
        {
            // ▼▼▼ このブロックのロジックを修正 ▼▼▼
            
            // 能力を持っていれば、ゲージとテキストの両方を有効化
            cooldownImage.enabled = true;
            if (nameText != null)
            {
                nameText.enabled = true;
            }

            // ゲージの量を更新 (クールタイム完了時は1.0になり、満タンで表示される)
            cooldownImage.fillAmount = currentAction.CooldownProgress;
            
            // テキストを更新
            if (nameText != null)
            {
                nameText.text = currentAction.AbilityName;
            }
            
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }
        else
        {
            // 能力がなければゲージもテキストも非表示にする
            cooldownImage.enabled = false;
            if (nameText != null)
            {
                nameText.enabled = false;
            }
        }
    }
}