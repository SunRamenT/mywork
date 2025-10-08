using UnityEngine;
using System.Linq; // OrderByを使うために必要
using System.Collections;
using NUnit.Framework;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    //public float jumpPower = 5f;
    [Tooltip("この秒数以上、空中にいた場合のみ着地音を鳴らす")] //
    public float landingThreshold = 0.1f;
    [Tooltip("ジャンプボタンを離した時の、重力のかかり具合")] // ▼▼▼ 追加 ▼▼▼
    public float lowJumpGravityMultiplier = 2.5f;
    [Tooltip("落下中の重力のかかり具合")] // ▼▼▼ 追加 ▼▼▼
    public float fallGravityMultiplier = 2f;

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
    /// 現在乗っ取っているキャラクターのTagを外部に公開する
    public string PossessedCharacterTag => IsPossessing() ? targetNPC.tag : null;

    private bool wasGrounded; 
    private float timeInAir = 0f; // ▼▼▼ 追加 ▼▼▼

    private AudioSource audioSource;
    [Tooltip("アクションができないときに再生する音")]
    public AudioClip MissSound;
    [Tooltip("アクションができないときに再生する音")]
    public AudioClip warpSound;
    public AudioClip dodgeSound;

    [Header("回避設定")]
    [Tooltip("回避アニメーションのトリガー名")]
    
    public string stateID = "State";
    [Tooltip("回避のクールタイム（秒）")]
    public float dodgeCooldown = 1.5f;
    [Tooltip("回避の移動速度")]
    public float dodgeSpeed = 10f;
    [Tooltip("回避の持続時間（秒）")]
    public float dodgeDuration = 0.3f;
    [Tooltip("回避開始時の無敵時間（秒）")]
    public float dodgeInvincibilityTime = 0.1f;
    
    private float nextDodgeTime = 0f;
    public bool isDodging = false;

    private bool isAttack = false;

    // PlayerController.csにGetCurrentAnimator()を追加
    public Animator GetCurrentAnimator()
    {
        return currentAnimator;
    }

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
        // AudioSourceを自分自身から取得、またはなければ追加する
        audioSource = GetComponent<AudioSource>();
        currentSpecialAction = ghost.GetComponent<ISpecialAction>();
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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
                if (nottoriController != null) nottoriController.ForceRelease(); return;
            }
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

            // ▼▼▼ この行を修正 ▼▼▼
            // currentSpecialAction = null; // ← これが問題の原因だった
            // 正しくは、幽霊自身の特殊能力（ワープなど）を取得する
            currentSpecialAction = ghost.GetComponent<ISpecialAction>();
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

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
        float playerTimeScale = PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f;
        float playerDeltaTime = Time.deltaTime * playerTimeScale;

        // 現在のアニメーターの再生速度を、プレイヤーの時間倍率に合わせる
        if (currentAnimator != null)
        {
            currentAnimator.speed = playerTimeScale;
        }
        
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.Gameplay)
        {
            if (targetNPC != null && currentAnimator != null)
            {
                if (HasParameter(currentAnimator, horizontalFloatName)) currentAnimator.SetFloat(horizontalFloatName, 0);
                if (HasParameter(currentAnimator, verticalFloatName)) currentAnimator.SetFloat(verticalFloatName, 0);
            }
            return;
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        
        if (nottoriController.isPossessing && targetNPC == null)
        {
            Debug.LogWarning("乗っ取り対象が消滅したため、強制的に憑依解除します。");
            // 憑依解除処理を呼び出す
            nottoriController.ForceRelease();
            return;
        }
        if (targetNPC != null && npcStatusManager.IsDead)
        {
            // 霊魂ダメージなどのペナルティ処理
            if (reikonManager != null && nottoriController != null)
            {
                reikonManager.TakeDamage(nottoriController.deathPenaltyAmount);
            }
            // 憑依解除
            nottoriController.ForceRelease();
            return;
        }
        
        CheckForRecoveryItems();
        CheckForInteractables();
        UpdatePhasingState();

        if (!currentController || !currentController.enabled) return;

        if (Input.GetButtonDown("Fire2") && currentInteractable != null)
        {
            currentInteractable.OnInteract(this);
            return;
        }
        // isDodgingがtrueの場合は、移動入力を無視
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        //float h = isDodging ? 0f : Input.GetAxis("Horizontal");
        //float v = isDodging ? 0f : Input.GetAxis("Vertical");

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
                if (Input.GetButtonDown("Fire1") && isAttack == false)
                {
                    if (npcStatusManager != null && punchAttackInfo != null) { punchAttackInfo.damage = npcStatusManager.power; }
                    isAttack = true;
                    currentAnimator.SetTrigger(attackTriggerName);
                    StartCoroutine(canAttack());
                }
                // 特殊能力
                if (Input.GetButtonDown("Fire2") && currentSpecialAction != null)
                {
                    // 1. まずクールタイムが完了しているかチェック
                    if (currentSpecialAction.CooldownProgress >= 1.0f)
                    {
                        // 2. 完了していれば、音を鳴らして能力を発動
                        currentCharacter.GetComponent<CharacterSounds>()?.PlaySpecialAbilitySound();
                        currentSpecialAction.PerformAction(this);
                    }
                    else
                    {
                        // (任意)クールタイム中であることを示す音を鳴らしても良い
                        Debug.Log("特殊能力はクールタイム中です。");
                        if (MissSound != null)
                        {
                            audioSource.PlayOneShot(MissSound);
                        }
                    }
                }
                // ▼▼▼ 回避入力の判定を追加 ▼▼▼
                if (Input.GetButtonDown("Jump") && Time.time >= nextDodgeTime)
                {
                    StartCoroutine(Dodge());
                }
            }   
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        else
        {
            //幽霊時使えないボタンを押したとき
            if (Input.GetButtonDown("Fire1"))
            {
                if (MissSound != null)
                {
                    audioSource.PlayOneShot(MissSound);
                }
            }
            // 特殊能力
            if (Input.GetButtonDown("Fire2"))
            {
                // 1. まずクールタイムが完了しているかチェック
                if (currentSpecialAction.CooldownProgress >= 1.0f)
                {
                    // 2. 完了していれば、音を鳴らして能力を発動
                    currentSpecialAction.PerformAction(this);
                    if (warpSound != null)
                    {
                        audioSource.PlayOneShot(warpSound);
                    }
                }
                else
                {
                    // (任意)クールタイム中であることを示す音を鳴らしても良い
                    Debug.Log("特殊能力はクールタイム中です。");
                    if (MissSound != null)
                    {
                        audioSource.PlayOneShot(MissSound);
                    }
                }
                
            }
            if (Input.GetButtonDown("Jump"))
            {
                if (MissSound != null)
                {
                    audioSource.PlayOneShot(MissSound);
                }
            }
        }

        // ▼▼▼ 時間の取得方法を変更 ▼▼▼
        float deltaTime = Time.deltaTime * (PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f);

        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            currentCharacter.transform.rotation = Quaternion.Slerp(currentCharacter.transform.rotation, targetRotation, rotationSpeed * playerDeltaTime);
        }

        float currentSpeed = IsPossessing() && npcStatusManager != null ? npcStatusManager.speed : this.moveSpeed;
        Vector3 move = (currentCharacter.transform.forward * v + currentCharacter.transform.right * h).normalized * currentSpeed;

        // ▼▼▼ ジャンプと重力のロジックを修正 ▼▼▼
        bool isGrounded = currentController.isGrounded;

        if (isGrounded)
        {
            if (!wasGrounded && timeInAir > landingThreshold) { /* 着地音 */ }
            timeInAir = 0f;
            // 地面にいる間、Y速度がマイナスに溜まり続けないようにする
            if (velocity.y < 0) velocity.y = -2f; 
        }
        else 
        {
            timeInAir += Time.deltaTime;
            // --- ここからが可変ジャンプの核 ---
            // 上昇中（velocity.y > 0）にジャンプボタン(Fire2)が離されたら
            if (velocity.y > 0 && Input.GetButtonUp("Fire2"))
            {
                // 上昇の勢いを弱める
                velocity.y *= 0.5f; 
            }
            // 落下中（velocity.y < 0）は、少し強い重力をかけてスピーディーに落とす
            else if (velocity.y < 0)
            {
                velocity.y += Physics.gravity.y * fallGravityMultiplier * playerDeltaTime;
            }
            // それ以外（上昇中にボタンを押し続けている場合など）
            else
            {
                velocity.y += Physics.gravity.y * playerDeltaTime; // 通常の重力
            }
        }
        
        Vector3 finalMove = move + new Vector3(0, velocity.y, 0);
        currentController.Move(finalMove * playerDeltaTime);
        wasGrounded = isGrounded;
        
        if (targetNPC != null)
        {
            ghost.transform.position = targetNPC.transform.position;
            ghost.transform.rotation = targetNPC.transform.rotation;
        }
    }

    /// <summary>
    /// ジャンプ能力から呼び出され、上方向の初速を設定する
    /// </summary>
    public void ApplyJumpForce(float force)
    {
        if (currentController.isGrounded)
        {
            velocity.y = force;
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

    private IEnumerator canAttack()
    {
        // 30フレーム待つ
        for (var i = 0; i < 30; i++)
        {
            yield return null;
        }
        Debug.Log("Attack OK");
        isAttack = false;
    }

    private IEnumerator Dodge()
    {
        //isDodging = true;
        nextDodgeTime = Time.time + dodgeCooldown;
        // Dodgeフラグをセット（Animatorに伝える）
        if (HasParameter(currentAnimator, "Dodge"))
            currentAnimator.SetTrigger("Dodge");

        if (dodgeSound != null)
        {
            audioSource.PlayOneShot(dodgeSound);
        }

        // --- 無敵処理 ---
        // StatusManagerの回避専用無敵化メソッドを呼び出す
        if (npcStatusManager != null)
        {
            npcStatusManager.StartCoroutine(npcStatusManager.BecomeDodgeInvincible(dodgeInvincibilityTime));
        }

        // --- 回避の方向を決定 ---
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dodgeDirection;
        if (h != 0 || v != 0)
        {
            dodgeDirection = (currentCharacter.transform.forward * v + currentCharacter.transform.right * h).normalized;
        }
        else
        {
            dodgeDirection = currentCharacter.transform.forward;
        }
        if (HasParameter(currentAnimator, horizontalFloatName))
            currentAnimator.SetFloat(horizontalFloatName, h);
        if (HasParameter(currentAnimator, verticalFloatName))
            currentAnimator.SetFloat(verticalFloatName, v);

        // --- 移動処理 ---
        float elapsedTime = 0f;
        while (elapsedTime < dodgeDuration)
        {
            float deltaTime = Time.deltaTime * (PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f);
            // CharacterController.Moveを使うことで、壁との衝突判定が行われる
            currentController.Move(dodgeDirection * dodgeSpeed * deltaTime);
            elapsedTime += deltaTime;
            yield return null;
        }

        //isDodging = false;
    }

    /// 指定された力でジャンプを実行する
    public void PerformJump(float customJumpPower)
    {
        if (currentController != null && currentController.isGrounded)
        {
            velocity.y = customJumpPower;
        }
    }

    public bool HasParameter(Animator animator, string paramName)
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