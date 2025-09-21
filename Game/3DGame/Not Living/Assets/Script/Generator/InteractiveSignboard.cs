using UnityEngine;
using TMPro; // TextMeshProを使用

public class InteractiveSignboard : MonoBehaviour
{
    [Header("UI設定")]
    [Tooltip("表示するメッセージUIのプレハブ")]
    [SerializeField] private GameObject messageUIPrefab;
    [Tooltip("UIを配置するCanvas")]
    [SerializeField] private Canvas targetCanvas;

    [Header("メッセージ内容")]
    [Tooltip("Inspectorで設定するメッセージ内容")]
    [TextArea(3, 10)] // 複数行入力できるようにする
    [SerializeField] private string message;

    // --- 内部で使う変数 ---
    private GameObject messageUIInstance; // 生成したUIのインスタンス
    private Animator uiAnimator; // UIのアニメーションを制御
    private TextMeshProUGUI messageText; // 表示するテキスト

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤー（Ghost）が範囲内に入ったら
        if (other.CompareTag("Player"))
        {
            // まだメッセージが表示されていなければ、新しく生成する
            if (messageUIInstance == null)
            {
                // プレハブとCanvasが設定されているか確認
                if (messageUIPrefab == null || targetCanvas == null)
                {
                    Debug.LogError("メッセージUIのプレハブまたはCanvasが設定されていません！", this);
                    return;
                }

                // UIを生成し、Canvasの子にする
                messageUIInstance = Instantiate(messageUIPrefab, targetCanvas.transform);

                // UIから必要なコンポーネントを取得
                uiAnimator = messageUIInstance.GetComponent<Animator>();
                // TextMeshProコンポーネントを子オブジェクトから探す
                messageText = messageUIInstance.GetComponentInChildren<TextMeshProUGUI>();
            }

            // テキストを設定し、表示アニメーションを再生
            if (messageText != null)
            {
                messageText.text = message;
            }
            if (uiAnimator != null)
            {
                // "Show"という名前のアニメーションステートに遷移させる
                uiAnimator.SetBool("IsShown", true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // プレイヤーが範囲外に出たら
        if (other.CompareTag("Player"))
        {
            // メッセージが表示されていれば、非表示アニメーションを再生
            if (messageUIInstance != null && uiAnimator != null)
            {
                uiAnimator.SetBool("IsShown", false);
            }
            // アニメーションの終了はアニメーターが自動で検知してGameObjectを非アクティブにする（後述）
        }
    }
}