using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class TrashItem : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    [Header("ワールド空間UI")]
    public GameObject statusUIPrefab;
    [Tooltip("オブジェクトの基点からのUIのオフセット")]
    public Vector3 statusUIOffset = new Vector3(0, 2.5f, 0);
    private GameObject _statusUIInstance; // UIのインスタンスを保持
    private TextMeshProUGUI _statusUIText;
    
    // このアイテムが現在持たれているかどうか
    public bool isHeld { get; private set; } = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void Start()
    {
        if (statusUIPrefab != null)
        {
            // 変更点: 生成したUIを保持し、最初は非表示
            _statusUIInstance = Instantiate(statusUIPrefab, transform);
            _statusUIInstance.transform.localPosition = statusUIOffset;
            _statusUIText = _statusUIInstance.GetComponentInChildren<TextMeshProUGUI>();
            _statusUIInstance.SetActive(false); // ★最初は非表示
        }
    }

    /// <summary>
    /// プレイヤーに拾われた時の処理
    /// </summary>
    /// <param name="holdPosition">プレイヤーの手の位置</param>
    public void OnPickUp(Transform holdPosition)
    {
        isHeld = true;

        // 物理演算を無効化（暴れないようにする）
        rb.isKinematic = true; 
        
        // 当たり判定を無効化（プレイヤーと衝突しないようにする）
        // ※必要に応じて triggerにするだけでもOKですが、ここでは無効化します
        col.enabled = false;

        // 手の子オブジェクトにして位置を固定
        transform.SetParent(holdPosition);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (_statusUIInstance != null)
        {
            _statusUIInstance.SetActive(false); // UIを非表示にする
        }
    }

    /// <summary>
    /// プレイヤーが離した時の処理
    /// </summary>
    public void OnDrop()
    {
        isHeld = false;

        // 親子関係を解除
        transform.SetParent(null);

        // 物理演算と当たり判定を復活
        rb.isKinematic = false;
        col.enabled = true;
        
        // 少し前に投げる力を加えても良い（お好みで）
        rb.AddForce(transform.forward * 1.1f, ForceMode.Impulse);
    }

    public void OnPlayerEnterRange()
    {
        Debug.Log($"<color=green>[TaskMachine] プレイヤーが {this.gameObject.name} の範囲内に入りました。</color>", this.gameObject);
        if (_statusUIInstance != null)
        {
            _statusUIInstance.SetActive(true); // UIを表示する
        }
    }

    //  OnPlayerExitRangeメソッドを変更 
    public void OnPlayerExitRange()
    {
        Debug.Log($"<color=orange>[TaskMachine] プレイヤーが {this.gameObject.name} の範囲外に出ました。</color>", this.gameObject);
        if (_statusUIInstance != null)
        {
            _statusUIInstance.SetActive(false); // UIを非表示にする
        }
    }

}