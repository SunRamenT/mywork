// SparkMover.cs (新規作成)
using UnityEngine;

public class SparkMover : MonoBehaviour
{
    private float _speed = 600f;
    private Vector2 _direction;
    private RectTransform _rect;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 生成元（RunnerMiniGame）から呼び出され、目標地点と速度を設定する
    /// </summary>
    public void Initialize(Vector2 targetPosition, float speed)
    {
        _speed = speed;
        // 自分自身の位置から目標地点への方向ベクトルを計算する
        _direction = ((Vector3)targetPosition - _rect.position).normalized;
    }

    void Update()
    {
        // 計算された方向へ、毎フレーム移動する
        _rect.position += (Vector3)_direction * _speed * Time.deltaTime;

        // 画面の範囲外に出たら、自分自身を破棄する
        if (!IsVisibleOnScreen())
        {
            Destroy(gameObject);
        }
    }

    // 簡単な画面内外判定
    private bool IsVisibleOnScreen()
    {
        return _rect.position.x > -10 && _rect.position.x < Screen.width + 10 &&
               _rect.position.y > -10 && _rect.position.y < Screen.height + 10;
    }
}