using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    [Tooltip("この秒数以上、空中にいた場合のみ着地音を鳴らす")]
    public float landingThreshold = 0.1f;
    [Tooltip("ジャンプボタンを離した時の、重力のかかり具合")]
    public float lowJumpGravityMultiplier = 2.5f;
    [Tooltip("落下中の重力のかかり具合")]
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
    public string horizontalFloatName = "Hor";
    public string verticalFloatName = "Vert";

    // --- Private Variables ---
    private CharacterController currentController;
    public Animator currentAnimator;
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
    [Tooltip("ダメージモーションのステートに設定したタグ名")]
    public string flinchingTagName = "Flinching";
    public string PossessedCharacterTag => IsPossessing() ? targetNPC.tag : null;

    private bool wasGrounded;
    private float timeInAir = 0f;

    private AudioSource audioSource;
    [Tooltip("アクションができないときに再生する音")]
    public AudioClip MissSound;
    [Tooltip("ワープ時に再生する音")]
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
    private int combocount = 0;

    [Header("Pick Up Settings")]
    [Tooltip("アイテムを拾える範囲")]
    public float pickupRadius = 2.0f;
    [Tooltip("拾えるアイテムのレイヤー（Trashなど）")]
    public LayerMask pickupLayer;
    [Tooltip("拾ったアイテムを固定する位置（手など）")]
    public Transform handHoldPosition;

    private TrashItem currentHeldItem;

    public Transform respawnPoint;

    // ★修正: Camera.mainを毎フレーム検索しないようにAwakeでキャッシュする
    private Camera m_MainCamera;

    // ★修正: OverlapSphereNonAlloc用の事前確保バッファ
    //   3つのOverlapSphere呼び出しが同一フレーム内で順番に実行されるため、バッファを共有できる
    private const int OVERLAP_BUFFER_SIZE = 16;
    private Collider[] m_OverlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];

    // ★修正: animatorのパラメータ名をHashSetにキャッシュし、HasParameterの毎フレームコピーを回避する
    //   animatorが切り替わるタイミング（SetTargetNPC）でキャッシュを更新する
    private HashSet<string> m_AnimatorParamCache = new HashSet<string>();
    private Animator m_CachedParamAnimator = null;

    // ★修正: canAttackコルーチンのWaitForSeconds再利用キャッシュ
    private WaitForSeconds m_WaitAttack40 = new WaitForSeconds(40f / 60f);
    private WaitForSeconds m_WaitAttack80 = new WaitForSeconds(80f / 60f);
    private WaitForSeconds m_WaitAttack100 = new WaitForSeconds(100f / 60f);

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
        audioSource = GetComponent<AudioSource>();
        currentSpecialAction = ghost.GetComponent<ISpecialAction>();

        // ★修正: Camera.mainをキャッシュ
        m_MainCamera = Camera.main;

        // 初期animatorのパラメータをキャッシュ
        RebuildAnimatorParamCache(ghostAnimator);
    }

    // ★修正: animatorが切り替わるたびにパラメータキャッシュを再構築するメソッド
    private void RebuildAnimatorParamCache(Animator animator)
    {
        m_AnimatorParamCache.Clear();
        m_CachedParamAnimator = animator;
        if (animator == null) return;
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            m_AnimatorParamCache.Add(param.name);
        }
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
            if (hitboxCtrl != null && hitboxCtrl.attackHitboxes.Length > 0)
            {
                punchAttackInfo = hitboxCtrl.attackHitboxes[0].GetComponent<AttackInfo>();
            }
            currentSpecialAction = targetNPC.GetComponent<ISpecialAction>();
            ghostController.enabled = false;
            currentCharacter = targetNPC;
            currentController = npcController;
            currentAnimator = npcAnimator;
        }
        else
        {
            ghostController.enabled = true;
            currentSpecialAction = ghost.GetComponent<ISpecialAction>();
            if (currentInteractable != null) { currentInteractable.OnPlayerExitRange(); currentInteractable = null; }
            currentCharacter = ghost;
            currentController = ghostController;
            currentAnimator = ghostAnimator;
            npcController = null;
            npcAnimator = null;
            npcStatusManager = null;
            punchAttackInfo = null;
        }

        // ★修正: animator切り替え時にパラメータキャッシュを更新
        RebuildAnimatorParamCache(currentAnimator);
    }

    private void Update()
    {
        float playerTimeScale = PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f;
        float playerDeltaTime = Time.deltaTime * playerTimeScale;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameStateManager.GameState.Gameplay)
        {
            if (targetNPC != null && currentAnimator != null)
            {
                if (HasParameter(currentAnimator, horizontalFloatName)) currentAnimator.SetFloat(horizontalFloatName, 0);
                if (HasParameter(currentAnimator, verticalFloatName)) currentAnimator.SetFloat(verticalFloatName, 0);
            }
            return;
        }

        if (nottoriController.isPossessing && targetNPC == null)
        {
            Debug.LogWarning("乗っ取り対象が消滅したため、強制的に憑依解除します。");
            nottoriController.ForceRelease();
            return;
        }
        if (targetNPC != null && npcStatusManager.IsDead)
        {
            if (reikonManager != null && nottoriController != null)
            {
                reikonManager.TakeDamage(nottoriController.deathPenaltyAmount);
            }
            nottoriController.ForceRelease();
            return;
        }

        CheckForRecoveryItems();
        CheckForInteractables();
        UpdatePhasingState();

        if (!currentController || !currentController.enabled) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

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
            AnimatorStateInfo stateInfo = currentAnimator.GetCurrentAnimatorStateInfo(0);
            bool isFlinching = stateInfo.IsTag(flinchingTagName);

            if (!isFlinching)
            {
                if (Input.GetButtonDown("Fire1") && isAttack == false)
                {
                    if (npcStatusManager != null && punchAttackInfo != null)
                    {
                        punchAttackInfo.damage = npcStatusManager.power;
                    }
                    isAttack = true;

                    if (combocount >= 4) combocount = 0;

                    // ★修正: attackTriggerNameフィールドを上書きせず、ローカル変数で管理する
                    //   フィールドを直接書き換えるとInspectorの設定値が失われる
                    string triggerToFire;
                    WaitForSeconds waitTime;

                    if (combocount == 0)
                    {
                        triggerToFire = "Attack1";
                        waitTime = m_WaitAttack40;
                    }
                    else if (combocount == 1)
                    {
                        triggerToFire = "Attack2";
                        waitTime = m_WaitAttack40;
                    }
                    else if (combocount == 2)
                    {
                        triggerToFire = "Kick1";
                        waitTime = m_WaitAttack80;
                    }
                    else
                    {
                        triggerToFire = "Kick2";
                        waitTime = m_WaitAttack100;
                    }

                    currentAnimator.SetTrigger(triggerToFire);
                    StartCoroutine(canAttack(waitTime));
                    combocount++;
                }

                if (Input.GetButtonDown("Fire2"))
                {
                    if (currentInteractable != null)
                    {
                        currentInteractable.OnInteract(this);
                    }
                    else if (currentHeldItem == null)
                    {
                        TrashItem trash = CheckForTrash();
                        if (trash != null)
                        {
                            currentHeldItem = trash;
                            currentHeldItem.OnPickUp(handHoldPosition);
                        }
                        else if (currentSpecialAction != null)
                        {
                            combocount = 0;
                            if (currentSpecialAction.CooldownProgress >= 1.0f)
                            {
                                currentCharacter.GetComponent<CharacterSounds>()?.PlaySpecialAbilitySound();
                                currentSpecialAction.PerformAction(this);
                            }
                            else
                            {
                                Debug.Log("特殊能力はクールタイム中です。");
                                if (MissSound != null) audioSource.PlayOneShot(MissSound);
                            }
                        }
                    }
                }

                if (Input.GetButtonUp("Fire2"))
                {
                    if (currentHeldItem != null)
                    {
                        currentHeldItem.OnDrop();
                        currentHeldItem = null;
                    }
                }

                if (Input.GetButtonDown("Jump") && Time.time >= nextDodgeTime)
                {
                    combocount = 0;
                    StartCoroutine(Dodge());
                }
            }
        }
        else
        {
            if (Input.GetButtonDown("Fire1"))
            {
                if (MissSound != null) audioSource.PlayOneShot(MissSound);
            }
            if (Input.GetButtonDown("Fire2"))
            {
                if (currentSpecialAction.CooldownProgress >= 1.0f)
                {
                    currentSpecialAction.PerformAction(this);
                    if (warpSound != null) audioSource.PlayOneShot(warpSound);
                }
                else
                {
                    Debug.Log("特殊能力はクールタイム中です。");
                    if (MissSound != null) audioSource.PlayOneShot(MissSound);
                }
            }
            if (Input.GetButtonDown("Jump"))
            {
                if (MissSound != null) audioSource.PlayOneShot(MissSound);
            }
        }

        // ★修正: 上で定義済みの playerDeltaTime を使い、重複定義していた deltaTime を削除
        if (m_MainCamera != null)
        {
            Vector3 lookDir = m_MainCamera.transform.forward;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                currentCharacter.transform.rotation = Quaternion.Slerp(currentCharacter.transform.rotation, targetRotation, rotationSpeed * playerDeltaTime);
            }
        }

        float currentSpeed = IsPossessing() && npcStatusManager != null ? npcStatusManager.speed : this.moveSpeed;
        Vector3 move = (currentCharacter.transform.forward * v + currentCharacter.transform.right * h).normalized * currentSpeed;

        bool isGrounded = currentController.isGrounded;

        if (isGrounded)
        {
            if (!wasGrounded && timeInAir > landingThreshold) { /* 着地音 */ }
            timeInAir = 0f;
            if (velocity.y < 0) velocity.y = -2f;
        }
        else
        {
            timeInAir += Time.deltaTime;
            if (velocity.y > 0 && Input.GetButtonUp("Fire2"))
            {
                velocity.y *= 0.5f;
            }
            else if (velocity.y < 0)
            {
                velocity.y += Physics.gravity.y * fallGravityMultiplier * playerDeltaTime;
            }
            else
            {
                velocity.y += Physics.gravity.y * playerDeltaTime;
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

        if (currentCharacter.transform.position.y < -5f)
        {
            Vector3 targetPos = (respawnPoint != null) ? respawnPoint.position : Vector3.zero;
            Teleport(targetPos);
            if (IsPossessing() && npcStatusManager != null)
            {
                npcStatusManager.TakeDamage(10, null);
            }
            Debug.Log("落下検知：リスポーン地点へワープしました。");
        }
    }

    public void ApplyJumpForce(float force)
    {
        if (currentController.isGrounded)
        {
            velocity.y = force;
        }
    }

    private void CheckForInteractables()
    {
        // ★修正: OverlapSphereNonAllocで事前確保バッファを使い、毎フレームのheapアロケーションを回避
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, interactionRadius, m_OverlapBuffer, interactableLayer);

        IInteractable closestInteractable = null;

        if (hitCount > 0)
        {
            // ★修正: LINQのOrderBy+FirstOrDefaultをforループに置き換え、EnumeratorのGCAllocを回避
            Collider closestCollider = null;
            float minSqrDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                float sqrDist = (transform.position - m_OverlapBuffer[i].transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closestCollider = m_OverlapBuffer[i];
                }
            }

            if (closestCollider != null)
            {
                closestInteractable = closestCollider.GetComponent<IInteractable>();
            }
        }

        if (closestInteractable != null && closestInteractable != currentInteractable)
        {
            if (currentInteractable != null) currentInteractable.OnPlayerExitRange();
            currentInteractable = closestInteractable;
            currentInteractable.OnPlayerEnterRange();
        }
        else if (closestInteractable == null && currentInteractable != null)
        {
            currentInteractable.OnPlayerExitRange();
            currentInteractable = null;
        }
    }

    // ★修正: WaitForSecondsをキャッシュ済みのものを受け取ることで毎回のnewを回避
    private IEnumerator canAttack(WaitForSeconds waitTime)
    {
        yield return waitTime;
        Debug.Log("Attack OK");
        isAttack = false;
    }

    private IEnumerator Dodge()
    {
        nextDodgeTime = Time.time + dodgeCooldown;
        if (HasParameter(currentAnimator, "Dodge"))
            currentAnimator.SetTrigger("Dodge");

        if (dodgeSound != null) audioSource.PlayOneShot(dodgeSound);

        if (npcStatusManager != null)
        {
            npcStatusManager.StartCoroutine(npcStatusManager.BecomeDodgeInvincible(dodgeInvincibilityTime));
        }

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

        float elapsedTime = 0f;
        while (elapsedTime < dodgeDuration)
        {
            float deltaTime = Time.deltaTime * (PlayerTimeManager.Instance?.PlayerTimeScale ?? 1f);
            currentController.Move(dodgeDirection * dodgeSpeed * deltaTime);
            elapsedTime += deltaTime;
            yield return null;
        }
    }

    public void PerformJump(float customJumpPower)
    {
        if (currentController != null && currentController.isGrounded)
        {
            velocity.y = customJumpPower;
        }
    }

    // ★修正: animator.parametersの毎フレームコピーをやめ、キャッシュ済みHashSetで参照する
    //   animatorが切り替わった場合はRebuildAnimatorParamCacheで更新される
    public bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;

        // animatorが切り替わっていたらキャッシュを再構築する
        if (animator != m_CachedParamAnimator)
        {
            RebuildAnimatorParamCache(animator);
        }

        return m_AnimatorParamCache.Contains(paramName);
    }

    public void Teleport(Vector3 targetPosition)
    {
        velocity = Vector3.zero;
        timeInAir = 0f;

        if (currentController != null) currentController.enabled = false;

        if (targetNPC != null)
        {
            targetNPC.transform.position = targetPosition;
            this.transform.position = targetPosition;
        }
        else
        {
            this.transform.position = targetPosition;
        }

        Physics.SyncTransforms();

        if (currentController != null) currentController.enabled = true;
    }

    public StatusManager GetPossessedStatusManager()
    {
        return npcStatusManager;
    }

    private void UpdatePhasingState()
    {
        if (IsPossessing())
        {
            reikonManager.UpdateState(false, true);
            return;
        }

        Vector3 boxCenter = transform.position + currentController.center;
        Vector3 halfExtents = new Vector3(currentController.radius, currentController.height / 2, currentController.radius);
        bool isInsideWall = Physics.CheckBox(boxCenter, halfExtents, transform.rotation, wallLayer);
        reikonManager.UpdateState(isInsideWall, false);
    }

    private void CheckForRecoveryItems()
    {
        // ★修正: OverlapSphereNonAllocで共有バッファを使いheapアロケーションを回避
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, itemDetectionRadius, m_OverlapBuffer, reikonLayer);

        if (hitCount > 0)
        {
            // ★修正: LINQをforループに置き換え
            Collider closest = null;
            float minSqrDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                float sqrDist = (transform.position - m_OverlapBuffer[i].transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = m_OverlapBuffer[i];
                }
            }

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

    private TrashItem CheckForTrash()
    {
        // ★修正: OverlapSphereNonAllocで共有バッファを使いheapアロケーションを回避
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, pickupRadius, m_OverlapBuffer, pickupLayer);

        if (hitCount > 0)
        {
            // ★修正: LINQをforループに置き換え
            Collider closest = null;
            float minSqrDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                float sqrDist = (transform.position - m_OverlapBuffer[i].transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = m_OverlapBuffer[i];
                }
            }

            if (closest != null)
            {
                return closest.GetComponent<TrashItem>();
            }
        }
        return null;
    }
}