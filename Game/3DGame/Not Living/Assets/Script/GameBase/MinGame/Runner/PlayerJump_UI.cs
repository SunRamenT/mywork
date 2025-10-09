using UnityEngine;

public class PlayerJump_UI : MonoBehaviour
{
    public float jumpForce = 650f;
    public float gravity = 2000f;
    public float maxJumpTime = 0.25f;
    public float groundY = -150f; // RunnerMiniGamePanelの下から少し上

    private RectTransform rect;
    private float verticalVelocity = 0f;
    private bool isGrounded = true;
    private float jumpTimeCounter = 0f;
    private bool isJumping = false;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        HandleJumpInput();
        ApplyGravity();
        MovePlayer();
    }

    private void HandleJumpInput()
    {
        if (isGrounded && Input.GetButtonDown("Fire1"))
        {
            isJumping = true;
            jumpTimeCounter = maxJumpTime;
            verticalVelocity = jumpForce;
            isGrounded = false;
        }

        if (Input.GetButton("Fire1") && isJumping)
        {
            if (jumpTimeCounter > 0)
            {
                verticalVelocity = jumpForce;
                jumpTimeCounter -= Time.deltaTime;
            }
        }

        if (Input.GetButtonUp("Fire1"))
        {
            isJumping = false;
        }
    }

    private void ApplyGravity()
    {
        if (!isGrounded)
            verticalVelocity -= gravity * Time.deltaTime;
    }

    private void MovePlayer()
    {
        rect.anchoredPosition += new Vector2(0, verticalVelocity * Time.deltaTime);

        if (rect.anchoredPosition.y <= groundY)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, groundY);
            verticalVelocity = 0;
            isGrounded = true;
            isJumping = false;
        }
    }
}
