using UnityEngine;
using System.Linq; // OrderByを使うために必要

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpPower = 5f;

    [Header("Item Detection Settings")] // ▼▼▼ 追加 ▼▼▼
    [Tooltip("回復アイテム（霊魂）が含まれるレイヤー")]
    public LayerMask reikonLayer; 
    [Tooltip("アイテムを検知する半径")]
    public float itemDetectionRadius = 1.5f;

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
    private AttackInfo punchAttackInfo;

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
            
            HitboxController hitboxCtrl = targetNPC.GetComponentInChildren<HitboxController>();
            if (hitboxCtrl != null && hitboxCtrl.attackHitboxes.Length > 0)
            {
                punchAttackInfo = hitboxCtrl.attackHitboxes[0].GetComponent<AttackInfo>();
            }

            if (reikonManager != null) reikonManager.UpdateState(false, true);

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
            if (reikonManager != null) reikonManager.UpdateState(true, false);
            
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
        // ▼▼▼ アイテム検知処理をUpdateの最初に追加 ▼▼▼
        CheckForRecoveryItems();
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        if (nottoriController.isPossessing && targetNPC == null)
        {
            if (reikonManager != null && nottoriController != null)
            {
                reikonManager.TakeDamage(nottoriController.deathPenaltyAmount);
            }
            nottoriController.ForceRelease();
            return;
        }

        if (!currentController || !currentController.enabled) return;
        
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (targetNPC != null)
        {
            currentAnimator.SetFloat("Hor", h);
            currentAnimator.SetFloat("Vert", v);
            
            if (Input.GetButtonDown("Fire1"))
            {
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

        float currentSpeed = (targetNPC != null && npcStatusManager != null) ? npcStatusManager.speed : this.moveSpeed;
        Vector3 move = (currentCharacter.transform.forward * v + currentCharacter.transform.right * h).normalized * currentSpeed;

        if (currentController.detectCollisions)
        {
            if (currentController.isGrounded)
            {
                velocity.y = -0.1f;
                if (Input.GetButtonDown("Jump"))
                {
                    float currentJumpPower = (targetNPC != null && npcStatusManager != null) ? npcStatusManager.jumpPower : this.jumpPower;
                    velocity.y = currentJumpPower;
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
    
    // ▼▼▼ 新しく追加したメソッド ▼▼▼
    /// <summary>
    /// 物理判定に頼らず、キャラクターの周囲にある回復アイテムを検知して取得する
    /// </summary>
    private void CheckForRecoveryItems()
    {
        // キャラクターの位置を中心に、指定した半径内のコライダーを全て取得する
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, itemDetectionRadius, reikonLayer);

        if (hitColliders.Length > 0)
        {
            // 複数のアイテムを同時に検知した場合、一番近いものを取得する
            Collider closest = hitColliders.OrderBy(c => (transform.position - c.transform.position).sqrMagnitude).FirstOrDefault();

            if (closest != null && closest.TryGetComponent<ReikonItem>(out ReikonItem item))
            {
                HealAndDestroyItem(item);
            }
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
    
    // ▼▼▼ 既存のOnTriggerEnterは不要になるため、コメントアウトまたは削除してもOK ▼▼▼
    /*
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
    */
    
    public bool IsCollisionsEnabled()
    {
        return currentController != null ? currentController.detectCollisions : false;
    }
    
    public bool IsPossessing()
    {
        return targetNPC != null;
    }
}