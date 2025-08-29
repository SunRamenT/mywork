using UnityEngine;

// TPSカメラをマウス入力で操作
public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;           // 注視するキャラクター
    [SerializeField, Min(0f)] private float height = 2f; // カメラ高さ
    [SerializeField, Min(0f)] private float distance = 5f; // カメラ距離

    [Header("Mouse Settings")]
    [SerializeField, Min(0f)] private float mouseSensitivityX = 300f; // 水平回転感度
    [SerializeField, Min(0f)] private float mouseSensitivityY = 2f;   // 垂直回転感度
    [SerializeField] private float minPitch = -20f; // 上下回転下限
    [SerializeField] private float maxPitch = 60f;  // 上下回転上限

    private float yaw;   // 水平角度
    private float pitch; // 垂直角度

    private void Start()
    {
        // マウスを中央に固定して非表示
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleRotation();     // マウス入力で回転
        UpdatePosition();     // カメラ位置更新
    }

    // マウス入力による回転更新
    private void HandleRotation()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    // カメラ位置計算と注視
    private void UpdatePosition()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        Vector3 targetPos = target.position + Vector3.up * height;

        transform.position = targetPos + offset;
        transform.LookAt(targetPos);
    }
}