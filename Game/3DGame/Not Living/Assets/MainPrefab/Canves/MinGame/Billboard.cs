// Billboard.cs (新規作成)
using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform _cameraTransform;

    void Start()
    {
        // メインカメラのTransformをキャッシュする
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
    }

    // LateUpdateは、カメラの移動を含む全てのUpdate処理が終わった後に呼ばれる
    void LateUpdate()
    {
        if (_cameraTransform == null) return;

        // UIがカメラの方向を向くようにする
        // カメラの位置 + カメラの前方ベクトル で、カメラの少し前の位置を見るようにする
        transform.LookAt(transform.position + _cameraTransform.rotation * Vector3.forward,
                         _cameraTransform.rotation * Vector3.up);
    }
}