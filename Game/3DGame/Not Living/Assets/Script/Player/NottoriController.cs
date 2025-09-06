using UnityEngine;
using UnityEngine.AI;

public class NottoriController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject ghost;
    public LayerMask targetLayer;
    public float rayDistance = 10f;

    [Header("Release Settings")]
    public float releaseForwardDistance = 2f;
    
    [Header("Animator Settings")]
    [Tooltip("Animatorで乗っ取り状態を管理するBoolパラメータ名")]
    public string possessionBoolName = "Nottori";
    [Tooltip("NPCのジャンプBool名")]
    public string jumpBoolName = "isJump";
    [Tooltip("NPCの水平移動Float名")]
    public string horizontalFloatName = "Hor";
    [Tooltip("NPCの垂直移動Float名")]
    public string verticalFloatName = "Vert";


    [HideInInspector] public bool isPossessing = false;

    private GameObject currentNPC;
    private Renderer[] ghostRenderers;
    private PlayerController playerController;
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

        // ▼▼▼ ここから修正 ▼▼▼
        // NPCのAnimatorを取得し、プレイヤー操作用のアニメーションパラメータをリセットする
        Animator npcAnimator = currentNPC.GetComponent<Animator>();
        if (npcAnimator != null)
        {
            npcAnimator.SetBool(jumpBoolName, false);
            npcAnimator.SetFloat(horizontalFloatName, 0f);
            npcAnimator.SetFloat(verticalFloatName, 0f);
        }
        // ▲▲▲ ここまで修正 ▲▲▲

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
}