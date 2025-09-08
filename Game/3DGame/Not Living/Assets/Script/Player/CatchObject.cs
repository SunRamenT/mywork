using UnityEngine;

/// <summary>
/// 特定のレイヤーとタグを持つオブジェクトを掴んで運ぶクラス
/// </summary>
public class CatchObject : MonoBehaviour
{
    [Header("設定")]
    public Transform hand;
    public float maxGrabDistance = 3f;
    public LayerMask grabbableLayer;
    public string grabbableTag = "Item";

    private GameObject grabbedObject = null;
    private Rigidbody grabbedRigidbody = null;
    private Camera mainCamera;
    private PlayerController playerController;

    void Start()
    {
        mainCamera = Camera.main;
        playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        // 乗っ取り中でなければ何もしない
        if (playerController == null || !playerController.IsPossessing())
        {
            // もし乗っ取り解除時にアイテムを持っていたら、強制的に離す
            if (grabbedObject != null)
            {
                Release();
            }
            return;
        }
        
        // --- 以下は乗っ取り中のみ実行される ---

        if (Input.GetButtonDown("Fire3"))
        {
            if (grabbedObject == null)
            {
                TryGrab();
            }
            else
            {
                Release();
            }
        }

        if (grabbedObject != null && grabbedRigidbody != null)
        {
            grabbedRigidbody.MovePosition(hand.position);
            grabbedRigidbody.MoveRotation(transform.rotation); 
        }
    }

    private void TryGrab()
    {
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        if (Physics.Raycast(ray, out RaycastHit hit, maxGrabDistance, grabbableLayer))
        {
            if (!hit.collider.CompareTag(grabbableTag))
            {
                return;
            }
            
            if (hit.collider.GetComponent<ReikonItem>() != null)
            {
                return;
            }
            
            Grab(hit.collider.gameObject);
        }
    }

    private void Grab(GameObject objectToGrab)
    {
        grabbedObject = objectToGrab;
        if (grabbedObject.TryGetComponent<Rigidbody>(out grabbedRigidbody))
        {
            grabbedRigidbody.useGravity = false;
        }
        Debug.Log($"{grabbedObject.name} を掴んだ！");
    }

    public void Release()
    {
        if (grabbedRigidbody != null)
        {
            grabbedRigidbody.useGravity = true;
        }
        grabbedObject = null;
        grabbedRigidbody = null;
        Debug.Log("オブジェクトを離した！");
    }
}