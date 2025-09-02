using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpPower = 5f;

    private CharacterController controller;    // Ghost用
    private CharacterController targetController; // 乗っ取りNPC用
    private Vector3 velocity;

    private GameObject targetNPC;  // 乗っ取り対象
    private GameObject ghost;      // Ghostオブジェクト

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!ghost) ghost = gameObject;
    }

    public void SetGhostReference(GameObject ghostObj)
    {
        ghost = ghostObj;
    }

    public void SetTargetNPC(GameObject npc)
    {
        targetNPC = npc;

        if (targetNPC != null)
        {
            targetController = targetNPC.GetComponent<CharacterController>();
            if (targetController == null)
            {
                targetController = targetNPC.AddComponent<CharacterController>();
                targetController.height = 2f;
                targetController.radius = 0.5f;
            }
        }
        else
        {
            targetController = null;
        }
    }

    private void Update()
    {
        GameObject objToMove = targetNPC != null ? targetNPC : ghost;
        CharacterController controllerToUse = targetNPC != null ? targetController : controller;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 回転（カメラ方向）
        Vector3 lookDir = Camera.main.transform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            objToMove.transform.rotation = Quaternion.Slerp(objToMove.transform.rotation,
                Quaternion.LookRotation(lookDir), rotationSpeed * Time.deltaTime);

        // 移動
        Vector3 move = objToMove.transform.forward * v + objToMove.transform.right * h;
        move = move.normalized * moveSpeed;

        // 重力
        if (!controllerToUse.isGrounded)
            velocity.y += Physics.gravity.y * Time.deltaTime;
        else
        {
            velocity.y = 0;
            if (Input.GetButtonDown("Jump"))
                velocity.y = jumpPower;
        }

        Vector3 finalMove = move + new Vector3(0, velocity.y, 0);
        controllerToUse.Move(finalMove * Time.deltaTime);

        // GhostをNPCに重ねる
        if (targetNPC != null && ghost != null)
        {
            ghost.transform.position = targetNPC.transform.position;
            ghost.transform.rotation = targetNPC.transform.rotation;
        }
    }
}
