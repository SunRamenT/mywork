using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Ghost状態の時の移動速度")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpPower = 5f;

    [Header("Animator Settings")]
    public string attackTriggerName = "Attack";
    public string jumpTriggerName = "Jump";

    // --- 現在操作している対象の情報を保持する変数 ---
    private CharacterController currentController;
    private Animator currentAnimator;
    private GameObject currentCharacter;

    // --- GhostとNPCの情報を個別に保持 ---
    private CharacterController ghostController;
    private Animator ghostAnimator;
    private GameObject ghost;
    
    private CharacterController npcController;
    private Animator npcAnimator;
    private GameObject targetNPC; 
    private StatusManager npcStatusManager; // ▼▼▼ NPCのステータスを保持する変数を追加 ▼▼▼

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
    
    // NottoriControllerから呼ばれる
    public void SetTargetNPC(GameObject npc, Animator anim)
    {
        targetNPC = npc;

        if (targetNPC != null)
        {
            // --- 乗っ取り時: 操作対象をNPCに切り替える ---
            npcController = targetNPC.GetComponent<CharacterController>();
            npcAnimator = anim;
            npcStatusManager = targetNPC.GetComponent<StatusManager>(); // ▼▼▼ NPCのStatusManagerを取得 ▼▼▼

            if (npcController == null)
            {
                Debug.LogError("乗っ取り対象のNPCにCharacterControllerがアタッチされていません！");
                return;
            }
            if (npcStatusManager == null)
            {
                Debug.LogWarning("乗っ取り対象のNPCにStatusManagerがアタッチされていません。");
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
            // --- 乗っ取り解除時: 操作対象をGhostに戻す ---
            if (npcController != null)
            {
                npcController.enabled = false;
            }
            
            ghostController.enabled = true;
            ghostController.detectCollisions = false; 
            
            currentCharacter = ghost;
            currentController = ghostController;
            currentAnimator = ghostAnimator;

            // NPCの参照をクリア
            npcController = null;
            npcAnimator = null;
            npcStatusManager = null; // ▼▼▼ StatusManagerの参照もクリア ▼▼▼
        }
    }

    private void Update()
    {
        if (!currentController || !currentController.enabled) return;
        
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (targetNPC != null)
        {
            currentAnimator.SetFloat("Hor", h);
            currentAnimator.SetFloat("Vert", v);
            
            if (Input.GetButtonDown("Fire1"))
            {
                currentAnimator.SetTrigger(attackTriggerName);
            }

            if (Input.GetButtonDown("Jump") && currentController.isGrounded)
            {
                currentAnimator.SetTrigger(jumpTriggerName);
            }
        }

        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            currentCharacter.transform.rotation = Quaternion.Slerp(currentCharacter.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // ▼▼▼ 現在の状況に応じた移動速度を決定 ▼▼▼
        float currentSpeed;
        if (targetNPC != null && npcStatusManager != null)
        {
            currentSpeed = npcStatusManager.speed; // 乗っ取り中はNPCの速度を使用
        }
        else
        {
            currentSpeed = this.moveSpeed; // 通常時はGhostの速度を使用
        }

        Vector3 move = currentCharacter.transform.forward * v + currentCharacter.transform.right * h;
        move = move.normalized * currentSpeed; // 決定した速度で移動

        if (currentController.detectCollisions)
        {
            if (currentController.isGrounded)
            {
                velocity.y = -0.1f;
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