using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class TrashItem : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;
    
    // このアイテムが現在持たれているかどうか
    public bool isHeld { get; private set; } = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
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
}