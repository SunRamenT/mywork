using UnityEngine;
using UnityEngine.AI;
using System.Linq;

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
    
    [Header("Animator Settings")]
    public string possessionBoolName = "Nottori";
    public string jumpBoolName = "isJump";
    public string horizontalFloatName = "Hor";
    public string verticalFloatName = "Vert";

    [HideInInspector] public bool isPossessing = false;

    private GameObject currentNPC;
    private Renderer[] ghostRenderers;
    private PlayerController playerController;
    private Animator ghostAnimator;
    private NPCMove npcMove;
    private NavMeshAgent npcAgent;
    private CharacterController npcController;

    private void Awake()
    {
        if (!ghost) ghost = gameObject;
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        playerController = GetComponent<PlayerController>();
        ghostAnimator = ghost.GetComponent<Animator>();
        if(ghostAnimator == null) Debug.LogError("GhostにAnimatorコンポーネントがありません！");
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
        currentNPC = npc;
        isPossessing = true;
        SetGhostVisible(false);

        if (ghostAnimator != null)
        {
            ghostAnimator.SetBool(possessionBoolName, true);
        }

        npcMove = currentNPC.GetComponent<NPCMove>();
        if (npcMove != null) npcMove.isNottoried = true;

        // AI用のNavMeshAgentは無効化
        npcAgent = currentNPC.GetComponent<NavMeshAgent>();
        if (npcAgent != null) npcAgent.enabled = false;

        // プレイヤー操作用のCharacterControllerは有効化
        npcController = currentNPC.GetComponent<CharacterController>();
        if (npcController != null) npcController.enabled = true;

        Animator npcAnimator = currentNPC.GetComponent<Animator>();
        playerController.SetTargetNPC(currentNPC, npcAnimator);
    }
    
    public void ForceRelease()
    {
        if (!isPossessing) return;

        if(currentNPC != null)
        {
            Animator npcAnimator = currentNPC.GetComponent<Animator>();
            if (npcAnimator != null)
            {
                if (HasParameter(npcAnimator, jumpBoolName)) npcAnimator.SetBool(jumpBoolName, false);
                if (HasParameter(npcAnimator, horizontalFloatName)) npcAnimator.SetFloat(horizontalFloatName, 0f);
                if (HasParameter(npcAnimator, verticalFloatName)) npcAnimator.SetFloat(verticalFloatName, 0f);
            }
            
            // AI用のNavMeshAgentを有効化
            if (npcAgent != null) npcAgent.enabled = true;
            // プレイヤー操作用のCharacterControllerを無効化
            //if (npcController != null) npcController.enabled = false;

            if (npcMove != null) npcMove.isNottoried = false;

            Vector3 offset = currentNPC.transform.forward * releaseForwardDistance;
            ghost.transform.position = currentNPC.transform.position + offset;
        }

        if (ghostAnimator != null)
        {
            ghostAnimator.SetBool(possessionBoolName, false);
        }
        
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