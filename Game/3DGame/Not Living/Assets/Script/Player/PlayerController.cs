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

    [Header("Item Detection Settings")]
    public LayerMask reikonLayer;
    public float itemDetectionRadius = 1.5f;

    [Header("Interaction Settings")]
    public LayerMask interactableLayer;
    public float interactionRadius = 3f;

    [Header("Wall Phasing Settings")]
    public LayerMask wallLayer;

    [Header("Animator Settings")]
    public string attackTriggerName = "Attack";
    public string jumpTriggerName = "Jump";
    // ▼▼▼ Animatorのパラメータ名をInspectorで設定できるように変更 ▼▼▼
    public string horizontalFloatName = "Hor";
    public string verticalFloatName = "Vert";

    // --- Private Variables ---
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
    private AttackInfo punchAttackInfo;
    private Vector3 velocity;
    private IInteractable currentInteractable;
    private ISpecialAction currentSpecialAction;

    public Transform CurrentCharacterTransform => currentCharacter != null ? currentCharacter.transform : this.transform;
    public ISpecialAction CurrentSpecialAction => currentSpecialAction;
    [Tooltip("ダメージモーションのステートに設定したタグ名")] // ▼▼▼ 追加 ▼▼▼
    public string flinchingTagName = "Flinching";

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
    }

    public void SetTargetNPC(GameObject npc, Animator anim)
    {
        targetNPC = npc;
        if (targetNPC != null)
        {
            npcController = targetNPC.GetComponent<CharacterController>();
            npcAnimator = anim;
            npcStatusManager = targetNPC.GetComponent<StatusManager>();
            if (npcController == null) { Debug.LogError("乗っ取り対象のNPCにCharacterControllerがアタッチされていません！");
                if (nottoriController != null) nottoriController.ForceRelease(); return; }
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
            if (currentInteractable != null) { currentInteractable.OnPlayerExitRange(); currentInteractable = null; }
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
            if(targetNPC != null && currentAnimator != null) { currentAnimator.SetFloat(horizontalFloatName, 0); currentAnimator.SetFloat(verticalFloatName, 0); }
            return;
        }
        
        if (nottoriController.isPossessing && targetNPC == null)
        {
            Debug.LogWarning("乗っ取り対象が消滅したため、強制的に憑依解除します。");
            // 霊魂ダメージなどのペナルティ処理
            if (reikonManager != null && nottoriController != null)
            {
                reikonManager.TakeDamage(nottoriController.deathPenaltyAmount);
            }
            // 憑依解除処理を呼び出す
            nottoriController.ForceRelease();
            return;
        }


        CheckForRecoveryItems();
        CheckForInteractables();
        UpdatePhasingState();

        if (!currentController || !currentController.enabled) return;

        if (Input.GetButtonDown("Fire3") && currentInteractable != null)
        {
            currentInteractable.OnInteract(this);
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // ▼▼▼ アニメーションパラメータの更新ロジックを修正 ▼▼▼
        // currentAnimatorにパラメータが存在する場合のみ値を設定する
        if (HasParameter(currentAnimator, horizontalFloatName))
        {
            currentAnimator.SetFloat(horizontalFloatName, h);
        }
        if (HasParameter(currentAnimator, verticalFloatName))
        {
            currentAnimator.SetFloat(verticalFloatName, v);
        }
        
        if (IsPossessing())
        {
            AnimatorStateInfo stateInfo = currentAnimator.GetCurrentAnimatorStateInfo(0); // 0はベースレイヤー
            bool isFlinching = stateInfo.IsTag(flinchingTagName);

            // isFlinchingがfalseの時（＝ダメージ中でない時）だけ、以下の入力が可能
            if (!isFlinching)
            {
                // 攻撃
                if (Input.GetButtonDown("Fire1"))
                {
                    if (npcStatusManager != null && punchAttackInfo != null) { punchAttackInfo.damage = npcStatusManager.power; }
                    currentAnimator.SetTrigger(attackTriggerName);
                }
                // 特殊能力
                if (Input.GetButtonDown("Fire2") && currentSpecialAction != null)
                {
                    currentSpecialAction.PerformAction(this);
                }
            }
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            currentCharacter.transform.rotation = Quaternion.Slerp(currentCharacter.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        float currentSpeed = IsPossessing() && npcStatusManager != null ? npcStatusManager.speed : this.moveSpeed;
        Vector3 move = (currentCharacter.transform.forward * v + currentCharacter.transform.right * h).normalized * currentSpeed;

        if (currentController.isGrounded)
        {
            velocity.y = -0.1f;
        }
        else
        {
            velocity.y += Physics.gravity.y * Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && currentController.isGrounded)
        {
            if (IsPossessing()) // ジャンプは憑依中のみ可能
            {
                if (HasParameter(currentAnimator, jumpTriggerName))
                {
                    currentAnimator.SetTrigger(jumpTriggerName);
                }
                PerformJump(npcStatusManager != null ? npcStatusManager.jumpPower : this.jumpPower);
            }
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

    private bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
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