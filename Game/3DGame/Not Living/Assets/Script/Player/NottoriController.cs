using UnityEngine;
using UnityEngine.AI;
using System.Linq;

[RequireComponent(typeof(AudioSource))]
public class NottoriController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject ghost;
    public LayerMask targetLayer;
    public float rayDistance = 10f;

    [Header("Release Settings")]
    public float releaseForwardDistance = 2f;
    [Tooltip("乗っ取り対象の消滅時に受ける霊魂ダメージ")]
    public float deathPenaltyAmount = 25f;
    
    [Header("Sound Settings")]
    public AudioClip possessSound;
    public AudioClip releaseSound;
    
    [Header("Effect Settings")]
    public GameObject possessEffectPrefab;
    public GameObject releaseEffectPrefab;
    [Tooltip("エフェクトを表示する高さのオフセット")]
    public float effectYOffset = 1.0f;

    [Header("Animator Settings")]
    public string possessionBoolName = "Nottori";
    public string jumpBoolName = "isJump";
    public string horizontalFloatName = "Hor";
    public string verticalFloatName = "Vert";

    [HideInInspector] public bool isPossessing = false;

    // --- Private Variables ---
    private GameObject currentNPC;
    private Renderer[] ghostRenderers;
    private PlayerController playerController;
    private Animator ghostAnimator;
    private NPCMove npcMove;
    private NavMeshAgent npcAgent;
    private CharacterController npcController;
    private AudioSource audioSource;

    private void Awake()
    {
        if (!ghost) ghost = gameObject;
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        playerController = GetComponent<PlayerController>();
        ghostAnimator = ghost.GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if(ghostAnimator == null) Debug.LogError("GhostにAnimatorコンポー-ネントがありません！");
    }

    private void Update()
    {
        if (Input.GetButtonDown("Interact"))
        {
            if (isPossessing) ForceRelease();
            else TryPossess();
        }
    }

    private void TryPossess()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, targetLayer))
        {
            GameObject npc = hit.collider.gameObject;
            if (npc != null && npc != ghost)
            {
                StartPossess(npc);
            }
        }
    }

    private void StartPossess(GameObject npc)
    {
        if (possessSound != null) audioSource.PlayOneShot(possessSound);
        
        if (possessEffectPrefab != null)
        {
            //  乗っ取り時は「乗っ取る対象のNPCの位置」を基準にする
            Vector3 spawnPosition = npc.transform.position + new Vector3(0, effectYOffset, 0);
            Instantiate(possessEffectPrefab, spawnPosition, Quaternion.identity);
        }

        currentNPC = npc;
        isPossessing = true;
        SetGhostVisible(false);

        if (ghostAnimator != null) ghostAnimator.SetBool(possessionBoolName, true);
        
        npcMove = currentNPC.GetComponent<NPCMove>();
        if (npcMove != null) npcMove.isNottoried = true;
        
        npcAgent = currentNPC.GetComponent<NavMeshAgent>();
        if (npcAgent != null) npcAgent.enabled = false;
        
        npcController = currentNPC.GetComponent<CharacterController>();
        if (npcController != null) npcController.enabled = true;

        Animator npcAnimator = currentNPC.GetComponent<Animator>();
        playerController.SetTargetNPC(currentNPC, npcAnimator);
    }
    
    public void ForceRelease()
    {
        if (!isPossessing) return;
        
        if (releaseSound != null) audioSource.PlayOneShot(releaseSound);

        if(currentNPC != null)
        {
            // 乗っ取り解除時は「幽霊が出現する位置」を基準にする
            // 先に幽霊の出現位置を計算する
            Vector3 releasePosition = currentNPC.transform.position + (currentNPC.transform.forward * releaseForwardDistance);

            if (releaseEffectPrefab != null)
            {
                Vector3 spawnPosition = releasePosition + new Vector3(0, effectYOffset, 0);
                Instantiate(releaseEffectPrefab, spawnPosition, Quaternion.identity);
            }
            
            // 計算した出現位置に幽霊を移動させる
            ghost.transform.position = releasePosition;

            Animator npcAnimator = currentNPC.GetComponent<Animator>();
            if (npcAnimator != null)
            {
                if (HasParameter(npcAnimator, jumpBoolName)) npcAnimator.SetBool(jumpBoolName, false);
                if (HasParameter(npcAnimator, horizontalFloatName)) npcAnimator.SetFloat(horizontalFloatName, 0f);
                if (HasParameter(npcAnimator, verticalFloatName)) npcAnimator.SetFloat(verticalFloatName, 0f);
            }
            
            if (npcAgent != null) npcAgent.enabled = true;
            if (npcMove != null) npcMove.isNottoried = false;
        }

        if (ghostAnimator != null) ghostAnimator.SetBool(possessionBoolName, false);
        
        SetGhostVisible(true);
        playerController.SetTargetNPC(null, null);

        currentNPC = null;
        isPossessing = false;
    }
    
    private void SetGhostVisible(bool visible)
    {
        foreach (var rend in ghostRenderers) rend.enabled = visible;
    }
    
    private bool HasParameter(Animator animator, string paramName)
    {
        if (string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }
}