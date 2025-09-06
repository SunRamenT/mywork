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
    public string jumpBoolName = "isJump";

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
    private StatusManager npcStatusManager; // NPCのステータスを保持

    private Vector3 velocity;

    private void Awake()
    {
        ghost = this.gameObject;
        ghostController = GetComponent<CharacterController>();
        ghostAnimator = GetComponent<Animator>();
        
        // --- 初期状態をGhostに設定 ---
        currentCharacter = ghost;
        currentController = ghostController;
        currentAnimator = ghostAnimator;
        currentController.detectCollisions = false; // 壁抜けON
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
            npcStatusManager = targetNPC.GetComponent<StatusManager>(); // NPCのStatusManagerを取得

            if (npcController == null)
            {
                Debug.LogError("乗っ取り対象のNPCにCharacterControllerがアタッチされていません！");
                return;
            }
            if (npcStatusManager == null)
            {
                Debug.LogWarning("乗っ取り対象のNPCにStatusManagerがアタッチされていません。");
            }

            // Ghostの物理的な動きを止め、NPCを有効化
            ghostController.enabled = false;
            npcController.enabled = true;
            npcController.detectCollisions = true; // NPCは壁抜けしない
            
            // 現在の操作対象をNPCの情報で上書き
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
            
            // Ghostの物理的な動きを再開し、壁抜け状態に戻す
            ghostController.enabled = true;
            ghostController.detectCollisions = false; 
            
            // 現在の操作対象をGhostの情報で上書き
            currentCharacter = ghost;
            currentController = ghostController;
            currentAnimator = ghostAnimator;

            // NPCの参照をクリア
            npcController = null;
            npcAnimator = null;
            npcStatusManager = null; // StatusManagerの参照もクリア
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

            currentAnimator.SetBool(jumpBoolName, !currentController.isGrounded);
        }

        // --- 回転の処理 ---
        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            currentCharacter.transform.rotation = Quaternion.Slerp(currentCharacter.transform.rotation,
                Quaternion.LookRotation(lookDir), rotationSpeed * Time.deltaTime);

        // --- 移動の処理 ---
        // ▼▼▼ ここからが修正・統合された部分 ▼▼▼
        float currentSpeed;
        if (targetNPC != null && npcStatusManager != null)
        {
            // 乗っ取り中はNPCのStatusManagerから速度を取得
            currentSpeed = npcStatusManager.speed; 
        }
        else
        {
            // 通常時はGhost自身の速度を使用
            currentSpeed = this.moveSpeed; 
        }

        Vector3 move = currentCharacter.transform.forward * v + currentCharacter.transform.right * h;
        move = move.normalized * currentSpeed; // 状況に応じた速度を適用
        // ▲▲▲ ここまで ▲▲▲

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
             velocity.y = 0; // 壁抜け中は重力を無視
        }

        Vector3 finalMove = move + new Vector3(0, velocity.y, 0);
        currentController.Move(finalMove * Time.deltaTime);
        
        // 乗っ取り中は、GhostのTransformをNPCのTransformに同期させる
        if (targetNPC != null)
        {
            ghost.transform.position = targetNPC.transform.position;
            ghost.transform.rotation = targetNPC.transform.rotation;
        }
    }
}