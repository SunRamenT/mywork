// PlayerController_Runner.cs (修正版)
using UnityEngine;

public class PlayerController_Runner : MonoBehaviour
{
    [Header("ジャンプ設定")]
    public float jumpForce = 800f;
    public float gravity = 2500f;
    [Tooltip("ジャンプ可能な回数（1で通常ジャンプ、2で二段ジャンプ）")] // ▼▼▼ 追加 ▼▼▼
    public int maxJumps = 2;

    [Header("左右移動設定")] // ▼▼▼ 追加 ▼▼▼
    public float moveSpeed = 400f;
    [Tooltip("移動可能な最小のX座標")]
    public float minX = -450f;
    [Tooltip("移動可能な最大のX座標")]
    public float maxX = 450f;

    private RectTransform _rect;
    private float _verticalVelocity = 0f;
    private bool _isGrounded = false;
    private const float PlayerHalfHeight = 25f;

    // 現在のジャンプ回数をカウントする変数
    private int _jumpCount = 0;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _rect.anchoredPosition = new Vector2(-300f, 100f);
    }

    private void Update()
    {
        CheckGroundedStatus();// 接地判定
        HandleJumpInput();// ジャンプ
        HandleHorizontalInput();// 左右移動
        ApplyMovement();// 移動適用
    }

    void CheckGroundedStatus()
    {
        bool foundGround = false;
        if (_verticalVelocity <= 0)
        {
            float playerBottom = _rect.anchoredPosition.y - PlayerHalfHeight;
            float playerLeft = _rect.anchoredPosition.x - 25f;
            float playerRight = _rect.anchoredPosition.x + 25f;

            foreach (var ground in RunnerMiniGame.GroundRects)
            {
                float groundTop = ground.anchoredPosition.y + ground.rect.height / 2f;// 地面の上端Y座標
                float groundLeft = ground.anchoredPosition.x - ground.rect.width / 2f;// 地面の左端X座標
                float groundRight = ground.anchoredPosition.x + ground.rect.width / 2f;// 地面の右端X座標

                if (playerRight > groundLeft && playerLeft < groundRight &&
                    Mathf.Abs(playerBottom - groundTop) < 10f)
                {
                    _isGrounded = true;
                    foundGround = true;
                    _verticalVelocity = 0;
                    _jumpCount = 0; // 接地したらジャンプ回数をリセット
                    _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, groundTop + PlayerHalfHeight);
                    break;
                }
            }
        }

        if (!foundGround)
        {
            _isGrounded = false;
        }
    }

    //左右移動の処理
    private void HandleHorizontalInput()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/Dキーまたは←/→キー
        float newX = _rect.anchoredPosition.x + horizontal * moveSpeed * Time.deltaTime;
        
        // 移動範囲をminXとmaxXの間に制限する
        newX = Mathf.Clamp(newX, minX, maxX);

        // X座標のみを更新
        _rect.anchoredPosition = new Vector2(newX, _rect.anchoredPosition.y);
    }

    private void HandleJumpInput()
    {
        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space))
        {
            // ジャンプ回数が上限に達していなければジャンプできる
            if (_jumpCount < maxJumps)
            {
                _verticalVelocity = jumpForce;
                _isGrounded = false; // 空中でジャンプするので接地状態をfalseに
                _jumpCount++; // ジャンプ回数を1増やす
            }
        }
    }
    
    private void ApplyMovement()
    {
        if (!_isGrounded)
        {
            _verticalVelocity -= gravity * Time.deltaTime;
        }
        _rect.anchoredPosition += new Vector2(0, _verticalVelocity * Time.deltaTime);
    }
}