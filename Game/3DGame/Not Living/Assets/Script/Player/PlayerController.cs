using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // 移動・回転設定
    [Header("Movement Settings")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;        // 移動速度
    [SerializeField, Min(0f)] private float jumpPower = 5f;        // ジャンプ力
    [SerializeField, Min(0f)] private float rotationSpeed = 10f;   // 回転速度

    [Header("Ground Settings")]
    [SerializeField] private LayerMask groundLayer;               // 地面判定用レイヤー

    private CharacterController characterController;             // キャラクターコントローラ
    private Vector3 velocity;                                     // 移動速度

    // キャラクターの前方向を外部から参照可能
    public Vector3 Forward => transform.forward;

    private NottoriController                                                                     NotList;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleRotation();   // カメラ方向にキャラクター回転
        HandleMovement();   // 移動
        HandleJump();       // ジャンプ
    }

    // キャラクターをカメラ方向に回転
    private void HandleRotation()
    {
        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0; // 水平方向のみ
        if (lookDir.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // 前後左右の移動
    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/D
        float vertical = Input.GetAxis("Vertical");     // W/S

        Vector3 moveInput = transform.forward * vertical + transform.right * horizontal;
        moveInput = moveInput.normalized;

        velocity.x = moveInput.x * moveSpeed;
        velocity.z = moveInput.z * moveSpeed;

        if (!characterController.isGrounded)
            velocity.y += Physics.gravity.y * Time.deltaTime;

        characterController.Move(velocity * Time.deltaTime);
    }

    // ジャンプ処理
    private void HandleJump()
    {
        if (characterController.isGrounded && Input.GetButtonDown("Jump"))
            velocity.y = jumpPower;
        else if (characterController.isGrounded)
            velocity.y = 0;
    }
}