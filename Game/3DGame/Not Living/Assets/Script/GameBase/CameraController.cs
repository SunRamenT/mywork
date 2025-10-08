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
    [Tooltip("警察官に憑依している時の特別なカメラ設定")] // ▼▼▼ 追加 ▼▼▼
    public CameraState policeCameraState;
    [Tooltip("カメラが目標に追従する際の滑らかさ。小さいほどゆっくり。")]
    [SerializeField, Range(0.1f, 20f)] private float positionTransitionSpeed = 5f;
    [Tooltip("カメラが目標の向きに追従する際の滑らかさ。")]
    [SerializeField, Range(0.1f, 20f)] private float rotationTransitionSpeed = 10f;


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
        pitch = 0f; // マウスによる角度は0から始める
        
        if (SettingsManager.Instance != null)
        {
            mouseSensitivityX *= SettingsManager.Instance.MouseSensitivityX;
        }
    }

    private void LateUpdate()
    {
        float playerTimeScale = PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f;
        float playerDeltaTime = Time.deltaTime * playerTimeScale;

        if (playerController == null) return;
        Transform target = playerController.CurrentCharacterTransform;
        if (target == null) return;

        // ▼▼▼ このブロックを追加 ▼▼▼
        // ゲームの状態が「通常プレイ」でない場合（ミニゲーム中やポーズ中など）は、
        // カメラの回転処理をスキップする
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.Gameplay)
        {
            return; // ここで処理を中断
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        HandleRotation();

        // 1. 憑依状態に応じて、目標となるカメラ設定を決定する
        CameraState targetState;
        if (playerController.IsPossessing())
        {
            // もし乗っ取っているキャラクターのTagが "Police" なら
            if (playerController.PossessedCharacterTag == "Police")
            {
                targetState = policeCameraState; // 警察用の設定を使用
            }
            else
            {
                targetState = possessedCameraState; // 通常の憑依設定を使用
            }
        }
        else
        {
            targetState = ghostCameraState; // ゴースト用の設定を使用
        }

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

        // 3. 現在のカメラの位置・向きから、「目標」に向かって滑らかに移動させる
        float posT = playerDeltaTime * positionTransitionSpeed;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, posT);
        float rotT = playerDeltaTime * rotationTransitionSpeed;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
    }

    private void HandleRotation()
    {
        float playerTimeScale = PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f;
        float playerDeltaTime = Time.deltaTime * playerTimeScale;
        yaw += Input.GetAxis("Mouse X") * mouseSensitivityX * playerDeltaTime;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivityY, minPitch, maxPitch);
    }
}