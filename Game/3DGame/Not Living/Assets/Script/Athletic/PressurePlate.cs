using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("踏んだ時に沈み込む深さ")]
    public float pressDepth = 0.1f;
    [Tooltip("スイッチの有効半径（この範囲外に出たら強制的にOFFにする）")] // ▼▼▼ 追加 ▼▼▼
    public float checkRadius = 1.5f; // スイッチの大きさより少し大きめに設定
    
    [Header("フィルタ設定")]
    public List<string> targetTags; 

    [Header("イベント")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    private List<GameObject> overlappingObjects = new List<GameObject>();
    
    private Vector3 initialPos;
    private Vector3 pressedPos;
    private bool isPressed = false;

    void Start()
    {
        initialPos = transform.localPosition;
        pressedPos = initialPos - new Vector3(0, pressDepth, 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        if (!IsValidObject(other)) return;

        GameObject rootObj = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!overlappingObjects.Contains(rootObj))
        {
            overlappingObjects.Add(rootObj);
        }

        CheckState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;

        GameObject rootObj = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        RemoveObject(rootObj);
    }

    // ▼▼▼ Updateロジックを強化 ▼▼▼
    void Update()
    {
        if (overlappingObjects.Count > 0)
        {
            // 削除リストを作成（ループ中にリストを変更できないため）
            List<GameObject> toRemove = new List<GameObject>();

            foreach (var obj in overlappingObjects)
            {
                // 1. オブジェクトが消滅している(null)場合
                if (obj == null)
                {
                    toRemove.Add(obj);
                    continue;
                }

                // 2. オブジェクトが非アクティブになっている場合（プールに戻されたなど）
                if (!obj.activeInHierarchy)
                {
                    toRemove.Add(obj);
                    continue;
                }

                // 3. オブジェクトがスイッチから遠く離れている場合
                // （TriggerExitが呼ばれずにワープや高速移動した場合の対策）
                float dist = Vector3.Distance(transform.position, obj.transform.position);
                if (dist > checkRadius)
                {
                    toRemove.Add(obj);
                }
            }

            // 削除対象を一括でリストから外す
            if (toRemove.Count > 0)
            {
                foreach (var removeObj in toRemove)
                {
                    overlappingObjects.Remove(removeObj);
                }
                CheckState();
            }
        }
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    private void RemoveObject(GameObject obj)
    {
        if (overlappingObjects.Contains(obj))
        {
            overlappingObjects.Remove(obj);
            CheckState();
        }
    }

    private void CheckState()
    {
        bool shouldBePressed = overlappingObjects.Count > 0;

        if (shouldBePressed && !isPressed)
        {
            isPressed = true;
            StopAllCoroutines();
            StartCoroutine(MovePlate(pressedPos));
            onPressed.Invoke();
            // Debug.Log("プレート ON");
        }
        else if (!shouldBePressed && isPressed)
        {
            isPressed = false;
            StopAllCoroutines();
            StartCoroutine(MovePlate(initialPos));
            onReleased.Invoke();
            // Debug.Log("プレート OFF");
        }
    }

    private bool IsValidObject(Collider col)
    {
        if (col.GetComponent<CharacterController>() != null) return IsTagValid(col.tag);
        if (col.attachedRigidbody != null) return IsTagValid(col.tag);
        return false;
    }

    private bool IsTagValid(string tag)
    {
        if (targetTags == null || targetTags.Count == 0) return true;
        return targetTags.Contains(tag);
    }

    private IEnumerator MovePlate(Vector3 target)
    {
        float speed = 5f;
        while (Vector3.Distance(transform.localPosition, target) > 0.001f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
            yield return null;
        }
        transform.localPosition = target;
    }

    // デバッグ用：検知範囲を可視化
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
    }
}