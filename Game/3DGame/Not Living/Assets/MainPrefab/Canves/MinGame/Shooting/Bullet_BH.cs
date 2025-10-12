// Bullet.cs (新規作成)
using UnityEngine;

public class Bullet_BH : MonoBehaviour
{
    [Tooltip("弾の当たり判定の半径")] // ▼▼▼ 追加 ▼▼▼
    public float hitboxRadius = 8f;
    private Vector3 _direction;
    private float _speed;
    private RectTransform _rect;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    public void Initialize(Vector3 direction, float speed)
    {
        _direction = direction.normalized;
        _speed = speed;
    }

    void Update()
    {
        // 指定された方向に移動
        _rect.anchoredPosition += (Vector2)(_direction * _speed * Time.deltaTime);

        // 画面外に出たら自動で消滅
        if (!IsVisible())
        {
            Destroy(gameObject);
        }
    }

    // 簡易的な画面内判定
    private bool IsVisible()
    {
        Vector2 pos = _rect.anchoredPosition;
        // 画面の範囲を少し広めに見積もる
        return pos.x > -600 && pos.x < 600 && pos.y > -500 && pos.y < 500;
    }
}