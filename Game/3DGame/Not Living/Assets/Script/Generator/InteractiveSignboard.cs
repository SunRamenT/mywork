using UnityEngine;
using TMPro;

// IInteractableインターフェースを実装する
public class InteractiveSignboard : MonoBehaviour, IInteractable
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

    /// <summary>
    /// プレイヤーが範囲内に入った時にPlayerControllerから呼び出される
    /// </summary>
    public void OnPlayerEnterRange()
    {
        // UIがまだ生成されていなければ、一度だけ生成する
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
        
        // メッセージ内容を更新
        if (messageText != null)
        {
            messageText.text = message;
        }

        // 表示アニメーションを実行
        if (uiAnimator != null)
        {
            messageUIInstance.SetActive(true);
            uiAnimator.SetBool("IsShown", true);
        }
    }

    /// <summary>
    /// プレイヤーが範囲外に出た時にPlayerControllerから呼び出される
    /// </summary>
    public void OnPlayerExitRange()
    {
        // 非表示アニメーションを実行
        if (messageUIInstance != null && uiAnimator != null)
        {
            uiAnimator.SetBool("IsShown", false);
        }
    }

    /// <summary>
    /// プレイヤーがインタラクトキーを押した時にPlayerControllerから呼び出される
    /// （看板は自動表示なので、この中身は空でOK）
    /// </summary>
    public void OnInteract(PlayerController playerController)
    {
        // 何もしない
    }
}