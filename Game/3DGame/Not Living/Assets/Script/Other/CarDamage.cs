using UnityEngine;

// Rigidbody と Collider が必須
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CarDamage : MonoBehaviour
{
    [Header("車の設定")]
    public int damage = 100;

    public GameObject owner; // 搭乗者

    private void Awake()
    {
        // Collider は Trigger にする
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        // Rigidbody の設定
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public void Initialize(GameObject owner)
    {
        this.owner = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"{owner.name} の弾が {other.name} に当たった");
        // 自分や発射者には当たらない
        if (other.transform == owner.transform) return;

        // StatusManager を持つ親オブジェクトを探す
        StatusManager victim = other.GetComponentInParent<StatusManager>();
        if (victim != null)
        {
            //Debug.Log($"{owner.name} の弾が {victim.name} に命中！ {damage} のダメージを与えます。");
            victim.TakeDamage(damage, owner);
        }
    }
}
