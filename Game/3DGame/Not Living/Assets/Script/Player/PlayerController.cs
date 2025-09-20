using UnityEngine;
using System.Linq;

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
    
    // ▼▼▼ 壁抜け判定用の設定を追加 ▼▼▼
    [Header("壁抜け判定")]
    [Tooltip("壁として判定するレイヤー")]
    public LayerMask wallLayer;
    [Tooltip("建物として判定するタグ")]
    public string buildingTag = "Building";

    // 現在、壁を抜けているかどうかの状態
    public bool IsPhasingThroughWall { get; private set; } = false;
    private float checkRadius;

    // --- (既存の他の変数) ---
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
    private ReikonManager reikonManager;
    private NottoriController nottoriController;
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

        // 判定用の球の半径をCharacterControllerの半径に合わせる
        checkRadius = ghostController.radius;
    }
    
    // ... (SetTargetNPCは変更なし) ...
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
            if (npcStatusManager == null)
            {
                Debug.LogWarning("乗っ取り対象のNPCにStatusManagerがアタッチされていません。");
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
        }
    }


    private void Update()
    {
        if (nottoriController.isPossessing && targetNPC == null)
        {
            nottoriController.ForceRelease();
            return;
        }

        if (!currentController || !currentController.enabled) return;
        
        // --- 壁抜け判定 ---
        // Ghost状態（当たり判定が無効）の時だけチェックする
        if (!currentController.detectCollisions)
        {
            CheckWallPhasing();
        }
        else
        {
            IsPhasingThroughWall = false;
        }

        // ... (以降の移動やアニメーションの処理は変更なし) ...
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

    /// <summary>
    /// Ghostが建物の中を通り抜けているかチェックする
    /// </summary>
    private void CheckWallPhasing()
    {
        IsPhasingThroughWall = false;

        Collider[] overlappingColliders = Physics.OverlapSphere(transform.position, checkRadius, wallLayer);

        foreach (var col in overlappingColliders)
        {
            if (col.CompareTag(buildingTag))
            {
                IsPhasingThroughWall = true;
                Debug.Log("aa");
                break;
            }
        }
    }
    
    // ... (以降のメソッドは変更なし) ...
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