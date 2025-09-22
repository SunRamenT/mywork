// InteractiveSignboard.cs
using UnityEngine;
using TMPro;

public class InteractiveSignboard : MonoBehaviour
{
    [Header("UI設定")]
    [SerializeField] private GameObject messageUIPrefab;
    [SerializeField] private Canvas targetCanvas;

    [Header("メッセージ内容")]
    [TextArea(3, 10)]
    [SerializeField] private string message;

    // --- 内部変数 ---
    private GameObject messageUIInstance;
    private Animator uiAnimator;
    private TextMeshProUGUI messageText;

    // ▼▼▼ PlayerControllerから呼ばれる公開メソッド ▼▼▼
    
    /// <summary>
    /// プレイヤーが範囲内に入った時に呼び出される
    /// </summary>
    public void OnPlayerEnter()
    {
        if (messageUIInstance == null)
        {
            if (messageUIPrefab == null || targetCanvas == null)
            {
                Debug.LogError("メッセージUIのプレハブまたはCanvasが設定されていません！", this);
                return;
            }
            messageUIInstance = Instantiate(messageUIPrefab, targetCanvas.transform);
            uiAnimator = messageUIInstance.GetComponent<Animator>();
            messageText = messageUIInstance.GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (messageText != null) messageText.text = message;

        if (uiAnimator != null)
        {
            messageUIInstance.SetActive(true);
            uiAnimator.SetBool("IsShown", true);
        }
    }

    /// <summary>
    /// プレイヤーが範囲外に出た時に呼び出される
    /// </summary>
    public void OnPlayerExit()
    {
        if (messageUIInstance != null && uiAnimator != null)
        {
            uiAnimator.SetBool("IsShown", false);
        }
    }

    // ▼▼▼ Updateメソッドは不要になったので削除 ▼▼▼
    // private void Update() { ... }
}