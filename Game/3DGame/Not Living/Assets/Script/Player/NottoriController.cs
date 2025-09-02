using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使用するために追加

public class NottoriController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject ghost;
    public LayerMask targetLayer;
    public float rayDistance = 10f;

    [Header("Release Settings")]
    public float releaseForwardDistance = 2f;
    public float releaseHeight = 1f;

    [HideInInspector] public bool isPossessing = false;

    private GameObject currentNPC;
    private Renderer[] ghostRenderers;
    private PlayerController playerController;
    private NPCMove npcMove;
    private NavMeshAgent npcAgent; // NPCのNavMeshAgentを保持

    private void Awake()
    {
        if (!ghost) ghost = gameObject;
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();

        playerController = GetComponent<PlayerController>();
        if (!playerController)
            playerController = gameObject.AddComponent<PlayerController>();

        playerController.SetGhostReference(ghost);
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

        // NPCMove 停止
        npcMove = currentNPC.GetComponent<NPCMove>();
        if (npcMove != null) npcMove.isNottoried = true;

        // NavMeshAgentを無効化
        npcAgent = currentNPC.GetComponent<NavMeshAgent>();
        if (npcAgent != null) npcAgent.enabled = false;

        // PlayerController に NPC を設定
        playerController.SetTargetNPC(currentNPC);
        isPossessing = true;
    }

    private void ReleaseNPC()
    {
        if (!currentNPC) return;

        // NPCをNavMesh上の最も近い点に移動させてからAgentを有効にする
        if (NavMesh.SamplePosition(currentNPC.transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            currentNPC.transform.position = hit.position;
        }

        // NavMeshAgentを再度有効化
        if (npcAgent != null)
        {
            npcAgent.enabled = true;
        }

        // NPCMove 再開
        if (npcMove != null) npcMove.isNottoried = false;

        // Ghost を NPC 前方に出現
        Vector3 offset = currentNPC.transform.forward * releaseForwardDistance + Vector3.up * releaseHeight;
        ghost.transform.position = currentNPC.transform.position + offset;
        SetGhostVisible(true);

        currentNPC = null;
        playerController.SetTargetNPC(null);
        isPossessing = false;
    }

    private void SetGhostVisible(bool visible)
    {
        foreach (var rend in ghostRenderers) rend.enabled = visible;
    }
}