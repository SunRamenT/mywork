using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PressurePlate : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("踏んだ時に沈み込む深さ")]
    public float pressDepth = 0.1f;
    [Tooltip("反応する対象のタグ（空なら何でも反応）")]
    public string targetTag = ""; // "Player", "Trash" など

    [Header("イベント")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    private int objectCount = 0; // 乗っている物体の数
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
        // 1. タグ指定がある場合、違うタグなら無視
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;
        // 2. Trigger（判定用コライダー）は無視（足場用コライダーのみ反応）
        if (other.isTrigger) return;

        objectCount++;

        // 0個から1個になった瞬間だけ ON
        if (objectCount == 1 && !isPressed)
        {
            isPressed = true;
            StopAllCoroutines();
            StartCoroutine(MovePlate(pressedPos));
            onPressed.Invoke();
            Debug.Log("プレート ON");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;
        if (other.isTrigger) return;

        objectCount--;

        // カウントがマイナスにならないように安全策
        if (objectCount < 0) objectCount = 0;

        // 全ての物体がいなくなったら OFF
        if (objectCount == 0 && isPressed)
        {
            isPressed = false;
            StopAllCoroutines();
            StartCoroutine(MovePlate(initialPos));
            onReleased.Invoke();
            Debug.Log("プレート OFF");
        }
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
}