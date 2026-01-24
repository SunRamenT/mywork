using UnityEngine;
using System.Collections;

public class AthleticDoor : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("ドアが開く時の移動量（ローカル座標）")]
    public Vector3 moveOffset = new Vector3(0, 3, 0); // デフォルトは上に3m開く
    [Tooltip("開閉にかかる時間")]
    public float duration = 1.0f;

    private Vector3 closedPos;
    private Vector3 openPos;
    private Coroutine currentCoroutine;

    void Start()
    {
        closedPos = transform.localPosition;
        openPos = closedPos + moveOffset;
    }

    // スイッチの UnityEvent から呼び出す関数
    public void Open()
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(MoveTo(openPos));
    }

    // スイッチの UnityEvent から呼び出す関数
    public void Close()
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(MoveTo(closedPos));
    }

    private IEnumerator MoveTo(Vector3 targetPos)
    {
        float elapsed = 0f;
        Vector3 startPos = transform.localPosition;

        while (elapsed < duration)
        {
            transform.localPosition = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = targetPos;
    }
}