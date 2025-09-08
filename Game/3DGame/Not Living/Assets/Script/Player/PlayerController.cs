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
    private StatusManager npcStatusManager;
    private ReikonManager reikonManager;
    private NottoriController nottoriController;
    private AttackInfo punchAttackInfo; // パンチのヒットボックスが持つAttackInfo

    private Vector3 velocity;

    private void Awake()
    {
        ghost = this.gameObject;
        ghostController = GetComponent<CharacterController>();
        ghostAnimator = GetComponent<Animator>();
        reikonManager = GetComponent<ReikonManager>();
        nottoriController = GetComponent<NottoriController>();
        
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
            
            // ヒットボックスを探してAttackInfoを取得
            HitboxController hitboxCtrl = targetNPC.GetComponentInChildren<HitboxController>();
            if (hitboxCtrl != null && hitboxCtrl.attackHitboxes.Length > 0)
            {
                punchAttackInfo = hitboxCtrl.attackHitboxes[0].GetComponent<AttackInfo>();
            }

            if (reikonManager != null) reikonManager.SetPhasingState(false);

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
            if (reikonManager != null) reikonManager.SetPhasingState(true);
            
            currentCharacter = ghost;
            currentController = ghostController;
            currentAnimator = ghostAnimator;

            npcController = null;
            npcAnimator = null;
            npcStatusManager = null;
            punchAttackInfo = null;
        }
    }

    private void Update()
    {
        if (!currentController || !currentController.enabled) return;
        
        if (nottoriController.isPossessing && targetNPC == null)
        {
            nottoriController.ForceRelease();
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (targetNPC != null)
        {
            currentAnimator.SetFloat("Hor", h);
            currentAnimator.SetFloat("Vert", v);
            
            if (Input.GetButtonDown("Fire1"))
            {
                // ダメージ値を設定してから攻撃トリガーを起動
                if (npcStatusManager != null && punchAttackInfo != null)
                {
                    punchAttackInfo.damage = npcStatusManager.power;
                }
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
    
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.TryGetComponent<ReikonItem>(out ReikonItem item))
        {
            HealAndDestroyItem(item);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<ReikonItem>(out ReikonItem item))
        {
            HealAndDestroyItem(item);
        }
    }
    
    private void HealAndDestroyItem(ReikonItem item)
    {
        if (reikonManager != null)
        {
            reikonManager.Heal(item.recoveryAmount);
        }
        Destroy(item.gameObject);
    }
    
    public bool IsCollisionsEnabled()
    {
        if (currentController != null)
        {
            return currentController.detectCollisions;
        }
        return false;
    }
    
    public bool IsPossessing()
    {
        return targetNPC != null;
    }
}