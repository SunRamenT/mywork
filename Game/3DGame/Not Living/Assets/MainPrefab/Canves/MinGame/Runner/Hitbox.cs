// Hitbox.cs (新規作成)
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    [Tooltip("当たり判定の中心位置のオフセット")]
    public Vector2 centerOffset = Vector2.zero;
    
    [Tooltip("当たり判定のサイズ")]
    public Vector2 size = new Vector2(50f, 50f);

    private RectTransform _rectTransform;

    void Awake()
    {
        // 自身のRectTransformをキャッシュしておく
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// ワールド座標での当たり判定用のRectを計算して返す
    /// </summary>
    public Rect GetWorldRect()
    {
        // RectTransformのワールド座標での中心位置を計算
        Vector2 worldCenter = (Vector2)_rectTransform.position + centerOffset;
        
        // Rectは左下の座標で生成するため、中心位置から計算する
        Vector2 worldBottomLeft = worldCenter - (size / 2f);

        return new Rect(worldBottomLeft, size);
    }

    // --- UnityエディタのSceneビューで当たり判定を視覚化するためのコード ---
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        
        Gizmos.color = Color.green;
        Rect worldRect = GetWorldRect();
        Gizmos.DrawWireCube(worldRect.center, worldRect.size);
    }
    #endif
}