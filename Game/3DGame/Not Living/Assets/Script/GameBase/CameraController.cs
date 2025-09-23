using UnityEngine;

public class CameraController : MonoBehaviour
{
    // ▼▼▼ カメラ設定をまとめるための新しいクラスを追加 ▼▼▼
    [System.Serializable]
    public class CameraState
    {
        public string stateName; // Inspectorでの見出し用
        [Range(0f, 1f)] public float lookAtHeightPercent = 0.85f;
        public Vector3 manualOffset = Vector3.zero;
        [Range(1f, 20f)] public float distance = 5f;
        [Range(-30f, 80f)] public float pitch = 20f;
    }

    [Header("カメラ設定")]
    [Tooltip("ゴースト状態の時のカメラ設定")]
    public CameraState ghostCameraState;
    [Tooltip("NPC憑依中のカメラ設定")]
    public CameraState possessedCameraState;
    [Tooltip("カメラ設定が切り替わる際の滑らかさ")]
    [SerializeField, Range(0f, 10f)] private float transitionSpeed = 5f;

    [Header("マウス設定")]
    [SerializeField, Min(0f)] private float mouseSensitivityX = 300f;
    [SerializeField, Min(0f)] private float mouseSensitivityY = 0f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;

    // --- 内部変数 ---
    private PlayerController playerController;
    private float yaw;
    private float pitch;

    // 現在のカメラ設定（滑らかに変化させるため）
    private float currentDistance;
    private float currentHeightPercent;
    private Vector3 currentManualOffset;
    private float currentPitch;

    private void Start()
    {
        // シーン内のPlayerControllerを自動で探す
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("シーンにPlayerControllerが見つかりません！カメラが機能しません。");
            this.enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 初期角度とカメラ設定を適用
        yaw = playerController.transform.eulerAngles.y;
        pitch = ghostCameraState.pitch;
        currentDistance = ghostCameraState.distance;
        currentHeightPercent = ghostCameraState.lookAtHeightPercent;
        currentManualOffset = ghostCameraState.manualOffset;
        currentPitch = ghostCameraState.pitch;
    }

    private void LateUpdate()
    {
        // PlayerControllerが見つからなければ何もしない
        if (playerController == null) return;

        HandleRotation();
        UpdateCameraState();
        UpdatePosition();
    }

    private void HandleRotation()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivityY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateCameraState()
    {
        // プレイヤーがNPCに憑依しているかどうかに基づいて、目標とするカメラ設定を決める
        CameraState targetState = playerController.IsPossessing() ? possessedCameraState : ghostCameraState;

        // Lerpを使って、現在のカメラ設定を目標設定に滑らかに近づける
        float t = Time.deltaTime * transitionSpeed;
        currentDistance = Mathf.Lerp(currentDistance, targetState.distance, t);
        currentHeightPercent = Mathf.Lerp(currentHeightPercent, targetState.lookAtHeightPercent, t);
        currentManualOffset = Vector3.Lerp(currentManualOffset, targetState.manualOffset, t);
        currentPitch = Mathf.Lerp(currentPitch, targetState.pitch, t); // Pitchも滑らかに
    }

    private void UpdatePosition()
    {
        // PlayerControllerから現在の操作対象を取得
        Transform target = playerController.CurrentCharacterTransform;
        if (target == null) return;

        Vector3 targetPos;
        if (target.TryGetComponent<CharacterController>(out var controller))
        {
            float height = controller.height * currentHeightPercent;
            targetPos = target.position + new Vector3(0, height, 0) + currentManualOffset;
        }
        else
        {
            targetPos = target.position + currentManualOffset;
        }

        // マウス操作の角度と、状態に応じたPitchを組み合わせる
        Quaternion rotation = Quaternion.Euler(pitch + currentPitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);

        transform.position = targetPos + offset;
        transform.LookAt(targetPos);
    }
}