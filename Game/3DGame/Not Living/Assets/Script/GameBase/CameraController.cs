using UnityEngine;

public class CameraController : MonoBehaviour
{
    [System.Serializable]
    public class CameraState
    {
        public string stateName;
        [Range(0f, 1f)] public float lookAtHeightPercent = 0.85f;
        public Vector3 manualOffset = Vector3.zero;
        [Range(1f, 20f)] public float distance = 5f;
        [Range(-30f, 80f)] public float pitch = 20f;
    }

    [Header("カメラ設定")]
    public CameraState ghostCameraState;
    public CameraState possessedCameraState;
    [Tooltip("カメラが目標に追従する際の滑らかさ。小さいほどゆっくり。")]
    [SerializeField, Range(0.1f, 20f)] private float transitionSpeed = 5f;

    [Header("マウス設定")]
    [SerializeField, Min(0f)] private float mouseSensitivityX = 300f;
    [SerializeField, Min(0f)] private float mouseSensitivityY = 0f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;

    // --- 内部変数 ---
    private PlayerController playerController;
    private float yaw;
    private float pitch;

    private void Start()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("シーンにPlayerControllerが見つかりません！カメラが機能しません。");
            this.enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        yaw = playerController.transform.eulerAngles.y;
        pitch = ghostCameraState.pitch;
    }

    // カメラの更新はキャラクターの移動が完了した後に行うのが望ましいため、LateUpdateを使用
    private void LateUpdate()
    {
        if (playerController == null) return;

        // 現在操作しているキャラクターのTransformを取得
        Transform target = playerController.CurrentCharacterTransform;
        if (target == null) return;
        
        // マウス入力で目標となる角度を更新
        HandleRotation();

        // --- ここからが新しいロジック ---

        // 1. 憑依状態に応じて、目標となるカメラ設定を決定する
        CameraState targetState = playerController.IsPossessing() ? possessedCameraState : ghostCameraState;

        // 2. 目標設定に基づいて、「カメラが最終的に到達したい座標と角度」を計算する
        Vector3 targetLookAtPos;
        if (target.TryGetComponent<CharacterController>(out var controller))
        {
            float height = controller.height * targetState.lookAtHeightPercent;
            targetLookAtPos = target.position + new Vector3(0, height, 0) + targetState.manualOffset;
        }
        else
        {
            targetLookAtPos = target.position + targetState.manualOffset;
        }

        Quaternion desiredRotation = Quaternion.Euler(pitch + targetState.pitch, yaw, 0f);
        Vector3 desiredOffset = desiredRotation * new Vector3(0f, 0f, -targetState.distance);
        Vector3 desiredPosition = targetLookAtPos + desiredOffset;

        // 3. 現在のカメラの位置から、「目標座標」に向かって滑らかに移動させる
        float t = Time.deltaTime * transitionSpeed;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
    }

    private void HandleRotation()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
        // pitchの更新は、マウス操作による相対的な変化のみにする
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivityY, minPitch, maxPitch);
    }
}