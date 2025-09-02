using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpPower = 5f;

    private CharacterController controller;       // Ghost用
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
            // NPCから既存のCharacterControllerを取得する
            targetController = targetNPC.GetComponent<CharacterController>();
            if (targetController == null)
            {
                Debug.LogError("乗っ取り対象のNPCにCharacterControllerがアタッチされていません！");
                return;
            }

            // Ghostのコントローラーを無効にし、NPCのコントローラーを有効にする
            controller.enabled = false;
            targetController.enabled = true;
        }
        else
        {
            // Ghostの操作に戻るので、NPCのコントローラーを無効にする
            if(targetController != null)
            {
                targetController.enabled = false;
            }
            // Ghostのコントローラーを有効にする
            controller.enabled = true;
            targetController = null;
        }
    }

    private void Update()
    {
        // 操作対象のオブジェクトとコントローラーを選択
        GameObject objToMove = targetNPC != null ? targetNPC : ghost;
        CharacterController controllerToUse = targetNPC != null ? targetController : controller;

        // コントローラーが無効な場合は処理を中断
        if (!controllerToUse || !controllerToUse.enabled) return;

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
        {
            velocity.y += Physics.gravity.y * Time.deltaTime;
        }
        else
        {
            // 地面にいるときは落下速度をリセット
            velocity.y = -0.1f;
            if (Input.GetButtonDown("Jump"))
            {
                velocity.y = jumpPower;
            }
        }

        Vector3 finalMove = move + new Vector3(0, velocity.y, 0);
        controllerToUse.Move(finalMove * Time.deltaTime);
        
        // ▼▼▼【修正箇所】▼▼▼
        // 乗っ取り中は、GhostのTransformをNPCのTransformに同期させる
        if (targetNPC != null)
        {
            ghost.transform.position = targetNPC.transform.position;
            ghost.transform.rotation = targetNPC.transform.rotation;
        }
    }
}