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
    
    [Header("Animator Settings")]
    public string possessionBoolName = "Nottori";
    public string jumpBoolName = "isJump";
    public string horizontalFloatName = "Hor";
    public string verticalFloatName = "Vert";

    [HideInInspector] public bool isPossessing = false;

    private GameObject currentNPC;
    private Renderer[] ghostRenderers;
    private PlayerController playerController;
    private CatchObject catchObject;
    private Transform ghostHand;
    private Animator ghostAnimator;
    private NPCMove npcMove;
    private NavMeshAgent npcAgent;

    private void Awake()
    {
        if (!ghost) ghost = gameObject;
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();

        playerController = GetComponent<PlayerController>();
        if (!playerController)
            playerController = gameObject.AddComponent<PlayerController>();

        catchObject = GetComponent<CatchObject>();
        if (catchObject != null)
        {
            ghostHand = catchObject.hand;
        }

        ghostAnimator = ghost.GetComponent<Animator>();
        if(ghostAnimator == null) Debug.LogError("GhostにAnimatorコンポーネントがありません！");
    }

    private void Update()
    {
        if (Input.GetButtonDown("Interact"))
        {
            if (isPossessing) ReleaseNPC();
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
        SetGhostVisible(false);

        if (ghostAnimator != null)
        {
            ghostAnimator.SetBool(possessionBoolName, true);
        }

        if (catchObject != null)
        {
            Transform npcHand = FindChildRecursive(npc.transform, "Hand");
            if (npcHand != null)
            {
                catchObject.hand = npcHand;
            }
            else
            {
                Debug.LogWarning($"NPC「{npc.name}」に 'Hand' という名前の子オブジェクトが見つかりません。");
                catchObject.hand = npc.transform;
            }
        }

        npcMove = currentNPC.GetComponent<NPCMove>();
        if (npcMove != null) npcMove.isNottoried = true;

        npcAgent = currentNPC.GetComponent<NavMeshAgent>();
        if (npcAgent != null) npcAgent.enabled = false;

        Animator npcAnimator = currentNPC.GetComponent<Animator>();
        playerController.SetTargetNPC(currentNPC, npcAnimator);
        isPossessing = true;
    }

    private void ReleaseNPC()
    {
        if (!currentNPC) return;

        if (catchObject != null)
        {
            catchObject.Release();
            catchObject.hand = ghostHand;
        }

        Animator npcAnimator = currentNPC.GetComponent<Animator>();
        if (npcAnimator != null)
        {
            if (HasParameter(npcAnimator, jumpBoolName))
                npcAnimator.SetBool(jumpBoolName, false);
            
            if (HasParameter(npcAnimator, horizontalFloatName))
                npcAnimator.SetFloat(horizontalFloatName, 0f);

            if (HasParameter(npcAnimator, verticalFloatName))
                npcAnimator.SetFloat(verticalFloatName, 0f);
        }

        if (ghostAnimator != null)
        {
            ghostAnimator.SetBool(possessionBoolName, false);
        }

        if (NavMesh.SamplePosition(currentNPC.transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            var npcController = currentNPC.GetComponent<CharacterController>();
            if (npcController != null) npcController.enabled = false;
            currentNPC.transform.position = hit.position;
            if (npcController != null) npcController.enabled = true;
        }

        if (npcAgent != null) npcAgent.enabled = true;
        if (npcMove != null) npcMove.isNottoried = false;
        
        Vector3 offset = currentNPC.transform.forward * releaseForwardDistance;
        ghost.transform.position = currentNPC.transform.position + offset;
        SetGhostVisible(true);

        currentNPC = null;
        playerController.SetTargetNPC(null, null);
        isPossessing = false;
    }

    private void SetGhostVisible(bool visible)
    {
        foreach (var rend in ghostRenderers) rend.enabled = visible;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
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