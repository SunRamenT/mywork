using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpPower = 5f;

    [Header("Animator Settings")]
    public string attackTriggerName = "Attack";
    public string jumpTriggerName = "Jump"; // ▼▼▼ BoolからTriggerの名前に変更 ▼▼▼

    // ... (他の変数は変更なし) ...
    private CharacterController currentController;
    private Animator currentAnimator;
    private GameObject currentCharacter;
    private CharacterController ghostController;
    private Animator ghostAnimator;
    private GameObject ghost;
    private CharacterController npcController;
    private Animator npcAnimator;
    private GameObject targetNPC; 
    private StatusManager npcStatusManager;
    private Vector3 velocity;

    private void Awake()
    {
        ghost = this.gameObject;
        ghostController = GetComponent<CharacterController>();
        ghostAnimator = GetComponent<Animator>();
        
        currentCharacter = ghost;
        currentController = ghostController;
        currentAnimator = ghostAnimator;
        currentController.detectCollisions = false;
    }
    
    public void SetTargetNPC(GameObject npc, Animator anim)
    {
        targetNPC = npc;

        if (targetNPC != null)
        {
            npcController = targetNPC.GetComponent<CharacterController>();
            npcAnimator = anim;
            npcStatusManager = targetNPC.GetComponent<StatusManager>();

            if (npcController == null)
            {
                Debug.LogError("乗っ取り対象のNPCにCharacterControllerがアタッチされていません！");
                return;
            }

            ghostController.enabled = false;
            npcController.enabled = true;
            npcController.detectCollisions = true; 
            
            currentCharacter = targetNPC;
            currentController = npcController;
            currentAnimator = npcAnimator;
        }
        else
        {
            if (npcController != null)
            {
                npcController.enabled = false;
            }
            
            ghostController.enabled = true;
            ghostController.detectCollisions = false; 
            
            currentCharacter = ghost;
            currentController = ghostController;
            currentAnimator = ghostAnimator;

            npcController = null;
            npcAnimator = null;
            npcStatusManager = null;
        }
    }

    private void Update()
    {
        if (!currentController || !currentController.enabled) return;
        
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 乗っ取り中（NPC操作中）の場合のみ、アニメーション命令を送る
        if (targetNPC != null)
        {
            currentAnimator.SetFloat("Hor", h);
            currentAnimator.SetFloat("Vert", v);
            
            if (Input.GetButtonDown("Fire1"))
            {
                currentAnimator.SetTrigger(attackTriggerName);
            }

            // ▼▼▼ ジャンプのロジックを修正 ▼▼▼
            // ジャンプボタンが押された時だけ、Jumpトリガーを起動する
            if (Input.GetButtonDown("Jump") && currentController.isGrounded)
            {
                currentAnimator.SetTrigger(jumpTriggerName);
            }
        }

        // --- 回転の処理 ---
        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            currentCharacter.transform.rotation = Quaternion.Slerp(currentCharacter.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // --- 移動の処理 ---
        float currentSpeed;
        if (targetNPC != null && npcStatusManager != null)
        {
            currentSpeed = npcStatusManager.speed;
        }
        else
        {
            currentSpeed = this.moveSpeed;
        }

        Vector3 move = currentCharacter.transform.forward * v + currentCharacter.transform.right * h;
        move = move.normalized * currentSpeed;

        // --- 重力と最終的な移動 ---
        if (currentController.detectCollisions)
        {
            if (currentController.isGrounded)
            {
                velocity.y = -0.1f;
                // ジャンプの物理的な処理
                if (Input.GetButtonDown("Jump"))
                {
                    velocity.y = jumpPower;
                }
            }
            else
            {
                velocity.y += Physics.gravity.y * Time.deltaTime;
            }
        }
        else
        {
             velocity.y = 0;
        }

        Vector3 finalMove = move + new Vector3(0, velocity.y, 0);
        currentController.Move(finalMove * Time.deltaTime);
        
        if (targetNPC != null)
        {
            ghost.transform.position = targetNPC.transform.position;
            ghost.transform.rotation = targetNPC.transform.rotation;
        }
    }
}