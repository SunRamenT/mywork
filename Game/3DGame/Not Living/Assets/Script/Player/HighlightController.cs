using UnityEngine;

public class HighlightController : MonoBehaviour
{
    [Header("ハイライト設定")]
    [Tooltip("表示するハイライトエフェクトのプレハブ")]
    public GameObject highlightEffectPrefab;
    [Tooltip("ハイライトする対象が含まれるレイヤー")]
    public LayerMask highlightLayer;
    [Tooltip("マウスカーソルからの光線の最大距離")]
    public float maxDistance = 100f;

    // --- 内部変数 ---
    private GameObject currentHighlightEffect; // 現在表示しているエフェクトのインスタンス
    private Transform currentlyHighlighted;    // 現在ハイライトしている対象のTransform

    private void Update()
    {
        // カメラからマウスカーソルの位置へレイを飛ばす
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // レイがオブジェクトに当たったかどうかをチェック
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, highlightLayer))
        {
            // 当たったオブジェクトが自分自身でなければ
            if (hit.transform.root != this.transform.root)
            {
                // まだハイライトしていない新しいオブジェクトなら
                if (currentlyHighlighted != hit.transform)
                {
                    // 既存のエフェクトを消し、新しい対象をハイライト
                    ClearHighlight();
                    SetHighlight(hit.transform);
                }
                // (既にハイライト中の同じオブジェクトなら何もしない)
            }
            // 当たったのが自分自身なら
            else
            {
                ClearHighlight();
            }
        }
        // レイが何にも当たらなかったら
        else
        {
            ClearHighlight();
        }
    }

    /// <summary>
    /// 指定された対象にハイライトエフェクトを表示する
    /// </summary>
    private void SetHighlight(Transform target)
    {
        if (highlightEffectPrefab == null) return;

        currentlyHighlighted = target;
        // エフェクトを生成し、対象オブジェクトの子にする
        currentHighlightEffect = Instantiate(highlightEffectPrefab, target.position, Quaternion.identity, target);
    }

    /// <summary>
    /// 現在のハイライトエフェクトを消去する
    /// </summary>
    private void ClearHighlight()
    {
        if (currentlyHighlighted != null)
        {
            // エフェクトを破棄
            if (currentHighlightEffect != null)
            {
                Destroy(currentHighlightEffect);
            }
            // 記録をリセット
            currentlyHighlighted = null;
            currentHighlightEffect = null;
        }
    }
}