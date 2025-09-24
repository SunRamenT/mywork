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

    // 現在フォーカスしている対象を、より汎用的なIInteractableで保持
    private IInteractable currentInteractable;

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
            if (npcController == null) { Debug.LogError("乗っ取り対象のNPCにCharacterControllerがアタッチされていません！"); if(nottoriController != null) nottoriController.ForceRelease(); return; }
            HitboxController hitboxCtrl = targetNPC.GetComponentInChildren<HitboxController>();
            if (hitboxCtrl != null && hitboxCtrl.attackHitboxes.Length > 0) { punchAttackInfo = hitboxCtrl.attackHitboxes[0].GetComponent<AttackInfo>(); }
            
            currentSpecialAction = targetNPC.GetComponent<ISpecialAction>();

            ghostController.enabled = false;
            currentCharacter = targetNPC;
            currentController = npcController;
            currentAnimator = npcAnimator;
        }
        else
        {
            ghostController.enabled = true;
            
            currentSpecialAction = null;

            // ▼▼▼ 憑依解除時にインタラクション対象もリセット ▼▼▼
            if (currentInteractable != null)
            {
                currentInteractable.OnPlayerExitRange();
                currentInteractable = null;
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.MiniGameActive)
        {
            if(targetNPC != null) { currentAnimator.SetFloat("Hor", 0); currentAnimator.SetFloat("Vert", 0); }
            return;
        }

        // --- インタラクション入力 ---
        if (Input.GetButtonDown("Fire3") && currentInteractable != null)
        {
            currentInteractable.OnInteract(this);
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        if (nottoriController.isPossessing && targetNPC == null)
        {
            nottoriController.ForceRelease();
            return;
        }
        
        // --- 毎フレーム実行するチェック処理 ---
        CheckForRecoveryItems();
        CheckForInteractables();
        UpdatePhasingState();
        
        if (!currentController || !currentController.enabled) return;
        
        // --- 移動とアクション入力 ---
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (targetNPC != null)
        {
            currentAnimator.SetFloat("Hor", h);
            currentAnimator.SetFloat("Vert", v);
            if (Input.GetButtonDown("Fire1"))
            {
                if (npcStatusManager != null && punchAttackInfo != null) { punchAttackInfo.damage = npcStatusManager.power; }
                currentAnimator.SetTrigger(attackTriggerName);
            }
            if (Input.GetButtonDown("Fire2") && currentSpecialAction != null)
            {
                currentSpecialAction.PerformAction(this);
            }
            if (Input.GetButtonDown("Jump") && currentController.isGrounded)
            {
                currentAnimator.SetTrigger(jumpTriggerName);
                
                // 実際にジャンプさせる
                PerformJump((targetNPC != null && npcStatusManager != null) ? npcStatusManager.jumpPower : this.jumpPower);
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

        if (currentController.isGrounded)
        {
            velocity.y = -0.1f;
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
    
    private void CheckForInteractables()
    {
        // 自分の現在位置を中心に、指定した半径・レイヤー内の全てのコライダーを見つけ出し、配列に格納する
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionRadius, interactableLayer);

        // 見つけた「操作可能な対象」を一時的に保持するための変数を準備する
        IInteractable closestInteractable = null;

        // もしコライダーが1つ以上見つかった場合
        if (hitColliders.Length > 0)
        {
            // 見つかった全てのコライダーを、自分との距離が近い順に並び替える
            Collider closestCollider = hitColliders
                .OrderBy(c => (transform.position - c.transform.position).sqrMagnitude)
                .FirstOrDefault(); // 並び替えた後、リストの先頭（＝最も近いもの）を1つだけ取り出す
            
            // 最も近いコライダーが確實に存在する場合
            if (closestCollider != null)
            {
                // そのコライダーがアタッチされているGameObjectから、IInteractableインターフェースを持つコンポーネントを探す
                closestInteractable = closestCollider.GetComponent<IInteractable>();
            }
        }
        
        // --- ここから、前のフレームの状態と比較して、イベントを通知する ---

        // 「今フレームで見つけた最も近い対象」が存在し、かつ「前のフレームでフォーカスしていた対象」と違う場合
        // つまり、新しく何かの範囲に入ったか、別の対象にフォーカスを乗り換えた瞬間
        if (closestInteractable != null && closestInteractable != currentInteractable)
        {
            // もし「前のフレームでフォーカスしていた対象」が存在するなら、まずその対象に「範囲外に出たよ」と通知する
            if (currentInteractable != null)
            {
                currentInteractable.OnPlayerExitRange();
            }
            // 「現在のフォーカス対象」を、新しく見つけた対象に更新する
            currentInteractable = closestInteractable;
            // 新しい対象に「範囲内に入ったよ」と通知する
            currentInteractable.OnPlayerEnterRange();
        }
        //  今フレームで見つけた最も近い対象が存在せず、かつ前のフレームではフォーカスしていた対象がいた場合
        //  何かの範囲から完全に出た瞬間
        else if (closestInteractable == null && currentInteractable != null)
        {
            // 「前のフレームでフォーカスしていた対象」に「範囲外に出たよ」と通知する
            currentInteractable.OnPlayerExitRange();
            // 「現在のフォーカス対象」を空にする
            currentInteractable = null;
        }
    }

    /// 指定された力でジャンプを実行する
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
    
    public bool IsCollisionsEnabled()
    {
        return currentController != null ? currentController.detectCollisions : false;
    }
    
    public bool IsPossessing()
    {
        return targetNPC != null;
    }
}