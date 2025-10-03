using UnityEngine;

public class CharacterPower : MonoBehaviour
{
    [Tooltip("ボールを押す力")]
    public float pushPower = 5f;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && !other.isTrigger && !rb.isKinematic)
        {
            // 衝突方向を計算（ボールの位置 - 拳の位置）
            Vector3 pushDir = other.transform.position - transform.position;

            // Y成分を無視して水平に限定
            pushDir.y = 0f;

            // 方向を正規化して力の大きさを統一
            pushDir.Normalize();

            // インパルスで力を加える
            rb.AddForce(pushDir * pushPower, ForceMode.Impulse);
        }
    }
}