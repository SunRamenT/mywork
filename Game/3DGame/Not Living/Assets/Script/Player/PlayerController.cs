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

    // ▼▼▼ 看板などを検知するための設定を追加 ▼▼▼
    [Header("Interaction Settings")]
    [Tooltip("看板など、インタラクション可能なオブジェクトのレイヤー")]
    public LayerMask interactableLayer;
    [Tooltip("インタラクション可能なオブジェクトを検知する半径")]
    public float interactionRadius = 3f;

    // ▼▼▼ 壁検知用の設定を追加 ▼▼▼
    [Header("Wall Phasing Settings")]
    [Tooltip("壁として認識するオブジェクトのレイヤー")]
    public LayerMask wallLayer;

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
    
    //  現在範囲内にいる看板を記憶しておくための変数を追加 
    private InteractiveSignboard currentSignboard;

    // 変数の型を、具体的なクラスではなくインターフェースにする
    private ISpecialAction currentSpecialAction;

    /// 現在の特殊能力を外部から読み取るためのプロパティ
    public ISpecialAction CurrentSpecialAction => currentSpecialAction;

    /// 現在操作しているキャラクターのTransformを外部（CameraControllerなど）に公開する
    /// </summary>
    public Transform CurrentCharacterTransform => currentCharacter != null ? currentCharacter.transform : this.transform;


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
            // GuardActionやSuperJumpActionなど、ISpecialActionを持つコンポーネントを探す
            currentSpecialAction = targetNPC.GetComponent<ISpecialAction>();

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
            //npcController.enabled = true;
            npcController.detectCollisions = true; 
            
            currentCharacter = targetNPC;
            currentController = npcController;
            currentAnimator = npcAnimator;
        }
        else
        {
            //if (npcController != null)
            //{
            //    npcController.enabled = false;
            //}
            
            ghostController.enabled = true;
            ghostController.detectCollisions = false; 
            if (reikonManager != null) reikonManager.UpdateState(true, false);
            
            currentCharacter = ghost;
            currentController = ghostController;
            currentAnimator = ghostAnimator;
            currentSpecialAction = null;

            npcController = null;
            npcAnimator = null;
            npcStatusManager = null;
            punchAttackInfo = null;
        }
    }

     private void Update()
    {
        // ▼▼▼ この安全確認をUpdateの一番最初に配置し、重複を削除 ▼▼▼
        // 乗っ取り中のはずなのに、対象のNPCが破壊されていた場合
        if (nottoriController.isPossessing && targetNPC == null)
        {
            Debug.LogWarning("乗っ取り対象が消滅したため、強制的に憑依解除します。");
            
            if (reikonManager != null && nottoriController != null)
            {
                reikonManager.TakeDamage(nottoriController.deathPenaltyAmount);
            }
            
            nottoriController.ForceRelease();
            return; // このフレームの以降の処理は行わず、安全に終了する
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        // 右クリックで特殊能力を発動
        if (Input.GetButtonDown("Fire2") && currentSpecialAction != null)
        {
            // 相手がどんな能力かは知る必要がない。ただ「実行」ボタンを押すだけ。
            currentSpecialAction.PerformAction(this);
        }

        CheckForRecoveryItems();
        CheckForInteractables();
        UpdatePhasingState();

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
            velocity.y += Physics.gravity.y * Time.deltaTime;
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
    /// 指定された力でジャンプを実行する
    /// </summary>
    public void PerformJump(float customJumpPower)
    {
        if (currentController != null && currentController.isGrounded)
        {
            velocity.y = customJumpPower;
        }
    }

    /// <summary>
    /// 現在乗っ取っているNPCのStatusManagerを取得する
    /// </summary>
    public StatusManager GetPossessedStatusManager()
    {
        return npcStatusManager;
    }

    // ▼▼▼ 壁抜け状態を検知・通知する新しいメソッドを追加 ▼▼▼
    private void UpdatePhasingState()
    {
        // 憑依中は壁抜け判定を行わない
        if (IsPossessing())
        {
            // ReikonManagerの状態を「憑依中」として更新
            reikonManager.UpdateState(false, true);
            return;
        }

        // --- 幽霊状態の時の処理 ---

        // CharacterControllerのサイズと位置を使って、仮想的なチェックボックスを作成
        Vector3 boxCenter = transform.position + currentController.center;
        Vector3 halfExtents = new Vector3(currentController.radius, currentController.height / 2, currentController.radius);

        // チェックボックスが "wallLayer" と重なっているか判定
        bool isInsideWall = Physics.CheckBox(boxCenter, halfExtents, transform.rotation, wallLayer);

        // ReikonManagerに現在の状態を通知
        // isPhasing: 壁の中にいるか？ / isPossessing: 憑依中か？(ここではfalse)
        reikonManager.UpdateState(isInsideWall, false);
    }
    
    // ▼▼▼ インタラクション可能オブジェクトを検知する新しいメソッドを追加 ▼▼▼
    private void CheckForInteractables()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, interactableLayer);

        InteractiveSignboard closestSign = null;
        if (hitColliders.Length > 0)
        {
            // 1. 最も近いコライダーを1つだけ見つける
            Collider closestCollider = hitColliders
                .OrderBy(c => (transform.position - c.transform.position).sqrMagnitude)
                .FirstOrDefault();

            // 2. そのコライダーからコンポーネントを取得する
            if (closestCollider != null)
            {
                closestSign = closestCollider.GetComponent<InteractiveSignboard>();
            }
        }

        // --- 状態の比較と通知 ---
        if (closestSign != null && closestSign != currentSignboard)
        {
            if (currentSignboard != null)
            {
                currentSignboard.OnPlayerExit();
            }
            currentSignboard = closestSign;
            currentSignboard.OnPlayerEnter();
        }
        else if (closestSign == null && currentSignboard != null)
        {
            currentSignboard.OnPlayerExit();
            currentSignboard = null;
        }
    }
    
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